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
	public BoardState(int playerCount = 2, int size = 19)
	{
		PlayerCount = playerCount;
		Size = size;
	}

	public BoardState(BoardState original)
	{
		PlayerCount = original.PlayerCount;
		Size = original.Size;
		StoneHardness = original.StoneHardness;
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
	public float BoardStateExtent => Size - 1;
	public float StoneHardness { get; set; } = 0.5f;

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

	public bool TryFindNearestStone(Vector2 absolutePosition, float maxDistance, out int player, out int stoneIndex)
	{
		player = -1;
		stoneIndex = -1;
		float bestSqr = maxDistance * maxDistance;

		for(int p = 0; p < PlayerCount; ++p)
		{
			IReadOnlyList<StonePlacement> playerStones = stones[p];
			for(int i = 0; i < playerStones.Count; ++i)
			{
				float sqr = (playerStones[i].position - absolutePosition).sqrMagnitude;
				if(sqr < bestSqr)
				{
					bestSqr = sqr;
					player = p;
					stoneIndex = i;
				}
			}
		}

		return player >= 0;
	}

	public bool TryRemoveStoneAtAbsolutePosition(Vector2 absolutePosition, out BoardState newState, float searchRadius = 0.5f)
	{
		newState = null;

		if(!TryFindNearestStone(absolutePosition, searchRadius, out int player, out int stoneIndex))
			return false;

		newState = new(this);
		newState.RemoveStoneAt(player, stoneIndex);
		return true;
	}

	public bool TryRemoveStoneAtLogicalPosition(Vector2 position, out BoardState newState, float searchRadius = 0.5f)
	{
		return TryRemoveStoneAtAbsolutePosition(position, out newState, searchRadius);
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
}
