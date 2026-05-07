using UnityEngine;
using System.Linq;
using System.Collections;

public static class GameUtility
{
	public static void ClearChildren(this Transform root)
	{
		var targets = Enumerable.Range(0, root.childCount).Select(root.GetChild).ToArray();
		foreach(var t in targets)
			Object.Destroy(t.gameObject);
	}

	public static string ToLocalizedString(this LobbyVisibility value)
	{
		return value switch
		{
			LobbyVisibility.Local => "本地",
			LobbyVisibility.Private => "私密",
			LobbyVisibility.Public => "公开",
			_ => null,
		};
	}

	public static string ToLocalizedString(this PlayerType value)
	{
		return value switch
		{
			PlayerType.Local => "本地玩家",
			PlayerType.Ai => "AI 玩家",
			PlayerType.Online => "在线玩家",
			_ => null,
		};
	}

	public static string ToLocalizedString(this BoardShape value)
	{
		return value switch
		{
			BoardShape.Square => "正方形",
			BoardShape.Sphere => "球形",
			_ => null,
		};
	}

	public static bool IsValidIndex(this IList list, int i)
	{
		return i >= 0 && i < list.Count;
	}
}
