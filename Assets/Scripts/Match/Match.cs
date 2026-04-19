using UnityEngine;
using System.Collections.Generic;
using System;

public abstract class Match : MonoBehaviour
{
	#region References
	[SerializeField] Board board;
	public Board Board => board;
	public BoardState State => Board.State;
	#endregion

	#region Unity life cycle
	protected void Awake()
	{
		board = GetComponentInChildren<Board>();
		gameObject.AddComponent<MatchInput>();
	}

	protected void Start()
	{
		CurrentPlayerIndex = 0;
	}
	#endregion

	#region Current player
	[SerializeField] int currentPlayerIndex = 0;
	public int CurrentPlayerIndex
	{
		get => currentPlayerIndex;
		set
		{
			currentPlayerIndex = Mathf.Clamp(value, 0, Board.PlayerCount - 1);
			Board.RefreshRendering();
			StateCommitted?.Invoke();
			CurrentPlayerChanged?.Invoke(currentPlayerIndex);
		}
	}
	public event Action<int> CurrentPlayerChanged;
	#endregion

	#region Preview
	public event Action StateCommitted;
	BoardState previewState;
	bool hasPreview;

	public void ClearPreview()
	{
		if(!hasPreview)
			return;

		hasPreview = false;
		previewState = null;
		Board.RefreshRendering();
	}
	#endregion

	#region Stone placement
	public bool TryPlaceStone(Vector2 logicalPosition, float strength = 1)
	{
		Board.RefreshRendering();

		if(Board.IsOccupiedAtLogicalPosition(State, logicalPosition))
			return false;

		if(!State.PeekStonePlacement(currentPlayerIndex, logicalPosition, out BoardState previewState, strength))
			return false;

		Board.RefreshRendering(previewState);

		List<Board.ChainStat> chainStats = Board.GetChainStats();
		Dictionary<int, Board.ChainStat> chainStatsByRoot = new(chainStats.Count);
		HashSet<int> capturedRoots = new();
		for(int i = 0; i < chainStats.Count; ++i)
		{
			Board.ChainStat chainStat = chainStats[i];
			chainStatsByRoot[chainStat.RootLabel] = chainStat;
			if(chainStat.Owner != currentPlayerIndex && !chainStat.HasLiberty)
				capturedRoots.Add(chainStat.RootLabel);
		}

		int placedChainRoot = Board.GetChainLabelAtLogicalPosition(previewState, logicalPosition);
		bool placedChainHasLiberty = chainStatsByRoot.TryGetValue(placedChainRoot, out Board.ChainStat placedChainStat) && placedChainStat.HasLiberty;
		if(capturedRoots.Count == 0 && !placedChainHasLiberty)
		{
			Board.RefreshRendering();
			return false;
		}

		if(capturedRoots.Count > 0)
			RemoveCapturedStones(previewState, capturedRoots);

		Board.SetState(previewState);
		hasPreview = false;
		previewState = null;

		currentPlayerIndex = (currentPlayerIndex + 1) % Board.PlayerCount;
		StateCommitted?.Invoke();
		CurrentPlayerChanged?.Invoke(currentPlayerIndex);
		return true;
	}

	public bool TryPreviewStone(Vector2 logicalPosition, float strength = 1)
	{
		Board.RefreshRendering();

		if(Board.IsOccupiedAtLogicalPosition(State, logicalPosition))
		{
			ClearPreview();
			return false;
		}

		if(!State.PeekStonePlacement(currentPlayerIndex, logicalPosition, out BoardState newState, strength))
		{
			ClearPreview();
			return false;
		}

		hasPreview = true;
		previewState = newState;
		Board.RefreshRendering(previewState);
		return true;
	}

	void RemoveCapturedStones(BoardState renderState, HashSet<int> capturedRoots)
	{
		List<List<int>> stoneChainLabels = Board.GetStoneChainLabels(renderState);
		for(int player = 0; player < renderState.PlayerCount; ++player)
		{
			if(player == currentPlayerIndex)
				continue;

			List<int> playerLabels = stoneChainLabels[player];
			for(int stoneIndex = playerLabels.Count - 1; stoneIndex >= 0; --stoneIndex)
			{
				if(capturedRoots.Contains(playerLabels[stoneIndex]))
					renderState.RemoveStoneAt(player, stoneIndex);
			}
		}
	}
	#endregion
}
