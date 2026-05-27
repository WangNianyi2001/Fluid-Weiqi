#if !DISABLESTEAMWORKS
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Steamworks;

/// <summary>
/// Steam-backed implementation of IMatchTransport.
///
/// Uses ISteamNetworkingMessages (session-less P2P) for all match action
/// traffic. Two channels are used so that requests and results can be
/// distinguished without embedding a type tag in the payload:
///   Channel 0 – MatchActionRequest  (client → host)
///   Channel 1 – MatchActionResult   (host → clients)
///
/// The host sends action results to every known lobby member (except itself).
/// The member list is refreshed from the Steam Lobby on each broadcast.
///
/// Receiving is polled in Update() via a MonoBehaviour runner that is
/// created automatically and kept alive for the lifetime of this transport.
/// </summary>
public class SteamMatchTransport : IMatchTransport
{
	const int ChannelRequest = 0;
	const int ChannelResult  = 1;
	const int MaxMessagesPerPoll = 32;
	const int SendFlagsReliable = Constants.k_nSteamNetworkingSend_Reliable;

	// -------------------------------------------------------------------------
	// IMatchTransport
	// -------------------------------------------------------------------------

	public event Action<MatchActionRequest> OnActionRequestReceived;
	public event Action<MatchActionResult>  OnActionResultReceived;
	public event Action<NetworkConnectionState> OnConnectionStateChanged;

	public bool IsHost { get; private set; }
	public LobbyLocator LobbyLocator { get; private set; }
	public PlayerLocator LocalPlayerLocator { get; private set; }
	public NetworkConnectionState ConnectionState { get; private set; } = NetworkConnectionState.Connected;

	// -------------------------------------------------------------------------
	// Internal state
	// -------------------------------------------------------------------------

	Callback<SteamNetworkingMessagesSessionFailed_t>  cbSessionFailed;
	Callback<SteamNetworkingMessagesSessionRequest_t> cbSessionRequest;
	MatchTransportRunner runner;

	// -------------------------------------------------------------------------
	// Configure
	// -------------------------------------------------------------------------

	public void ConfigureAsHost(LobbyLocator lobbyLocator, PlayerLocator hostLocator)
	{
		Detach();
		IsHost = true;
		LobbyLocator = lobbyLocator;
		LocalPlayerLocator = hostLocator;
		Attach();
	}

	public void ConfigureAsClient(LobbyLocator lobbyLocator, PlayerLocator localPlayerLocator)
	{
		Detach();
		IsHost = false;
		LobbyLocator = lobbyLocator;
		LocalPlayerLocator = localPlayerLocator;
		Attach();
	}

	// -------------------------------------------------------------------------
	// Send (client → host)
	// -------------------------------------------------------------------------

	public void SendActionRequest(MatchActionRequest request)
	{
		if(IsHost)
			return;
		if(!ulong.TryParse(LobbyLocator.id, out ulong lobbyRawId))
			return;

		// The host's locator id is set to "host" in InMemory mode but in Steam mode
		// we need the host's SteamID. Retrieve it from the lobby owner.
		CSteamID hostSteamId = SteamMatchmaking.GetLobbyOwner(new CSteamID(lobbyRawId));
		if(!hostSteamId.IsValid())
			return;

		byte[] data = NetworkSerializer.SerializeMatchActionRequest(request);
		SendTo(hostSteamId, data, ChannelRequest);
	}

	public void SendActionResult(MatchActionResult result, PlayerLocator targetPlayerLocator)
	{
		if(!IsHost || !targetPlayerLocator.IsValid)
			return;
		if(!ulong.TryParse(targetPlayerLocator.id, out ulong targetRawId))
			return;

		byte[] data = NetworkSerializer.SerializeMatchActionResult(result);
		SendTo(new CSteamID(targetRawId), data, ChannelResult);
	}

	// -------------------------------------------------------------------------
	// Broadcast (host → all lobby members except self)
	// -------------------------------------------------------------------------

	public void BroadcastActionResult(MatchActionResult result, PlayerLocator excludedPlayerLocator)
	{
		if(!IsHost)
			return;
		if(!ulong.TryParse(LobbyLocator.id, out ulong lobbyRawId))
			return;

		byte[] data = NetworkSerializer.SerializeMatchActionResult(result);
		CSteamID steamLobbyId = new CSteamID(lobbyRawId);
		CSteamID selfId = SteamUser.GetSteamID();
		CSteamID excludedId = default;
		ulong excludedRawId = 0;
		bool hasExcluded = excludedPlayerLocator.IsValid && ulong.TryParse(excludedPlayerLocator.id, out excludedRawId);
		if(hasExcluded)
			excludedId = new CSteamID(excludedRawId);

		int memberCount = SteamMatchmaking.GetNumLobbyMembers(steamLobbyId);
		for(int i = 0; i < memberCount; ++i)
		{
			CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(steamLobbyId, i);
			if(member == selfId)
				continue;
			if(hasExcluded && member == excludedId)
				continue;
			SendTo(member, data, ChannelResult);
		}
	}

	// -------------------------------------------------------------------------
	// Connection state
	// -------------------------------------------------------------------------

	public void SetConnectionState(NetworkConnectionState state)
	{
		if(ConnectionState == state)
			return;
		ConnectionState = state;
		OnConnectionStateChanged?.Invoke(state);
	}

	// -------------------------------------------------------------------------
	// Polling – called every frame by the runner MonoBehaviour
	// -------------------------------------------------------------------------

	internal void Poll()
	{
		if(IsHost)
			PollChannel(ChannelRequest, DispatchRequest);
		else
			PollChannel(ChannelResult, DispatchResult);
	}

	void PollChannel(int channel, Action<IntPtr[], int> dispatcher)
	{
		IntPtr[] msgs = new IntPtr[MaxMessagesPerPoll];
		int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(channel, msgs, MaxMessagesPerPoll);
		if(count > 0)
			dispatcher(msgs, count);
	}

	void DispatchRequest(IntPtr[] msgs, int count)
	{
		for(int i = 0; i < count; ++i)
		{
			SteamNetworkingMessage_t msg = Marshal.PtrToStructure<SteamNetworkingMessage_t>(msgs[i]);
			try
			{
				byte[] buf = new byte[msg.m_cbSize];
				Marshal.Copy(msg.m_pData, buf, 0, msg.m_cbSize);
				MatchActionRequest request = NetworkSerializer.DeserializeMatchActionRequest(buf, msg.m_cbSize);
				OnActionRequestReceived?.Invoke(request);
			}
			catch(Exception ex)
			{
				Debug.LogError($"[SteamMatchTransport] Failed to deserialize MatchActionRequest: {ex.Message}");
			}
			finally
			{
				SteamNetworkingMessage_t.Release(msgs[i]);
			}
		}
	}

	void DispatchResult(IntPtr[] msgs, int count)
	{
		for(int i = 0; i < count; ++i)
		{
			SteamNetworkingMessage_t msg = Marshal.PtrToStructure<SteamNetworkingMessage_t>(msgs[i]);
			try
			{
				byte[] buf = new byte[msg.m_cbSize];
				Marshal.Copy(msg.m_pData, buf, 0, msg.m_cbSize);
				MatchActionResult result = NetworkSerializer.DeserializeMatchActionResult(buf, msg.m_cbSize);
				OnActionResultReceived?.Invoke(result);
			}
			catch(Exception ex)
			{
				Debug.LogError($"[SteamMatchTransport] Failed to deserialize MatchActionResult: {ex.Message}");
			}
			finally
			{
				SteamNetworkingMessage_t.Release(msgs[i]);
			}
		}
	}

	// -------------------------------------------------------------------------
	// Helpers
	// -------------------------------------------------------------------------

	static void SendTo(CSteamID target, byte[] data, int channel)
	{
		var identity = new SteamNetworkingIdentity();
		identity.SetSteamID(target);

		IntPtr ptr = Marshal.AllocHGlobal(data.Length);
		try
		{
			Marshal.Copy(data, 0, ptr, data.Length);
			EResult res = SteamNetworkingMessages.SendMessageToUser(
				ref identity, ptr, (uint)data.Length, SendFlagsReliable, channel);

			if(res != EResult.k_EResultOK)
				Debug.LogWarning($"[SteamMatchTransport] SendMessageToUser to {target} returned {res}");
		}
		finally
		{
			Marshal.FreeHGlobal(ptr);
		}
	}

	void Attach()
	{
		cbSessionFailed  = Callback<SteamNetworkingMessagesSessionFailed_t>.Create(OnSessionFailed);
		cbSessionRequest = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnSessionRequest);

		// Create a hidden MonoBehaviour to drive Poll() each frame
		var go = new GameObject("[SteamMatchTransportRunner]") { hideFlags = HideFlags.HideAndDontSave };
		UnityEngine.Object.DontDestroyOnLoad(go);
		runner = go.AddComponent<MatchTransportRunner>();
		runner.transport = this;
	}

	void Detach()
	{
		cbSessionFailed?.Dispose();
		cbSessionFailed = null;
		cbSessionRequest?.Dispose();
		cbSessionRequest = null;

		if(runner != null)
		{
			UnityEngine.Object.Destroy(runner.gameObject);
			runner = null;
		}
	}

	void OnSessionFailed(SteamNetworkingMessagesSessionFailed_t data)
	{
		Debug.LogWarning($"[SteamMatchTransport] Session failed with {data.m_info.m_identityRemote.GetSteamID()}");
		SetConnectionState(NetworkConnectionState.Disconnected);
	}

	void OnSessionRequest(SteamNetworkingMessagesSessionRequest_t data)
	{
		// Auto-accept every incoming P2P session so messages can flow in both directions.
		SteamNetworkingMessages.AcceptSessionWithUser(ref data.m_identityRemote);
	}

	// -------------------------------------------------------------------------
	// Runner MonoBehaviour
	// -------------------------------------------------------------------------

	sealed class MatchTransportRunner : MonoBehaviour
	{
		internal SteamMatchTransport transport;

		void Update()
		{
			transport?.Poll();
		}
	}
}
#endif
