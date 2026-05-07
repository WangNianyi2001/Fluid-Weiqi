using UnityEngine;
using System.Collections.Generic;

public class SquareBoard : Board
{
	public override Bounds GetWorldBounds()
	{
		if(BoardRenderer != null)
			return BoardRenderer.bounds;

		return new Bounds(transform.position, Vector3.zero);
	}

	public override Vector2 WorldToBoardLocalPosition(Vector3 worldPosition)
	{
		Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
		return new Vector2(localPosition.x, localPosition.y);
	}

	public override Vector3 BoardLocalToWorldPosition(Vector2 boardLocalPosition)
	{
		return transform.TransformPoint(new Vector3(boardLocalPosition.x, boardLocalPosition.y, 0));
	}

	public override Vector2 BoardLocalToAbsolutePosition(Vector2 boardLocalPosition)
	{
		float span = State.BoardStateExtent;
		return new Vector2((boardLocalPosition.x + .5f) * span, (boardLocalPosition.y + .5f) * span);
	}

	public override Vector2 AbsoluteToBoardLocalPosition(Vector2 absolutePosition)
	{
		float span = State.BoardStateExtent;
		return new Vector2(
			absolutePosition.x / span - .5f,
			absolutePosition.y / span - .5f
		);
	}

	public override BoardState TryShrink(BoardState current, float deltaMargin)
	{
		deltaMargin = Mathf.Max(0f, deltaMargin);
		float currentSize = Mathf.Max(1f, current.Size);
		float nextSize = currentSize - 2f * deltaMargin;

		// Keep at least a 2-point span to avoid degenerate geometry.
		if(nextSize < 2f)
			return null;

		BoardState nextState = new(current);
		nextState.SetSize(nextSize);
		nextState.SetShrinkMargin(current.ShrinkMargin + (currentSize - nextSize));

		float oldExtent = current.BoardStateExtent;
		float trim = deltaMargin;
		float newExtent = nextState.BoardStateExtent;

		// Trim from all four sides, then shift surviving stones into the new origin.
		for(int player = nextState.PlayerCount - 1; player >= 0; --player)
		{
			IReadOnlyList<StonePlacement> stones = nextState.GetStones(player);
			for(int stoneIndex = stones.Count - 1; stoneIndex >= 0; --stoneIndex)
			{
				StonePlacement stone = stones[stoneIndex];
				Vector2 pos = stone.position;
				if(pos.x < trim || pos.x > oldExtent - trim ||
				   pos.y < trim || pos.y > oldExtent - trim)
				{
					nextState.RemoveStoneAt(player, stoneIndex);
					continue;
				}

				nextState.RemoveStoneAt(player, stoneIndex);
				nextState.AddStone(player, new Vector2(
					Mathf.Clamp(pos.x - trim, 0f, newExtent),
					Mathf.Clamp(pos.y - trim, 0f, newExtent)),
					stone.strength);
			}
		}

		// Remove stones without liberties due to shrinking
		RemoveDeadStones(nextState);

		return nextState;
	}

	protected override void UpdateBoardScale()
	{
		// For square board, scale the board anchor (parent transform)
		if(transform.parent == null)
			return;

		float size = Mathf.Max(1f, State.Size);
		float initialSize = Mathf.Max(size, size + State.ShrinkMargin);
		float scaleRatio = size / initialSize;
		transform.parent.localScale = Vector3.one * scaleRatio;
	}

	void RemoveDeadStones(BoardState state)
	{
		if(Caches == null || !Caches.isInitialized)
			return;

		Color[] playerColors = new Color[state.PlayerCount];
		for(int i = 0; i < playerColors.Length; ++i)
			playerColors[i] = Color.white;
		
		BoardUtility.RenderAnalysis(Caches, state, playerColors);
		List<BoardUtility.ChainStat> chainStats = BoardUtility.GetChainStats(Caches);

		HashSet<int> capturedRoots = new();
		for(int i = 0; i < chainStats.Count; ++i)
		{
			if(chainStats[i].hasLiberty == 0)
				capturedRoots.Add(chainStats[i].rootLabel);
		}

		if(capturedRoots.Count == 0)
			return;

		// Remove captured stones
		List<List<int>> stoneChainLabels = BoardUtility.GetStoneChainLabels(Caches, state);
		for(int player = 0; player < state.PlayerCount; ++player)
		{
			List<int> playerLabels = stoneChainLabels[player];
			for(int stoneIndex = playerLabels.Count - 1; stoneIndex >= 0; --stoneIndex)
			{
				if(capturedRoots.Contains(playerLabels[stoneIndex]))
					state.RemoveStoneAt(player, stoneIndex);
			}
		}
	}

	protected override void UpdateGridMaterialParameters()
	{
		base.UpdateGridMaterialParameters();

		if(GridMaterial == null)
			return;

		int boardSize = Mathf.Max(2, Mathf.RoundToInt(State.Size));
		GridMaterial.SetFloat("_BoardSize", boardSize);
		GridMaterial.SetFloat("_StarEdgeOffset", BoardUtility.GetStarEdgeOffset(boardSize));
	}
}
