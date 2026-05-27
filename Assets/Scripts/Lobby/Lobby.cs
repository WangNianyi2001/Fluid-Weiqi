using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public enum LobbyVisibility
{
	Local, Private, Public
}

[System.Serializable]
public struct LobbyLocator
{
	public string id;

	public LobbyLocator(string id)
	{
		this.id = id;
	}

	public bool IsValid => !string.IsNullOrWhiteSpace(id);
}

public enum PlayerType
{
	Local, Ai, Online
}

public enum LobbyMatchEndReason
{
	Unknown,
	HostEnded,
	ConnectionLost,
}

[System.Serializable]
public struct PlayerLocator
{
	public string id;

	public PlayerLocator(string id)
	{
		this.id = id;
	}

	public bool IsValid => !string.IsNullOrWhiteSpace(id);

	public override string ToString()
	{
		return id ?? string.Empty;
	}

	public override bool Equals(object obj)
	{
		return obj is PlayerLocator other && string.Equals(id, other.id, StringComparison.Ordinal);
	}

	public override int GetHashCode()
	{
		return id != null ? StringComparer.Ordinal.GetHashCode(id) : 0;
	}

	public static bool operator ==(PlayerLocator left, PlayerLocator right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(PlayerLocator left, PlayerLocator right)
	{
		return !left.Equals(right);
	}
}

public class PlayerDescriptor
{
	public PlayerType type;
	public bool isHost;
	public PlayerLocator locator;
	public string aiId;
	public int colorIndex;

	public int Index => Lobby.Current?.Players.IndexOf(this) ?? -1;

	public Color color => GameSettings.Instance?.GetPlayerColor(colorIndex) ?? Color.white;
	public string GetLocalizedName()
	{
		string steamName = TryGetSteamPersonaName(locator);

		return type switch
		{
			PlayerType.Local => !string.IsNullOrWhiteSpace(steamName) ? steamName : $"本地玩家 {Index}",
			PlayerType.Ai => GetAiDisplayName(),
			PlayerType.Online => !string.IsNullOrWhiteSpace(steamName) ? steamName : $"网络玩家 {Index}",
			_ => null,
		};
	}

	static string TryGetSteamPersonaName(PlayerLocator playerLocator)
	{
		if(!playerLocator.IsValid)
			return null;

#if !DISABLESTEAMWORKS
		if(!SteamManager.Initialized)
			return null;
		if(!ulong.TryParse(playerLocator.id, out ulong rawId))
			return null;

		string name = Steamworks.SteamFriends.GetFriendPersonaName(new Steamworks.CSteamID(rawId));
		if(string.IsNullOrWhiteSpace(name))
			return null;
		return name;
#else
		return null;
#endif
	}

	string GetAiDisplayName()
	{
		if(GameManager.Instance != null
			&& !string.IsNullOrWhiteSpace(aiId)
			&& GameManager.Instance.TryGetAiConfig(aiId, out AiConfig config)
			&& !string.IsNullOrWhiteSpace(config.AiName))
		{
			return config.AiName;
		}

		return $"AI 玩家 {Index}";
	}
}

public abstract class Lobby
{
	public static Lobby Current
	{
		get
		{
			if(GameManager.Instance == null)
				return null;
			return GameManager.Instance.Lobby;
		}
	}

	#region Status
	public bool IsHost => this is HostLobby;
	public bool IsOnline => Visibility != LobbyVisibility.Local;
	public LobbyMatchEndReason LastMatchEndReason { get; protected set; } = LobbyMatchEndReason.Unknown;

	public Action OnDismissed;
	public Action OnStartingMatch;
	public Action OnMatchEnded;
	#endregion

	#region Lobby settings
	public abstract LobbyLocator Locator { get; }
	public abstract LobbyVisibility Visibility { get; }
	public Action OnVisibilityChanged;

	public virtual string GetInvitationCode() => null;
	#endregion

	#region Players
	public abstract List<PlayerDescriptor> Players { get; }
	public abstract PlayerLocator LocalPlayerLocator { get; }
	public PlayerDescriptor HostPlayer => Players.FirstOrDefault(p => p.isHost);
	public IEnumerable<PlayerDescriptor> UniqueOnlinePlayers => Players
		.Where(p => p != null && p.type == PlayerType.Online)
		.GroupBy(p => p.locator)
		.Select(g => g.First());
	public Action OnPlayersChanged;

	public bool IsOwnedByLocal(PlayerDescriptor descriptor)
	{
		if(descriptor == null)
			return false;

		if(LocalPlayerLocator.IsValid && descriptor.locator.IsValid)
			return descriptor.locator == LocalPlayerLocator;

		// Backward compatibility while locator data is still being wired.
		if(IsHost)
			return descriptor.type != PlayerType.Online;

		return false;
	}
	#endregion

	#region Match rules
	public abstract MatchRule MatchRule { get; }
	public Action OnMatchRuleChanged;

	public bool ValidateStartingCondition(out string errorMessage)
	{
		errorMessage = null;

		if(Players == null || Players.Count < 2)
		{
			errorMessage = "至少需要 2 名玩家才能开始对局。";
			return false;
		}

		if(GameManager.Instance == null)
		{
			errorMessage = "GameManager 未初始化。";
			return false;
		}

		if(string.IsNullOrWhiteSpace(MatchRule.modeId))
		{
			errorMessage = "未设置对局模式。";
			return false;
		}

		if(!GameManager.Instance.TryGetMatchModeConfig(MatchRule.modeId, out MatchModeConfig modeConfig))
		{
			errorMessage = $"未找到对局模式配置: {MatchRule.modeId}";
			return false;
		}

		if(IsOnline && !modeConfig.IsLegacyMode)
		{
			errorMessage = "联机模式暂不支持 DLC 对局模式。";
			return false;
		}

		if(!modeConfig.ValidateRules(MatchRule, this, out errorMessage))
			return false;

		for(int i = 0; i < Players.Count; ++i)
		{
			PlayerDescriptor player = Players[i];
			if(player == null || player.type != PlayerType.Ai)
				continue;

			if(string.IsNullOrWhiteSpace(player.aiId))
			{
				errorMessage = $"AI 玩家 #{i} 未配置 aiId。";
				return false;
			}

			if(!GameManager.Instance.TryGetAiConfig(player.aiId, out AiConfig aiConfig))
			{
				errorMessage = $"未找到 AI 配置: {player.aiId}";
				return false;
			}

			if(!aiConfig.SupportsMode(MatchRule.modeId))
			{
				errorMessage = $"AI '{aiConfig.AiName}' 不支持模式 '{MatchRule.modeId}'。";
				return false;
			}
		}

		return true;
	}
	#endregion
}