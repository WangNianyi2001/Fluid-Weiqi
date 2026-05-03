using System;
using System.Collections.Generic;

public sealed class JoinLobbyResult
{
	public bool success;
	public LobbyLocator lobbyLocator;
	public PlayerLocator localPlayerLocator;
	public LobbyVisibility visibility;
	public MatchRule matchRule;
	public IReadOnlyList<PlayerDescriptor> players;
}

public interface ILobbyService
{
	void QueryLobbies(int offset, int count, string nameFilter, Action<IReadOnlyList<LobbySnapshot>> onResult);
	void JoinLobby(string lobbyId, Action<JoinLobbyResult> onResult);
	void JoinLobbyByCode(string invitationCode, Action<JoinLobbyResult> onResult);

	// Called by HostLobby when visibility is set to Private.
	// The service (e.g. Steam) generates/registers the code on the backend
	// and returns it via callback. The stub generates one locally.
	void RequestInvitationCode(LobbyLocator lobbyLocator, Action<string> onResult);
}
