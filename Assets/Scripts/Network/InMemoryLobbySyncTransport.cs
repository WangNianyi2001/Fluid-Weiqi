using System;
using System.Collections.Generic;

public class InMemoryLobbySyncTransport : ILobbySyncTransport
{
	sealed class LobbyRoom
	{
		public InMemoryLobbySyncTransport host;
		public readonly HashSet<InMemoryLobbySyncTransport> clients = new();
	}

	static readonly Dictionary<string, LobbyRoom> roomById = new();

	public event Action<LobbySyncSnapshot> OnSnapshotReceived;
	public event Action<PlayerLocator> OnClientDisconnected;

	public bool IsHost { get; private set; }
	public LobbyLocator LobbyLocator { get; private set; }
	public PlayerLocator LocalPlayerLocator { get; private set; }

	public void ConfigureAsHost(LobbyLocator lobbyLocator, PlayerLocator hostLocator)
	{
		DetachCurrentRoom();
		IsHost = true;
		LobbyLocator = lobbyLocator;
		LocalPlayerLocator = hostLocator;

		if(!lobbyLocator.IsValid)
			return;

		LobbyRoom room = GetOrCreateRoom(lobbyLocator.id);
		room.host = this;
	}

	public void ConfigureAsClient(LobbyLocator lobbyLocator, PlayerLocator localPlayerLocator)
	{
		DetachCurrentRoom();
		IsHost = false;
		LobbyLocator = lobbyLocator;
		LocalPlayerLocator = localPlayerLocator;

		if(!lobbyLocator.IsValid)
			return;

		LobbyRoom room = GetOrCreateRoom(lobbyLocator.id);
		room.clients.Add(this);
	}

	public void BroadcastSnapshot(LobbySyncSnapshot snapshot)
	{
		if(!IsHost || !LobbyLocator.IsValid)
			return;

		LobbyRoom room = GetOrCreateRoom(LobbyLocator.id);
		foreach(InMemoryLobbySyncTransport client in room.clients)
			client.OnSnapshotReceived?.Invoke(snapshot);
	}

	public void NotifyClientDisconnected(PlayerLocator playerLocator)
	{
		if(IsHost)
			return;
		if(!LobbyLocator.IsValid)
			return;

		LobbyRoom room = GetOrCreateRoom(LobbyLocator.id);
		room.host?.OnClientDisconnected?.Invoke(playerLocator);
	}

	void DetachCurrentRoom()
	{
		if(!LobbyLocator.IsValid || !roomById.TryGetValue(LobbyLocator.id, out LobbyRoom room))
			return;

		if(room.host == this)
			room.host = null;
		room.clients.Remove(this);
	}

	static LobbyRoom GetOrCreateRoom(string lobbyId)
	{
		if(!roomById.TryGetValue(lobbyId, out LobbyRoom room))
		{
			room = new LobbyRoom();
			roomById.Add(lobbyId, room);
		}
		return room;
	}
}
