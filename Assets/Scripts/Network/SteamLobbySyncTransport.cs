#if !DISABLESTEAMWORKS
using System;
using UnityEngine;
using Steamworks;

/// <summary>
/// Steam-backed implementation of ILobbySyncTransport.
///
/// Host writes the serialized LobbySyncSnapshot as a single Steam Lobby
/// metadata entry under the key "snapshot". Steam automatically pushes
/// LobbyDataUpdate_t to every member whenever any metadata changes, so no
/// manual per-client messaging is needed.
///
/// Member join/leave is detected via LobbyChatUpdate_t.
/// </summary>
public class SteamLobbySyncTransport : ILobbySyncTransport
{
	const string SnapshotKey = "snapshot";

	// -------------------------------------------------------------------------
	// ILobbySyncTransport
	// -------------------------------------------------------------------------

	public event Action<LobbySyncSnapshot> OnSnapshotReceived;
	public event Action<PlayerLocator> OnClientDisconnected;

	public bool IsHost { get; private set; }
	public LobbyLocator LobbyLocator { get; private set; }
	public PlayerLocator LocalPlayerLocator { get; private set; }

	// -------------------------------------------------------------------------
	// Steam callbacks (registered on configure, disposed on detach)
	// -------------------------------------------------------------------------

	Callback<LobbyDataUpdate_t>  cbLobbyDataUpdate;
	Callback<LobbyChatUpdate_t>  cbLobbyChatUpdate;

	// -------------------------------------------------------------------------
	// Configure
	// -------------------------------------------------------------------------

	public void ConfigureAsHost(LobbyLocator lobbyLocator, PlayerLocator hostLocator)
	{
		Detach();
		IsHost = true;
		LobbyLocator = lobbyLocator;
		LocalPlayerLocator = hostLocator;
		RegisterCallbacks();
	}

	public void ConfigureAsClient(LobbyLocator lobbyLocator, PlayerLocator localPlayerLocator)
	{
		Detach();
		IsHost = false;
		LobbyLocator = lobbyLocator;
		LocalPlayerLocator = localPlayerLocator;
		RegisterCallbacks();
	}

	// -------------------------------------------------------------------------
	// BroadcastSnapshot (host only)
	// Host writes to lobby metadata; Steam pushes LobbyDataUpdate_t to all members.
	// -------------------------------------------------------------------------

	public void BroadcastSnapshot(LobbySyncSnapshot snapshot)
	{
		if(!IsHost || !LobbyLocator.IsValid)
			return;
		if(!ulong.TryParse(LobbyLocator.id, out ulong rawId))
			return;

		string json = NetworkSerializer.SerializeLobbySnapshot(snapshot);
		SteamMatchmaking.SetLobbyData(new CSteamID(rawId), SnapshotKey, json);
	}

	// -------------------------------------------------------------------------
	// NotifyClientDisconnected (client only)
	// Clients just leave the Steam lobby; the host detects this via LobbyChatUpdate_t.
	// Nothing to send explicitly.
	// -------------------------------------------------------------------------

	public void NotifyClientDisconnected(PlayerLocator playerLocator)
	{
		// No-op: Steam lobby departure is detected on the host via LobbyChatUpdate_t.
	}

	// -------------------------------------------------------------------------
	// Internal
	// -------------------------------------------------------------------------

	void RegisterCallbacks()
	{
		cbLobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
		cbLobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
	}

	void Detach()
	{
		cbLobbyDataUpdate?.Dispose();
		cbLobbyChatUpdate?.Dispose();
		cbLobbyDataUpdate = null;
		cbLobbyChatUpdate = null;
	}

	void OnLobbyDataUpdate(LobbyDataUpdate_t data)
	{
		// Only care about metadata on our specific lobby (not member-level data)
		if(data.m_ulSteamIDLobby != data.m_ulSteamIDMember)
			return;
		if(!LobbyLocator.IsValid)
			return;
		if(!ulong.TryParse(LobbyLocator.id, out ulong rawId))
			return;
		if(data.m_ulSteamIDLobby != rawId)
			return;

		CSteamID steamLobbyId = new CSteamID(rawId);
		string json = SteamMatchmaking.GetLobbyData(steamLobbyId, SnapshotKey);
		if(string.IsNullOrEmpty(json))
			return;

		LobbySyncSnapshot snapshot = NetworkSerializer.DeserializeLobbySnapshot(json);
		if(snapshot != null)
			OnSnapshotReceived?.Invoke(snapshot);
	}

	void OnLobbyChatUpdate(LobbyChatUpdate_t data)
	{
		if(!IsHost)
			return;
		if(!LobbyLocator.IsValid)
			return;
		if(!ulong.TryParse(LobbyLocator.id, out ulong rawId))
			return;
		if(data.m_ulSteamIDLobby != rawId)
			return;

		// Fire for any state that means the member has left
		const uint leftFlags =
			(uint)EChatMemberStateChange.k_EChatMemberStateChangeLeft       |
			(uint)EChatMemberStateChange.k_EChatMemberStateChangeDisconnected |
			(uint)EChatMemberStateChange.k_EChatMemberStateChangeKicked      |
			(uint)EChatMemberStateChange.k_EChatMemberStateChangeBanned;

		if((data.m_rgfChatMemberStateChange & leftFlags) != 0)
		{
			var locator = new PlayerLocator(data.m_ulSteamIDUserChanged.ToString());
			OnClientDisconnected?.Invoke(locator);
		}
	}
}
#endif
