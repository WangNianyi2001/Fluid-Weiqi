using System;

public interface ILobbySyncTransport
{
	event Action<LobbySyncSnapshot> OnSnapshotReceived;
	event Action<PlayerLocator> OnClientDisconnected;

	bool IsHost { get; }
	LobbyLocator LobbyLocator { get; }
	PlayerLocator LocalPlayerLocator { get; }

	void ConfigureAsHost(LobbyLocator lobbyLocator, PlayerLocator hostLocator);
	void ConfigureAsClient(LobbyLocator lobbyLocator, PlayerLocator localPlayerLocator);

	void BroadcastSnapshot(LobbySyncSnapshot snapshot);
	void NotifyClientDisconnected(PlayerLocator playerLocator);
}
