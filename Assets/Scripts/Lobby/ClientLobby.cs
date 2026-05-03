using UnityEngine;
using System;
using System.Collections.Generic;

public class ClientLobby : Lobby
{
	readonly List<PlayerDescriptor> players = new();
	readonly LobbyLocator locator;
	readonly PlayerLocator localPlayerLocator;

	LobbyVisibility visibility;
	MatchRule matchRule;
	string invitationCode;

	public override LobbyLocator Locator => locator;
	public override LobbyVisibility Visibility => visibility;
	public override List<PlayerDescriptor> Players => players;
	public override MatchRule MatchRule => matchRule;
	public override PlayerLocator LocalPlayerLocator => localPlayerLocator;

	public ClientLobby(LobbyLocator locator, PlayerLocator localPlayerLocator, LobbyVisibility visibility, MatchRule matchRule, IReadOnlyList<PlayerDescriptor> initialPlayers)
	{
		this.locator = locator;
		this.localPlayerLocator = localPlayerLocator;
		this.visibility = visibility;
		this.matchRule = matchRule;
		ReplacePlayers(initialPlayers);
	}

	public void ApplySnapshot(LobbySyncSnapshot snapshot)
	{
		if(snapshot == null)
			return;
		if(Locator.IsValid && snapshot.lobbyLocator.IsValid && Locator.id != snapshot.lobbyLocator.id)
			return;

		ApplySnapshot(snapshot.visibility, snapshot.matchRule, NetworkSnapshotUtility.ToPlayerDescriptors(snapshot.players), snapshot.invitationCode);
	}

	public override string GetInvitationCode() =>
		Visibility == LobbyVisibility.Private ? invitationCode : null;

	public void ApplySnapshot(LobbyVisibility newVisibility, MatchRule newMatchRule, IReadOnlyList<PlayerDescriptor> snapshotPlayers, string newInvitationCode = null)
	{
		bool changedVisibility = visibility != newVisibility;
		visibility = newVisibility;
		if(changedVisibility)
			OnVisibilityChanged?.Invoke();

		matchRule = newMatchRule;
		OnMatchRuleChanged?.Invoke();

		invitationCode = newInvitationCode;
		ReplacePlayers(snapshotPlayers);
	}

	void ReplacePlayers(IReadOnlyList<PlayerDescriptor> source)
	{
		players.Clear();
		if(source != null)
			players.AddRange(source);
		OnPlayersChanged?.Invoke();
	}

	public void NotifyLobbyDismissed()
	{
		OnDismissed?.Invoke();
	}

	public void NotifyMatchStarting()
	{
		OnStartingMatch?.Invoke();
	}

	public void NotifyMatchEnded()
	{
		OnMatchEnded?.Invoke();
	}
}