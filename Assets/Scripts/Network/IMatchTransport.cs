using System;

public interface IMatchTransport
{
	event Action<MatchActionRequest> OnActionRequestReceived;
	event Action<MatchActionResult> OnActionResultReceived;
	event Action<NetworkConnectionState> OnConnectionStateChanged;

	bool IsHost { get; }
	LobbyLocator LobbyLocator { get; }
	PlayerLocator LocalPlayerLocator { get; }
	NetworkConnectionState ConnectionState { get; }

	void ConfigureAsHost(LobbyLocator lobbyLocator, PlayerLocator hostLocator);
	void ConfigureAsClient(LobbyLocator lobbyLocator, PlayerLocator localPlayerLocator);

	void SendActionRequest(MatchActionRequest request);
	void BroadcastActionResult(MatchActionResult result);

	void SetConnectionState(NetworkConnectionState state);
}
