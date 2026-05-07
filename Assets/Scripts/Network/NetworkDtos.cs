using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public enum NetworkConnectionState
{
	Connected,
	Degraded,
	Disconnected,
}

public enum MatchActionType
{
	Place,
	Pass,
	Remove,
}

[System.Serializable]
public sealed class MatchActionRequest
{
	public PlayerLocator playerLocator;
	public int playerIndex;
	public MatchActionType actionType;
	public Vector2 position;
	public int turnSeq;
	public int actionSeq;
}

[System.Serializable]
public struct StonePlacementSnapshot
{
	public int playerIndex;
	public int stoneId;
	public Vector2 position;
	public float strength;
}

[System.Serializable]
public sealed class BoardStateSnapshot
{
	public int playerCount;
	public float size;
	public float stoneHardness;
	public float shrinkMargin;
	public List<StonePlacementSnapshot> stones = new();
}

[System.Serializable]
public sealed class MatchFlowSnapshot
{
	public int currentPlayerIndex;
	public int turnSeq;
	public bool isEnded;
	public bool[] passStates;
}

[System.Serializable]
public sealed class MatchActionResult
{
	public bool accepted;
	public string reason;
	public int playerIndex;
	public int actionSeq;
	public BoardStateSnapshot boardSnapshot;
	public MatchFlowSnapshot flowSnapshot;
}

[System.Serializable]
public sealed class LobbyPlayerSnapshot
{
	public PlayerType type;
	public bool isHost;
	public PlayerLocator locator;
	public string aiId;
	public int colorIndex;
}

[System.Serializable]
public sealed class LobbySyncSnapshot
{
	public LobbyLocator lobbyLocator;
	public int version;
	public bool isMatchInProgress;
	public LobbyVisibility visibility;
	public MatchRule matchRule;
	public string invitationCode;
	public List<LobbyPlayerSnapshot> players = new();
}

public static class NetworkSnapshotUtility
{
	public static BoardStateSnapshot BuildBoardSnapshot(BoardState state)
	{
		if(state == null)
			return null;

		BoardStateSnapshot snapshot = new BoardStateSnapshot
		{
			playerCount = state.PlayerCount,
			size = state.Size,
			stoneHardness = state.StoneHardness,
			shrinkMargin = state.ShrinkMargin,
		};

		for(int player = 0; player < state.PlayerCount; ++player)
		{
			IReadOnlyList<StonePlacement> stones = state.GetStones(player);
			for(int i = 0; i < stones.Count; ++i)
			{
				StonePlacement stone = stones[i];
				snapshot.stones.Add(new StonePlacementSnapshot
				{
					playerIndex = player,
					stoneId = stone.id,
					position = stone.position,
					strength = stone.strength,
				});
			}
		}

		return snapshot;
	}

	public static BoardState ToBoardState(BoardStateSnapshot snapshot)
	{
		if(snapshot == null)
			return null;

		BoardState state = new BoardState(snapshot.playerCount, Mathf.Max(1, Mathf.RoundToInt(snapshot.size)))
		{
			StoneHardness = snapshot.stoneHardness,
		};
		state.SetSize(snapshot.size);
		state.SetShrinkMargin(snapshot.shrinkMargin);

		if(snapshot.stones != null)
		{
			foreach(StonePlacementSnapshot stone in snapshot.stones)
				state.AddStone(stone.playerIndex, stone.position, stone.strength);
		}

		return state;
	}

	public static LobbySyncSnapshot BuildLobbySnapshot(Lobby lobby, int version)
	{
		LobbySyncSnapshot snapshot = new LobbySyncSnapshot
		{
			lobbyLocator = lobby.Locator,
			version = version,
			isMatchInProgress = lobby is HostLobby hostLobby && hostLobby.IsMatchInProgress,
			visibility = lobby.Visibility,
			matchRule = lobby.MatchRule,
			invitationCode = lobby.GetInvitationCode(),
		};

		for(int i = 0; i < lobby.Players.Count; ++i)
		{
			PlayerDescriptor player = lobby.Players[i];
			if(player == null)
				continue;

			snapshot.players.Add(new LobbyPlayerSnapshot
			{
				type = player.type,
				isHost = player.isHost,
				locator = player.locator,
				aiId = player.aiId,
				colorIndex = player.colorIndex,
			});
		}

		return snapshot;
	}

	public static List<PlayerDescriptor> ToPlayerDescriptors(IReadOnlyList<LobbyPlayerSnapshot> snapshots)
	{
		if(snapshots == null)
			return new List<PlayerDescriptor>();

		return snapshots.Select(s => new PlayerDescriptor
		{
			type = s.type,
			isHost = s.isHost,
			locator = s.locator,
			aiId = s.aiId,
			colorIndex = s.colorIndex,
		}).ToList();
	}
}
