using System;
using System.Collections.Generic;

public class InMemoryMatchTransport : IMatchTransport
{
	sealed class MatchRoom
	{
		public InMemoryMatchTransport host;
		public readonly HashSet<InMemoryMatchTransport> clients = new();
	}

	static readonly Dictionary<string, MatchRoom> roomById = new();

	public event Action<MatchActionRequest> OnActionRequestReceived;
	public event Action<MatchActionResult> OnActionResultReceived;
	public event Action<NetworkConnectionState> OnConnectionStateChanged;

	public bool IsHost { get; private set; }
	public LobbyLocator LobbyLocator { get; private set; }
	public PlayerLocator LocalPlayerLocator { get; private set; }
	public NetworkConnectionState ConnectionState { get; private set; } = NetworkConnectionState.Connected;

	public void ConfigureAsHost(LobbyLocator lobbyLocator, PlayerLocator hostLocator)
	{
		DetachCurrentRoom();
		IsHost = true;
		LobbyLocator = lobbyLocator;
		LocalPlayerLocator = hostLocator;

		if(!lobbyLocator.IsValid)
			return;

		MatchRoom room = GetOrCreateRoom(lobbyLocator.id);
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

		MatchRoom room = GetOrCreateRoom(lobbyLocator.id);
		room.clients.Add(this);
	}

	public void SendActionRequest(MatchActionRequest request)
	{
		if(IsHost || !LobbyLocator.IsValid)
			return;

		MatchRoom room = GetOrCreateRoom(LobbyLocator.id);
		room.host?.OnActionRequestReceived?.Invoke(request);
	}

	public void BroadcastActionResult(MatchActionResult result)
	{
		if(!IsHost || !LobbyLocator.IsValid)
			return;

		MatchRoom room = GetOrCreateRoom(LobbyLocator.id);
		foreach(InMemoryMatchTransport client in room.clients)
			client.OnActionResultReceived?.Invoke(result);
	}

	public void SetConnectionState(NetworkConnectionState state)
	{
		if(ConnectionState == state)
			return;
		ConnectionState = state;
		OnConnectionStateChanged?.Invoke(state);
	}

	void DetachCurrentRoom()
	{
		if(!LobbyLocator.IsValid || !roomById.TryGetValue(LobbyLocator.id, out MatchRoom room))
			return;

		if(room.host == this)
			room.host = null;
		room.clients.Remove(this);
	}

	static MatchRoom GetOrCreateRoom(string lobbyId)
	{
		if(!roomById.TryGetValue(lobbyId, out MatchRoom room))
		{
			room = new MatchRoom();
			roomById.Add(lobbyId, room);
		}
		return room;
	}
}
