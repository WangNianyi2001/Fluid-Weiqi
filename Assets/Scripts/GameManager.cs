using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System;
using System.IO;

public enum GameScene
{
	StartMenu, Match
}

public class GameManager : MonoBehaviour
{
	public static GameManager Instance { get; private set; }
	public event Action<PreferencesData> PreferencesChanged;

	readonly Dictionary<string, MatchModeConfig> matchModeConfigById = new();
	readonly Dictionary<string, AiConfig> aiConfigById = new();
	readonly List<MatchModeConfig> legacyMatchModeConfigs = new();
	readonly List<AiConfig> legacyAiConfigs = new();
	public IReadOnlyList<MatchModeConfig> LegacyMatchModeConfigs => legacyMatchModeConfigs;
	public IReadOnlyList<MatchModeConfig> LoadedMatchModeConfigs => legacyMatchModeConfigs;
	public IReadOnlyList<AiConfig> LegacyAiConfigs => legacyAiConfigs;
	public string DefaultMatchModeId { get; private set; }
	public GameObject DefaultMatchSkinPrefab { get; private set; }
	public GameObject DefaultSphericalMatchSkinPrefab { get; private set; }
	public PreferencesData Preferences { get; private set; }
	string preferencesPath;

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
		preferencesPath = Path.Combine(Application.persistentDataPath, "preferences.json");
		LoadPreferences();
		InitializeMatchModeConfigs();

		// Create audio manager
		if(AudioManager.Instance == null)
			gameObject.AddComponent<AudioManager>();

		// Create Steam manager
		if(SteamManager.Instance == null)
			gameObject.AddComponent<SteamManager>();

		// Initialize network services after Steam is ready
		InitializeNetworkServices();
	}

	protected void OnDestroy()
	{
		if(Instance == this)
			Instance = null;
	}
	#endregion

	#region Preferences
	public PreferencesData GetPreferences()
	{
		if(Preferences == null)
			LoadPreferences();
		return Preferences;
	}

	public void SavePreferences()
	{
		if(Preferences == null)
			Preferences = new PreferencesData();

		NormalizePreferences(Preferences);

		try
		{
			string directory = Path.GetDirectoryName(preferencesPath);
			if(!string.IsNullOrEmpty(directory))
				Directory.CreateDirectory(directory);

			string json = JsonUtility.ToJson(Preferences, true);
			File.WriteAllText(preferencesPath, json);
		}
		catch(Exception e)
		{
			Debug.LogWarning($"GameManager: Failed to save preferences to {preferencesPath}. {e.Message}");
		}

		PreferencesChanged?.Invoke(Preferences);
	}

	void LoadPreferences()
	{
		PreferencesData loaded = null;

		if(File.Exists(preferencesPath))
		{
			try
			{
				string json = File.ReadAllText(preferencesPath);
				if(!string.IsNullOrWhiteSpace(json))
					loaded = JsonUtility.FromJson<PreferencesData>(json);
			}
			catch(Exception e)
			{
				Debug.LogWarning($"GameManager: Failed to load preferences from {preferencesPath}. {e.Message}");
			}
		}

		Preferences = loaded ?? new PreferencesData();
		NormalizePreferences(Preferences);
		PreferencesChanged?.Invoke(Preferences);

		if(loaded == null)
			SavePreferences();
	}

	void NormalizePreferences(PreferencesData data)
	{
		if(data == null)
			return;

		data.volume = Mathf.Clamp01(data.volume);
		if(data.languageIndex < 0)
			data.languageIndex = 0;
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
			DefaultSphericalMatchSkinPrefab = null;
			return;
		}

		DefaultMatchModeId = settings.DefaultMatchModeId;
		DefaultMatchSkinPrefab = settings.DefaultMatchSkinPrefab;
		DefaultSphericalMatchSkinPrefab = settings.DefaultSphericalMatchSkinPrefab;
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

#if !DISABLESTEAMWORKS
	static bool SteamAvailable => SteamManager.Initialized;
#else
	static bool SteamAvailable => false;
#endif

	public ILobbyService LobbyService { get; private set; }
	public IMatchTransport MatchTransport { get; private set; }
	public ILobbySyncTransport LobbySyncTransport { get; private set; }

	void InitializeNetworkServices()
	{
#if !DISABLESTEAMWORKS
		if(SteamAvailable)
		{
			LobbyService      = new SteamLobbyService();
			MatchTransport    = new SteamMatchTransport();
			LobbySyncTransport = new SteamLobbySyncTransport();
			return;
		}
#endif
		LobbyService       = new StubLobbyService();
		MatchTransport     = new InMemoryMatchTransport();
		LobbySyncTransport = new InMemoryLobbySyncTransport();
	}

	void ConfigureHostTransports(HostLobby hostLobby)
	{
		if(hostLobby == null)
			return;

		MatchTransport?.ConfigureAsHost(hostLobby.Locator, hostLobby.LocalPlayerLocator);
		LobbySyncTransport?.ConfigureAsHost(hostLobby.Locator, hostLobby.LocalPlayerLocator);
		if(LobbySyncTransport != null)
		{
			LobbySyncTransport.OnClientConnected -= hostLobby.NotifyClientConnected;
			LobbySyncTransport.OnClientConnected += hostLobby.NotifyClientConnected;
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
			LobbySyncTransport.OnLobbyClosed -= clientLobby.NotifyLobbyDismissed;
			LobbySyncTransport.OnLobbyClosed += clientLobby.NotifyLobbyDismissed;
		}
	}

	public void LoadDefaultLobby()
	{
		LoadDefaultLobby(null);
	}

	void LoadDefaultLobby(System.Action<HostLobby> onLoaded)
	{
		LobbyService.CreateLobby(LobbyVisibility.Local, 4, locator =>
		{
			if(!locator.IsValid)
			{
				Debug.LogError("Failed to create lobby: received invalid lobby locator.");
				return;
			}

			HostLobby hostLobby = new HostLobby(DefaultMatchModeId, locator);
			Lobby = hostLobby;
			ConfigureHostTransports(hostLobby);
			onLoaded?.Invoke(hostLobby);
		});
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
	}

	public void ExitLobby()
	{
		if(Lobby != null && !Lobby.IsHost)
			LobbySyncTransport?.NotifyClientDisconnected(Lobby.LocalPlayerLocator);

		if(LobbySyncTransport != null && Lobby is ClientLobby clientLobby)
		{
			LobbySyncTransport.OnSnapshotReceived -= clientLobby.ApplySnapshot;
			LobbySyncTransport.OnLobbyClosed -= clientLobby.NotifyLobbyDismissed;
		}
		if(LobbySyncTransport != null && Lobby is HostLobby hostLobby)
			LobbySyncTransport.OnClientDisconnected -= hostLobby.NotifyClientDisconnected;

		// Leave the Steam lobby so the host receives LobbyChatUpdate_t immediately.
		if(Lobby != null)
			LobbyService?.LeaveLobby(Lobby.Locator);

		if(IsMatchSceneActive())
			SwitchScene(GameScene.StartMenu);
		Lobby = null;
	}
	#endregion

	bool IsStartMenuSceneActive()
	{
		return SceneManager.GetActiveScene().name == "Start Menu";
	}

	bool IsMatchSceneActive()
	{
		return SceneManager.GetActiveScene().name == "Match";
	}

	#region Misc
	public void SwitchScene(GameScene scene)
	{
		string sceneName = scene switch
		{
			GameScene.StartMenu => "Start Menu",
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
