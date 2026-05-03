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

public interface ILobbyBrowser
{
	void QueryLobbies(int offset, int count, string nameFilter, Action<IReadOnlyList<LobbySnapshot>> onResult);
	void JoinLobby(string lobbyId, Action<JoinLobbyResult> onResult);
}
