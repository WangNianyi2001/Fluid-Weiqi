using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class HostLobby : Lobby
{
	public new static HostLobby Current => Lobby.Current as HostLobby;
	public static readonly PlayerLocator HostPlayerLocator = new("host");
	readonly LobbyLocator locator;
	readonly PlayerLocator localPlayerLocator;
	int lobbyVersion = 0;
	bool isMatchInProgress = false;

	#region Creation
	static PlayerLocator MakeRemotePlayerLocator(int i)
	{
		return new PlayerLocator($"remote-{i}");
	}

	static IEnumerable<PlayerDescriptor> MakeDefaultPlayerList(PlayerLocator hostLocalLocator)
	{
		yield return new()
		{
			type = PlayerType.Local,
			isHost = true,
			locator = hostLocalLocator,
			colorIndex = 0,
		};
		yield return new()
		{
			type = PlayerType.Ai,
			aiId = "lao-song",
			isHost = false,
			locator = hostLocalLocator,
			colorIndex = 1,
		};
	}

	static PlayerLocator ResolveHostLocalPlayerLocator()
	{
#if !DISABLESTEAMWORKS
		if(SteamManager.Initialized)
			return new PlayerLocator(Steamworks.SteamUser.GetSteamID().m_SteamID.ToString());
#endif
		return HostPlayerLocator;
	}

	static bool IsVacantOnlineSlot(PlayerDescriptor player)
	{
		if(player == null || player.type != PlayerType.Online)
			return false;
		if(!player.locator.IsValid)
			return true;
		return player.locator.id != null && player.locator.id.StartsWith("remote-", StringComparison.Ordinal);
	}

	PlayerDescriptor MakeNewPlayer(int i)
	{
		return new()
		{
			type = PlayerType.Local,
			isHost = false,
			locator = localPlayerLocator,
			colorIndex = (i + 2) % 4,
		};
	}

	public HostLobby(string defaultMatchModeId, LobbyLocator locator)
	{
		this.locator = locator;
		localPlayerLocator = ResolveHostLocalPlayerLocator();
		players.AddRange(MakeDefaultPlayerList(localPlayerLocator));
		matchRule = new MatchRule
		{
			modeId = defaultMatchModeId,
			boardSize = 19,
			stoneHardness = 0.25f,
			boardShape = BoardShape.Square,
			useShrinking = false,
			shrinkSpeed = 0.05f,
		};
	}
	#endregion

	#region Status
	public void StartMatch()
	{
		isMatchInProgress = true;
		PublishLobbySnapshot();
		OnStartingMatch?.Invoke();
	}

	public void Dismiss()
	{
		GameManager.Instance?.ExitLobby();
		OnDismissed?.Invoke();
	}

	public void EndMatch()
	{
		isMatchInProgress = false;
		PublishLobbySnapshot();
		OnMatchEnded?.Invoke();
	}

	public bool IsMatchInProgress => isMatchInProgress;

	public void NotifyClientConnected(PlayerLocator locator)
	{
		if(!locator.IsValid)
			return;

		for(int i = 0; i < players.Count; ++i)
		{
			PlayerDescriptor player = players[i];
			if(player == null || player.type != PlayerType.Online)
				continue;
			if(player.locator == locator)
				return;
		}

		for(int i = players.Count - 1; i >= 0; --i)
		{
			PlayerDescriptor player = players[i];
			if(!IsVacantOnlineSlot(player))
				continue;

			player.locator = locator;
			players[i] = player;
			OnPlayersChanged?.Invoke();
			PublishLobbySnapshot();
			return;
		}

		Debug.LogWarning($"No vacant online slot available for connected client '{locator}'.");
	}

	public void NotifyClientDisconnected(PlayerLocator locator)
	{
		if(!locator.IsValid)
			return;

		bool changed = false;
		for(int i = 0; i < players.Count; ++i)
		{
			PlayerDescriptor player = players[i];
			if(player == null || player.type != PlayerType.Online)
				continue;
			if(player.locator != locator)
				continue;

			player.locator = MakeRemotePlayerLocator(i);
			players[i] = player;
			changed = true;
		}

		if(!changed)
			return;

		Debug.LogWarning($"Client '{locator}' disconnected from host lobby.");
		OnPlayersChanged?.Invoke();
		if(isMatchInProgress)
		{
			EndMatch();
			return;
		}
		PublishLobbySnapshot();
	}
	#endregion

	#region Lobby settings
	public override LobbyLocator Locator => locator;

	LobbyVisibility visibility = LobbyVisibility.Local;
	public override LobbyVisibility Visibility => visibility;
	public void SetVisibility(LobbyVisibility value)
	{
		visibility = value;
		if(visibility != LobbyVisibility.Private)
			invitationCode = null;
		OnVisibilityChanged?.Invoke();

		if(visibility == LobbyVisibility.Private && string.IsNullOrEmpty(invitationCode))
		{
			var service = GameManager.Instance?.LobbyService;
			if(service != null)
			{
				service.RequestInvitationCode(locator, code =>
				{
					invitationCode = code;
					OnVisibilityChanged?.Invoke();
					PublishLobbySnapshot();
				});
				return; // broadcast deferred until code arrives
			}
		}

		PublishLobbySnapshot();
	}

	string invitationCode;
	public override string GetInvitationCode() =>
		Visibility == LobbyVisibility.Private ? invitationCode : null;
	#endregion

	#region Players
	readonly List<PlayerDescriptor> players = new();
	public override List<PlayerDescriptor> Players => players;
	public override PlayerLocator LocalPlayerLocator => localPlayerLocator;

	public void SetPlayerType(int i, PlayerType type)
	{
		if(!players.IsValidIndex(i))
		{
			Debug.LogWarning($"Failed to set player #{i}'s type to {type.ToLocalizedString()}.");
			return;
		}
		if(!IsOnline && type == PlayerType.Online)
		{
			Debug.LogWarning($"Cannot set player #{i}'s type to {type.ToLocalizedString()} because the lobby is offline.");
			return;
		}

		PlayerDescriptor player = players[i];
		player.type = type;
		player.locator = type == PlayerType.Online
			? MakeRemotePlayerLocator(i)
			: localPlayerLocator;
		if(type == PlayerType.Ai)
		{
			if(string.IsNullOrWhiteSpace(player.aiId) && GameManager.Instance != null)
			{
				AiConfig defaultAi = GameManager.Instance.FindFirstAiForMode(matchRule.modeId);
				player.aiId = defaultAi != null ? defaultAi.AiId : null;
			}
		}
		else
		{
			player.aiId = null;
		}

		players[i] = player;
		OnPlayersChanged?.Invoke();
		PublishLobbySnapshot();
	}

	public void SetPlayerAi(int i, string aiId)
	{
		if(!players.IsValidIndex(i))
		{
			Debug.LogWarning($"Failed to set player #{i}'s AI to '{aiId}'.");
			return;
		}
		PlayerDescriptor player = players[i];
		if(player.type != PlayerType.Ai)
			return;
		player.aiId = aiId;
		players[i] = player;
		OnPlayersChanged?.Invoke();
		PublishLobbySnapshot();
	}

	public void SetPlayerColor(int i, int colorIndex)
	{
		if(!players.IsValidIndex(i))
		{
			Debug.LogWarning($"Failed to set player #{i}'s color to index {colorIndex}.");
			return;
		}
		PlayerDescriptor player = players[i];
		player.colorIndex = colorIndex;
		players[i] = player;
		OnPlayersChanged?.Invoke();
		PublishLobbySnapshot();
	}

	public void RemovePlayer(int i)
	{
		if(!players.IsValidIndex(i))
		{
			Debug.LogWarning($"Failed to remove player #{i} because there are only {players.Count} players.");
			return;
		}
		if(players.Count <= 2)
		{
			Debug.LogWarning($"Cannot remove player #{i} because a match must have at least 2 players. Current player count = {players.Count}.");
			return;
		}
		players.RemoveAt(i);
		OnPlayersChanged?.Invoke();
		PublishLobbySnapshot();
	}

	public void AddPlayer()
	{
		if(players.Count >= 4)
		{
			Debug.LogWarning($"Cannot add new player because a match can have at most 4 players. Current player count = {players.Count}.");
			return;
		}
		players.Add(MakeNewPlayer(players.Count));
		OnPlayersChanged?.Invoke();
		PublishLobbySnapshot();
	}
	#endregion

	#region Match rules
	MatchRule matchRule;
	public override MatchRule MatchRule => matchRule;
	public void SetMatchRule(MatchRule value)
	{
		matchRule = value;
		OnMatchRuleChanged?.Invoke();
		PublishLobbySnapshot();
	}
	#endregion

	void PublishLobbySnapshot()
	{
		if(GameManager.Instance == null)
			return;
		if(!IsOnline)
			return;

		lobbyVersion += 1;
		LobbySyncSnapshot snapshot = NetworkSnapshotUtility.BuildLobbySnapshot(this, lobbyVersion);
		GameManager.Instance.LobbySyncTransport?.BroadcastSnapshot(snapshot);
	}
}