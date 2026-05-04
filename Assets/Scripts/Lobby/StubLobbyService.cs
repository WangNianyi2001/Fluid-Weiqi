using System;
using System.Collections.Generic;
using System.Text;

public class StubLobbyService : ILobbyService
{
	static readonly IReadOnlyList<LobbySnapshot> empty = new List<LobbySnapshot>();

	public void CreateLobby(LobbyVisibility visibility, int maxMembers, Action<LobbyLocator> onCreated)
	{
		onCreated?.Invoke(new LobbyLocator(System.Guid.NewGuid().ToString("N")));
	}

	public void QueryLobbies(int offset, int count, string nameFilter, Action<IReadOnlyList<LobbySnapshot>> onResult)
	{
		onResult?.Invoke(empty);
	}

	public void JoinLobby(string lobbyId, Action<JoinLobbyResult> onResult)
	{
		onResult?.Invoke(new JoinLobbyResult { success = false });
	}

	public void JoinLobbyByCode(string invitationCode, Action<JoinLobbyResult> onResult)
	{
		onResult?.Invoke(new JoinLobbyResult { success = false });
	}

	public void RequestInvitationCode(LobbyLocator lobbyLocator, Action<string> onResult)
	{
		const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
		var sb = new StringBuilder(8);
		var rng = new System.Random();
		for(int i = 0; i < 8; ++i)
			sb.Append(chars[rng.Next(chars.Length)]);
		onResult?.Invoke(sb.ToString());
	}
}
