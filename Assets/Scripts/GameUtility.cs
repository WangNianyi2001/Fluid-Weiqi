using UnityEngine;

public static class GameUtility
{
	public static PlayerInfo[] MakePlayerInfos(int count)
	{
		return count switch
		{
			2 => new PlayerInfo[]
			{
				new() { name = "黑方", color = Color.black, },
				new() { name = "白方", color = Color.white, },
			},
			3 => new PlayerInfo[]
			{
				new() { name = "红方", color = Color.red, },
				new() { name = "绿方", color = Color.green, },
				new() { name = "蓝方", color = Color.blue, },
			},
			4 => new PlayerInfo[]
			{
				new() { name = "红方", color = Color.red, },
				new() { name = "黄方", color = Color.yellow, },
				new() { name = "蓝方", color = Color.blue, },
				new() { name = "绿方", color = Color.green, },
			},
			_ => throw new System.NotImplementedException(),
		};
	}
}
