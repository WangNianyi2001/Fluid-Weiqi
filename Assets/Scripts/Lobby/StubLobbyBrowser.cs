using System;
using System.Collections.Generic;

public class StubLobbyBrowser : ILobbyBrowser
{
	static readonly IReadOnlyList<LobbySnapshot> empty = new List<LobbySnapshot>();

	public void QueryLobbies(int offset, int count, string nameFilter, Action<IReadOnlyList<LobbySnapshot>> onResult)
	{
		onResult?.Invoke(empty);
	}

	public void JoinLobby(string lobbyId, Action<JoinLobbyResult> onResult)
	{
		onResult?.Invoke(new JoinLobbyResult
		{
			success = false,
		});
	}
}
