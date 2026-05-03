using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public enum GameScene
{
	StartMenu, Lobby, BrowseLobby, Match
}

public class GameManager : MonoBehaviour
{
	public static GameManager Instance { get; private set; }

	readonly Dictionary<string, MatchModeConfig> matchModeConfigById = new();
	readonly Dictionary<string, AiConfig> aiConfigById = new();
	readonly List<MatchModeConfig> legacyMatchModeConfigs = new();
	readonly List<AiConfig> legacyAiConfigs = new();
	public IReadOnlyList<MatchModeConfig> LegacyMatchModeConfigs => legacyMatchModeConfigs;
	public IReadOnlyList<MatchModeConfig> LoadedMatchModeConfigs => legacyMatchModeConfigs;
	public IReadOnlyList<AiConfig> LegacyAiConfigs => legacyAiConfigs;
	public string DefaultMatchModeId { get; private set; }
	public GameObject DefaultMatchSkinPrefab { get; private set; }

	#region Game initialization
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	static void OnGameInitialize()
	{
		if(!FindAnyObjectByType<GameManager>())
		{
			var go = new GameObject("Game Manager");
			go.AddComponent<GameManager>();
		}
	}
	#endregion

	#region Unity life cycle
	protected void Awake()
	{
		Instance = this;
		DontDestroyOnLoad(gameObject);
		InitializeMatchModeConfigs();

		// Create audio manager
		if(AudioManager.Instance == null)
			gameObject.AddComponent<AudioManager>();
	}

	protected void OnDestroy()
	{
		if(Instance == this)
			Instance = null;
	}
	#endregion

	#region Match modes
	void InitializeMatchModeConfigs()
	{
		matchModeConfigById.Clear();
		aiConfigById.Clear();
		legacyMatchModeConfigs.Clear();
		legacyAiConfigs.Clear();

		GameSettings settings = GameSettings.Instance;
		if(settings == null)
		{
			DefaultMatchModeId = null;
			DefaultMatchSkinPrefab = null;
			return;
		}

		DefaultMatchModeId = settings.DefaultMatchModeId;
		DefaultMatchSkinPrefab = settings.DefaultMatchSkinPrefab;
		if(DefaultMatchSkinPrefab == null)
			Debug.LogError("Default match skin prefab is not configured in GameSettings.");
		for(int i = 0; i < settings.LegacyMatchModes.Count; ++i)
		{
			MatchModeConfig config = settings.LegacyMatchModes[i];
			if(config == null)
				continue;

			if(string.IsNullOrWhiteSpace(config.ModeId))
			{
				Debug.LogError($"Match mode config '{config.name}' has empty mode id.");
				continue;
			}

			if(matchModeConfigById.ContainsKey(config.ModeId))
			{
				Debug.LogError($"Duplicated match mode id '{config.ModeId}'.");
				continue;
			}

			matchModeConfigById.Add(config.ModeId, config);
			legacyMatchModeConfigs.Add(config);
		}

		for(int i = 0; i < settings.LegacyAis.Count; ++i)
		{
			AiConfig config = settings.LegacyAis[i];
			if(config == null)
				continue;

			if(string.IsNullOrWhiteSpace(config.AiId))
			{
				Debug.LogError($"AI config '{config.name}' has empty ai id.");
				continue;
			}
			if(aiConfigById.ContainsKey(config.AiId))
			{
				Debug.LogError($"Duplicated ai id '{config.AiId}'.");
				continue;
			}

			aiConfigById.Add(config.AiId, config);
			legacyAiConfigs.Add(config);
		}

		if(!string.IsNullOrWhiteSpace(DefaultMatchModeId) && !matchModeConfigById.ContainsKey(DefaultMatchModeId))
			Debug.LogError($"Default match mode id '{DefaultMatchModeId}' is not found in GameSettings list.");
	}

	public bool TryGetMatchModeConfig(string modeId, out MatchModeConfig config)
	{
		if(string.IsNullOrWhiteSpace(modeId))
		{
			config = null;
			return false;
		}
		return matchModeConfigById.TryGetValue(modeId, out config);
	}

	public bool TryGetAiConfig(string aiId, out AiConfig config)
	{
		if(string.IsNullOrWhiteSpace(aiId))
		{
			config = null;
			return false;
		}
		return aiConfigById.TryGetValue(aiId, out config);
	}

	public AiConfig FindFirstAiForMode(string modeId)
	{
		for(int i = 0; i < legacyAiConfigs.Count; ++i)
		{
			AiConfig config = legacyAiConfigs[i];
			if(config != null && config.SupportsMode(modeId))
				return config;
		}
		return null;
	}
	#endregion

	#region Lobby
	public Lobby Lobby { get; private set; } = null;
	public ILobbyBrowser LobbyBrowser { get; private set; } = new StubLobbyBrowser();
	public IMatchTransport MatchTransport { get; private set; } = new InMemoryMatchTransport();
	public ILobbySyncTransport LobbySyncTransport { get; private set; } = new InMemoryLobbySyncTransport();

	void ConfigureHostTransports(HostLobby hostLobby)
	{
		if(hostLobby == null)
			return;

		MatchTransport?.ConfigureAsHost(hostLobby.Locator, hostLobby.LocalPlayerLocator);
		LobbySyncTransport?.ConfigureAsHost(hostLobby.Locator, hostLobby.LocalPlayerLocator);
		if(LobbySyncTransport != null)
		{
			LobbySyncTransport.OnClientDisconnected -= hostLobby.NotifyClientDisconnected;
			LobbySyncTransport.OnClientDisconnected += hostLobby.NotifyClientDisconnected;
		}
	}

	void ConfigureClientTransports(ClientLobby clientLobby)
	{
		if(clientLobby == null)
			return;

		MatchTransport?.ConfigureAsClient(clientLobby.Locator, clientLobby.LocalPlayerLocator);
		LobbySyncTransport?.ConfigureAsClient(clientLobby.Locator, clientLobby.LocalPlayerLocator);
		if(LobbySyncTransport != null)
		{
			LobbySyncTransport.OnSnapshotReceived -= clientLobby.ApplySnapshot;
			LobbySyncTransport.OnSnapshotReceived += clientLobby.ApplySnapshot;
		}
	}

	public void LoadDefaultLobby()
	{
		HostLobby hostLobby = new HostLobby(DefaultMatchModeId, new LobbyLocator(System.Guid.NewGuid().ToString("N")));
		Lobby = hostLobby;
		ConfigureHostTransports(hostLobby);
	}

	public void LoadClientLobby(LobbyLocator lobbyLocator, PlayerLocator localPlayerLocator, LobbyVisibility visibility, MatchRule matchRule, IReadOnlyList<PlayerDescriptor> snapshotPlayers)
	{
		ClientLobby clientLobby = new ClientLobby(lobbyLocator, localPlayerLocator, visibility, matchRule, snapshotPlayers);
		Lobby = clientLobby;
		ConfigureClientTransports(clientLobby);
	}

	public void CreateLobby()
	{
		LoadDefaultLobby();
		SwitchScene(GameScene.Lobby);
	}

	public void BrowseLobbies()
	{
		SwitchScene(GameScene.BrowseLobby);
	}

	public void ExitLobby()
	{
		if(Lobby != null && !Lobby.IsHost)
			LobbySyncTransport?.NotifyClientDisconnected(Lobby.LocalPlayerLocator);

		if(LobbySyncTransport != null && Lobby is ClientLobby clientLobby)
			LobbySyncTransport.OnSnapshotReceived -= clientLobby.ApplySnapshot;
		if(LobbySyncTransport != null && Lobby is HostLobby hostLobby)
			LobbySyncTransport.OnClientDisconnected -= hostLobby.NotifyClientDisconnected;

		SwitchScene(GameScene.StartMenu);
		Lobby = null;
	}
	#endregion

	#region Misc
	public void SwitchScene(GameScene scene)
	{
		string sceneName = scene switch
		{
			GameScene.StartMenu => "Start Menu",
			GameScene.Lobby => "Lobby",
			GameScene.BrowseLobby => "Browse Lobby",
			GameScene.Match => "Match",
			_ => throw new System.ArgumentOutOfRangeException()
		};
		SceneManager.LoadScene(sceneName);
	}

	public void QuitGame()
	{
#if UNITY_EDITOR
		if(UnityEditor.EditorApplication.isPlaying)
		{
			UnityEditor.EditorApplication.isPlaying = false;
			return;
		}
#endif
		Application.Quit();
	}
	#endregion
}
