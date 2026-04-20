using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct StonePlacement
{
	public int id;
	public Vector2 position;
	public float strength;
}

public class BoardState
{
	const float StoneCollisionDiameter = 1f - 1e-4f;
	const float StoneCollisionDiameterSquared = StoneCollisionDiameter * StoneCollisionDiameter;

	public BoardState(int playerCount = 2, int size = 19)
	{
		PlayerCount = playerCount;
		Size = size;
	}

	public BoardState(BoardState original)
	{
		PlayerCount = original.PlayerCount;
		Size = original.Size;
		StoneVariance = original.StoneVariance;
		Threshold = original.Threshold;
		nextStoneId = original.nextStoneId;
		stones = original.stones.Select(ps => new List<StonePlacement>(ps)).ToList();
	}

	readonly List<List<StonePlacement>> stones = new();
	int nextStoneId = 1;

	public int PlayerCount
	{
		get => stones.Count;
		set
		{
			if(value < 2)
				throw new System.ArgumentOutOfRangeException("A board must have at least 2 players.");
			if(value > 4)
				throw new System.ArgumentOutOfRangeException("A board can have at most 4 players.");
			stones.Capacity = value;
			if(stones.Count > value)
				stones.RemoveRange(value, stones.Count - value);
			while(stones.Count < value)
				stones.Add(new());
		}
	}

	public float Size { get; private set; } = 19;
	public float StoneVariance { get; set; } = 1f / Mathf.Sqrt(16);
	public float Threshold { get; set; } = .5f;

	public void AddStone(int player, Vector2 position, float strength = 1)
	{
		AddStone(player, CreateStone(position, strength));
	}

	void AddStone(int player, StonePlacement stone)
	{
		if(player < 0 || player >= PlayerCount)
			throw new System.IndexOutOfRangeException("Player index our of range.");
		stones[player].Add(stone);
	}

	public void RemoveStoneAt(int player, int stoneIndex)
	{
		if(player < 0 || player >= PlayerCount)
			throw new System.IndexOutOfRangeException("Player index our of range.");
		stones[player].RemoveAt(stoneIndex);
	}

	public IReadOnlyList<StonePlacement> GetStones(int player)
	{
		if(player < 0 || player >= PlayerCount)
			throw new System.IndexOutOfRangeException("Player index our of range.");
		return stones[player];
	}

	public bool HasStoneOverlap(Vector2 position, int ignoredStoneId = -1)
	{
		for(int player = 0; player < PlayerCount; ++player)
		{
			IReadOnlyList<StonePlacement> playerStones = stones[player];
			for(int stoneIndex = 0; stoneIndex < playerStones.Count; ++stoneIndex)
			{
				StonePlacement stone = playerStones[stoneIndex];
				if(stone.id == ignoredStoneId)
					continue;

				if((stone.position - position).sqrMagnitude < StoneCollisionDiameterSquared)
					return true;
			}
		}

		return false;
	}

	public bool PeekStonePlacement(int player, Vector2 position, out BoardState newState, float strength = 1)
	{
		if(player < 0 || player >= PlayerCount)
		{
			newState = null;
			return false;
		}

		if(strength <= 0)
		{
			newState = null;
			return false;
		}

		if(position.x < 0 || position.x >= Size || position.y < 0 || position.y >= Size)
		{
			newState = null;
			return false;
		}

		newState = new(this);
		newState.AddStone(player, position, strength);
		return true;
	}

	public bool TryPlaceStone(
		int player,
		Vector2 position,
		Func<BoardState, List<BoardUtility.ChainStat>> getChainStats,
		Func<BoardState, Vector2, int> getChainLabelAtLogicalPosition,
		Func<BoardState, List<List<int>>> getStoneChainLabels,
		out BoardState newState,
		float strength = 1)
	{
		newState = null;
		if(getChainStats == null || getChainLabelAtLogicalPosition == null || getStoneChainLabels == null)
			throw new ArgumentNullException("BoardState.TryPlaceStone analysis callbacks must not be null.");

		if(!PeekStonePlacement(player, position, out BoardState placedPreviewState, strength))
			return false;

		StonePlacement placedStone = placedPreviewState.stones[player][placedPreviewState.stones[player].Count - 1];
		List<BoardUtility.ChainStat> chainStats = getChainStats(placedPreviewState);
		Dictionary<int, BoardUtility.ChainStat> chainStatsByRoot = new(chainStats.Count);
		HashSet<int> capturedRoots = new();

		for(int i = 0; i < chainStats.Count; ++i)
		{
			BoardUtility.ChainStat chainStat = chainStats[i];
			chainStatsByRoot[chainStat.rootLabel] = chainStat;
			if(chainStat.owner != player && chainStat.hasLiberty == 0)
				capturedRoots.Add(chainStat.rootLabel);
		}

		int placedChainRoot = getChainLabelAtLogicalPosition(placedPreviewState, position);
		bool placedChainHasLiberty = chainStatsByRoot.TryGetValue(placedChainRoot, out BoardUtility.ChainStat placedChainStat)
			&& placedChainStat.hasLiberty != 0;
		if(capturedRoots.Count == 0 && !placedChainHasLiberty)
			return false;

		if(capturedRoots.Count > 0)
			RemoveCapturedStones(placedPreviewState, capturedRoots, player, getStoneChainLabels);

		if(placedPreviewState.HasStoneOverlap(placedStone.position, placedStone.id))
			return false;

		newState = placedPreviewState;
		return true;
	}

	StonePlacement CreateStone(Vector2 position, float strength)
	{
		return new StonePlacement
		{
			id = nextStoneId++,
			position = position,
			strength = strength,
		};
	}

	static void RemoveCapturedStones(
		BoardState renderState,
		HashSet<int> capturedRoots,
		int currentPlayerIndex,
		Func<BoardState, List<List<int>>> getStoneChainLabels)
	{
		List<List<int>> stoneChainLabels = getStoneChainLabels(renderState);
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
