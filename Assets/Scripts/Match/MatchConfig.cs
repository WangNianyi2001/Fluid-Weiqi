using UnityEngine;

public enum MatchMode
{
	Undefined = 0,
	Traditional = 1,
	Training = 0xffff,
}

public struct MatchConfig
{
	public MatchMode mode;
	public int playerCount;
	public int boardSize;

	public static MatchConfig Default => new()
	{
		mode = MatchMode.Training,
		boardSize = 19,
	};
}
