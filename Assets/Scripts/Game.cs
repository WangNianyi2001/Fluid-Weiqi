using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(GameInput))]
public class Game : MonoBehaviour
{
	[SerializeField] Board board;
	[SerializeField] int currentPlayerIndex = 0;
	BoardState previewState;
	bool hasPreview;

	public int CurrentPlayerIndex => currentPlayerIndex;
	public Board Board => board;
	public BoardState State => board.State;

	void Start()
	{
		board.RefreshRendering();
	}

	public bool TryPlaceStone(Vector2 logicalPosition, float strength = 1)
	{
		RenderCurrentStateAnalysis();

		if(board.IsOccupiedAtLogicalPosition(State, logicalPosition))
			return false;

		if(!State.PeekStonePlacement(currentPlayerIndex, logicalPosition, out BoardState previewState, strength))
			return false;

		board.RefreshRendering(previewState);

		List<Board.ChainStat> chainStats = board.GetChainStats();
		Dictionary<int, Board.ChainStat> chainStatsByRoot = new(chainStats.Count);
		HashSet<int> capturedRoots = new();
		for(int i = 0; i < chainStats.Count; ++i)
		{
			Board.ChainStat chainStat = chainStats[i];
			chainStatsByRoot[chainStat.RootLabel] = chainStat;
			if(chainStat.Owner != currentPlayerIndex && !chainStat.HasLiberty)
				capturedRoots.Add(chainStat.RootLabel);
		}

		int placedChainRoot = board.GetChainLabelAtLogicalPosition(previewState, logicalPosition);
		bool placedChainHasLiberty = chainStatsByRoot.TryGetValue(placedChainRoot, out Board.ChainStat placedChainStat) && placedChainStat.HasLiberty;
		if(capturedRoots.Count == 0 && !placedChainHasLiberty)
		{
			board.RefreshRendering();
			return false;
		}

		if(capturedRoots.Count > 0)
			RemoveCapturedStones(previewState, capturedRoots);

		board.SetState(previewState);
		hasPreview = false;
		previewState = null;

		currentPlayerIndex = (currentPlayerIndex + 1) % board.PlayerCount;
		return true;
	}

	public bool TryPreviewStone(Vector2 logicalPosition, float strength = 1)
	{
		RenderCurrentStateAnalysis();

		if(board.IsOccupiedAtLogicalPosition(State, logicalPosition))
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
		board.RefreshRendering(previewState);
		return true;
	}

	public void ClearPreview()
	{
		if(!hasPreview)
			return;

		hasPreview = false;
		previewState = null;
		RenderCurrentStateAnalysis();
	}

	void RenderCurrentStateAnalysis()
	{
		board.RefreshRendering();
	}

	void RemoveCapturedStones(BoardState renderState, HashSet<int> capturedRoots)
	{
		List<List<int>> stoneChainLabels = board.GetStoneChainLabels(renderState);
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
}
