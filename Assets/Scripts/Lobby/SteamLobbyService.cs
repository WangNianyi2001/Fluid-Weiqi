#if !DISABLESTEAMWORKS
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Steamworks;

/// <summary>
/// Steam-backed implementation of ILobbyService.
///
/// Lobby metadata keys:
///   "name"     – display name (host's Steam persona name)
///   "players"  – current member count (int, as string)
///   "max"      – max member count (int, as string)
///   "code"     – invitation code for private lobbies (only set when Private)
/// </summary>
public class SteamLobbyService : ILobbyService
{
	// Metadata key constants
	const string KeyName    = "name";
	const string KeyPlayers = "players";
	const string KeyMax     = "max";
	const string KeyCode    = "code";

	// -------------------------------------------------------------------------
	// CreateLobby
	// -------------------------------------------------------------------------

	CallResult<LobbyCreated_t> createLobbyResult;

	public void CreateLobby(LobbyVisibility visibility, int maxMembers, Action<LobbyLocator> onCreated)
	{
		ELobbyType lobbyType = visibility == LobbyVisibility.Public
			? ELobbyType.k_ELobbyTypePublic
			: ELobbyType.k_ELobbyTypePrivate;

		SteamAPICall_t call = SteamMatchmaking.CreateLobby(lobbyType, maxMembers);
		createLobbyResult = CallResult<LobbyCreated_t>.Create((result, ioFailure) =>
		{
			if(ioFailure || result.m_eResult != EResult.k_EResultOK)
			{
				Debug.LogError($"[SteamLobbyService] CreateLobby failed: result={result.m_eResult} ioFailure={ioFailure}");
				onCreated?.Invoke(new LobbyLocator());
				return;
			}

			CSteamID steamLobbyId = new CSteamID(result.m_ulSteamIDLobby);
			// Write the host's persona name so clients can display it
			string hostName = SteamFriends.GetPersonaName();
			SteamMatchmaking.SetLobbyData(steamLobbyId, KeyName, hostName);
			SteamMatchmaking.SetLobbyData(steamLobbyId, KeyMax, maxMembers.ToString());

			onCreated?.Invoke(new LobbyLocator(result.m_ulSteamIDLobby.ToString()));
		});
		createLobbyResult.Set(call);
	}

	// -------------------------------------------------------------------------
	// QueryLobbies
	// -------------------------------------------------------------------------

	CallResult<LobbyMatchList_t> lobbyListResult;

	public void QueryLobbies(int offset, int count, string nameFilter, Action<IReadOnlyList<LobbySnapshot>> onResult)
	{
		// Only show public lobbies (no "code" key set)
		SteamMatchmaking.AddRequestLobbyListStringFilter(KeyCode, "", ELobbyComparison.k_ELobbyComparisonEqual);
		SteamMatchmaking.AddRequestLobbyListResultCountFilter(offset + count);

		SteamAPICall_t call = SteamMatchmaking.RequestLobbyList();
		lobbyListResult = CallResult<LobbyMatchList_t>.Create((result, ioFailure) =>
		{
			if(ioFailure)
			{
				Debug.LogError("[SteamLobbyService] QueryLobbies IO failure.");
				onResult?.Invoke(new List<LobbySnapshot>());
				return;
			}

			int total = (int)result.m_nLobbiesMatching;
			var snapshots = new List<LobbySnapshot>();

			for(int i = offset; i < Mathf.Min(total, offset + count); ++i)
			{
				CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
				if(!lobbyId.IsValid())
					continue;

				string lobbyName = SteamMatchmaking.GetLobbyData(lobbyId, KeyName);
				if(!string.IsNullOrEmpty(nameFilter) &&
				   !lobbyName.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
					continue;

				int.TryParse(SteamMatchmaking.GetLobbyData(lobbyId, KeyPlayers), out int currentPlayers);
				int.TryParse(SteamMatchmaking.GetLobbyData(lobbyId, KeyMax),     out int maxPlayers);

				snapshots.Add(new LobbySnapshot
				{
					lobbyId       = lobbyId.m_SteamID.ToString(),
					lobbyName     = lobbyName,
					hostName      = lobbyName,  // host name == lobby name (persona name)
					currentPlayers = currentPlayers,
					maxPlayers    = maxPlayers,
				});
			}

			onResult?.Invoke(snapshots);
		});
		lobbyListResult.Set(call);
	}

	// -------------------------------------------------------------------------
	// JoinLobby
	// -------------------------------------------------------------------------

	CallResult<LobbyEnter_t> joinLobbyResult;

	public void JoinLobby(string lobbyId, Action<JoinLobbyResult> onResult)
	{
		if(!ulong.TryParse(lobbyId, out ulong rawId))
		{
			Debug.LogError($"[SteamLobbyService] JoinLobby: invalid lobbyId '{lobbyId}'");
			onResult?.Invoke(new JoinLobbyResult { success = false });
			return;
		}

		CSteamID steamLobbyId = new CSteamID(rawId);
		SteamAPICall_t call = SteamMatchmaking.JoinLobby(steamLobbyId);
		joinLobbyResult = CallResult<LobbyEnter_t>.Create((result, ioFailure) =>
		{
			bool success = !ioFailure &&
			               result.m_ulSteamIDLobby != 0 &&
			               (EChatRoomEnterResponse)result.m_EChatRoomEnterResponse == EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess;

			if(!success)
			{
				Debug.LogError($"[SteamLobbyService] JoinLobby failed: ioFailure={ioFailure} response={result.m_EChatRoomEnterResponse}");
				onResult?.Invoke(new JoinLobbyResult { success = false });
				return;
			}

			CSteamID joinedLobby = new CSteamID(result.m_ulSteamIDLobby);
			CSteamID localSteamId = SteamUser.GetSteamID();

			onResult?.Invoke(new JoinLobbyResult
			{
				success             = true,
				lobbyLocator        = new LobbyLocator(result.m_ulSteamIDLobby.ToString()),
				localPlayerLocator  = new PlayerLocator(localSteamId.m_SteamID.ToString()),
				visibility          = LobbyVisibility.Public,
				matchRule           = default,
				players             = new List<PlayerDescriptor>(),
			});
		});
		joinLobbyResult.Set(call);
	}

	// -------------------------------------------------------------------------
	// JoinLobbyByCode
	// -------------------------------------------------------------------------

	CallResult<LobbyMatchList_t> joinByCodeListResult;
	CallResult<LobbyEnter_t>     joinByCodeEnterResult;

	public void JoinLobbyByCode(string invitationCode, Action<JoinLobbyResult> onResult)
	{
		SteamMatchmaking.AddRequestLobbyListStringFilter(KeyCode, invitationCode, ELobbyComparison.k_ELobbyComparisonEqual);
		SteamMatchmaking.AddRequestLobbyListResultCountFilter(1);

		SteamAPICall_t call = SteamMatchmaking.RequestLobbyList();
		joinByCodeListResult = CallResult<LobbyMatchList_t>.Create((result, ioFailure) =>
		{
			if(ioFailure || result.m_nLobbiesMatching == 0)
			{
				Debug.LogWarning($"[SteamLobbyService] JoinLobbyByCode: no lobby found for code '{invitationCode}'");
				onResult?.Invoke(new JoinLobbyResult { success = false });
				return;
			}

			CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(0);
			JoinLobby(lobbyId.m_SteamID.ToString(), onResult);
		});
		joinByCodeListResult.Set(call);
	}

	// -------------------------------------------------------------------------
	// RequestInvitationCode
	// -------------------------------------------------------------------------

	public void RequestInvitationCode(LobbyLocator lobbyLocator, Action<string> onResult)
	{
		if(!ulong.TryParse(lobbyLocator.id, out ulong rawId))
		{
			Debug.LogError($"[SteamLobbyService] RequestInvitationCode: invalid locator '{lobbyLocator.id}'");
			onResult?.Invoke(null);
			return;
		}

		// Generate a code and publish it as lobby metadata so JoinLobbyByCode can find it
		const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
		var sb = new StringBuilder(8);
		var rng = new System.Random();
		for(int i = 0; i < 8; ++i)
			sb.Append(chars[rng.Next(chars.Length)]);
		string code = sb.ToString();

		CSteamID steamLobbyId = new CSteamID(rawId);
		SteamMatchmaking.SetLobbyData(steamLobbyId, KeyCode, code);

		onResult?.Invoke(code);
	}

	// -------------------------------------------------------------------------
	// LeaveLobby
	// -------------------------------------------------------------------------

	public void LeaveLobby(LobbyLocator lobbyLocator)
	{
		if(!ulong.TryParse(lobbyLocator.id, out ulong rawId))
			return;
		SteamMatchmaking.LeaveLobby(new CSteamID(rawId));
	}
}
#endif
