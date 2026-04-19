using UnityEngine;

public enum MatchMode
{
	Undefined = 0,
	Traditional = 1,
	Training = 0xffff,
}

public struct MatchConfig
{
	public MatchMode matchMode;
	public int boardSize;

	public static MatchConfig Default => new()
	{
		matchMode = MatchMode.Training,
		boardSize = 19,
	};
}
