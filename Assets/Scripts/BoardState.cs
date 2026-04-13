using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct StonePlacement
{
	public Vector2 position;
	public float strength;
}

public class BoardState
{
	public BoardState(int playerCount = 2)
	{
		PlayerCount = playerCount;
	}

	public BoardState(BoardState original)
	{
		PlayerCount = original.PlayerCount;
		Size = original.Size;
		StoneVariance = original.StoneVariance;
		Threshold = original.Threshold;
		stones = original.stones.Select(ps => new List<StonePlacement>(ps)).ToList();
	}

	readonly List<List<StonePlacement>> stones = new();

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

	public float Size { get; set; } = 19;
	public float StoneVariance { get; set; } = 1;
	public float Threshold { get; set; } = 1f / Mathf.Sqrt(32);

	public void AddStone(int player, Vector2 position, float strength = 1)
	{
		if(player < 0 || player >= PlayerCount)
			throw new System.IndexOutOfRangeException("Player index our of range.");
		stones[player].Add(new()
		{
			position = position,
			strength = strength,
		}
		);
	}

	public IReadOnlyList<StonePlacement> GetStones(int player)
	{
		if(player < 0 || player >= PlayerCount)
			throw new System.IndexOutOfRangeException("Player index our of range.");
		return stones[player];
	}

	public bool PlaceStone(int player, Vector2 position, float strength = 1) {
		if(!PeekStonePlacement(player, position, out _, strength))
			return false;

		AddStone(player, position, strength);
		return true;
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

		if(position.x < 0 || position.x > Size || position.y < 0 || position.y > Size)
		{
			newState = null;
			return false;
		}

		if(IsOccupied(position))
		{
			newState = null;
			return false;
		}

		newState = new(this);
		newState.AddStone(player, position, strength);
		return true;
	}

	public bool IsOccupied(Vector2 position)
	{
		if(position.x < 0 || position.x > Size || position.y < 0 || position.y > Size)
			return false;

		return EvaluateTotalDensity(position) >= Threshold;
	}

	float EvaluateTotalDensity(Vector2 position)
	{
		float totalDensity = 0;
		for(int player = 0; player < PlayerCount; ++player)
		{
			IReadOnlyList<StonePlacement> playerStones = GetStones(player);
			for(int i = 0; i < playerStones.Count; ++i)
				totalDensity += EvaluateStoneDensityWithMirrors(position, playerStones[i]);
		}

		return totalDensity;
	}

	float EvaluateStoneDensityWithMirrors(Vector2 position, StonePlacement stone)
	{
		float invDoubleVariance = 0.5f / Mathf.Max(StoneVariance, 0.0001f);

		float SumTerm(float x, float y)
		{
			Vector2 delta = position - new Vector2(x, y);
			return stone.strength * Mathf.Exp(-Vector2.Dot(delta, delta) * invDoubleVariance);
		}

		float rightX = 2 * Size - stone.position.x;
		float topY = 2 * Size - stone.position.y;

		return
			SumTerm(stone.position.x, stone.position.y) +
			SumTerm(-stone.position.x, stone.position.y) +
			SumTerm(rightX, stone.position.y) +
			SumTerm(stone.position.x, -stone.position.y) +
			SumTerm(stone.position.x, topY) +
			SumTerm(-stone.position.x, -stone.position.y) +
			SumTerm(-stone.position.x, topY) +
			SumTerm(rightX, -stone.position.y) +
			SumTerm(rightX, topY);
	}
}
