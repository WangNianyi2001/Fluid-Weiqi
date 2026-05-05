using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class LobbyUi : MonoBehaviour
{
	#region Unity life cycle
	protected void Start()
	{
#if DEBUG && UNITY_EDITOR
		// Debug
		if(Lobby.Current == null)
		{
			Debug.LogWarning("Creating debug lobby.");
			GameManager.Instance.CreateLobby();
			return;
		}
#endif

		if(Lobby.Current == null)
		{
			Debug.LogError("No lobby present.");
			return;
		}

		Lobby.Current.OnDismissed += OnLobbyDismissed;
		Lobby.Current.OnStartingMatch += OnStartingMatch;

		// Lobby settings
		SetVisibilityOptions(allVisibilityOptions);
		visibilityDropdown.interactable = Lobby.Current.IsHost;
		visibilityDropdown.onValueChanged.AddListener(OnVisibilityDropdownValueChanged);
		Lobby.Current.OnVisibilityChanged += OnVisibilityChanged;
		RefreshLobbySettingsUi();

		// Player settings
		Lobby.Current.OnPlayersChanged += OnPlayersChanged;
		ReconstructPlayerSlots();

		// Match rule
		SetMatchModeOptions(GameManager.Instance != null ? GameManager.Instance.LegacyMatchModeConfigs : new List<MatchModeConfig>());
		matchModeDropdown.onValueChanged.AddListener(OnMatchModeDropdownValueChanged);
		boardSizeSlider.onValueChanged.AddListener(OnBoardSizeSliderValueChanged);
		stoneHardnessSlider.onValueChanged.AddListener(OnStoneHardnessSliderValueChanged);
		Lobby.Current.OnMatchRuleChanged += OnMatchRuleChanged;
		RefreshMatchRuleArea();

		// Footer
		startButton.gameObject.SetActive(Lobby.Current.IsHost);
		startButton.interactable = Lobby.Current.IsHost;
		RefreshFooterArea();
	}

	protected void OnDestroy()
	{
		if(Lobby.Current != null)
		{
			Lobby.Current.OnDismissed -= OnLobbyDismissed;
			Lobby.Current.OnStartingMatch -= OnStartingMatch;
			Lobby.Current.OnVisibilityChanged -= OnVisibilityChanged;
			Lobby.Current.OnPlayersChanged -= OnPlayersChanged;
			Lobby.Current.OnMatchRuleChanged -= OnMatchRuleChanged;
		}
	}
	#endregion

	#region Life cycle
	public void OnCloseButtonClicked()
	{
		if(Lobby.Current?.IsHost ?? true)
			HostLobby.Current?.Dismiss();
		else
			LeaveLobby();
	}

	void ReturnToStartMenu()
	{
		GameManager.Instance.SwitchScene(GameScene.StartMenu);
	}

	void LeaveLobby()
	{
		GameManager.Instance.ExitLobby();
	}

	void OnLobbyDismissed()
	{
		// TODO: Show message
		ReturnToStartMenu();
	}

	void OnStartingMatch()
	{
		GameManager.Instance.SwitchScene(GameScene.Match);
	}
	#endregion

	#region Lobby settings
	[Header("Lobby Settings")]
	[SerializeField] Dropdown visibilityDropdown;

	static readonly LobbyVisibility[] allVisibilityOptions = new LobbyVisibility[]
	{
		LobbyVisibility.Local,
		LobbyVisibility.Private,
		LobbyVisibility.Public,
	};

	public LobbyVisibility Visibility => visibilityOptions[visibilityDropdown.value];
	readonly List<LobbyVisibility> visibilityOptions = new();

	void SetVisibilityOptions(params LobbyVisibility[] value) => SetVisibilityOptions(value as IList<LobbyVisibility>);
	void SetVisibilityOptions(IList<LobbyVisibility> value)
	{
		visibilityOptions.Clear();
		visibilityOptions.AddRange(value);

		// Refresh dropdown
		visibilityDropdown.options = visibilityOptions
			.Select((LobbyVisibility v, int i) => new Dropdown.OptionData(v.ToLocalizedString()))
			.ToList();
		if(visibilityDropdown.value >= visibilityOptions.Count)
			visibilityDropdown.value = 0;
	}

	public void OnVisibilityDropdownValueChanged(int index)
	{
		HostLobby.Current?.SetVisibility(visibilityOptions[visibilityDropdown.value]);
	}

	void OnVisibilityChanged()
	{
		RefreshLobbySettingsUi();
		ReconstructPlayerSlots();
	}

	void RefreshLobbySettingsUi()
	{
		invitationCodeRow.SetActive(Lobby.Current.Visibility == LobbyVisibility.Private);
		invitationCodeText.text = Lobby.Current?.GetInvitationCode();
	}

	[SerializeField] GameObject invitationCodeRow;
	[SerializeField] Text invitationCodeText;
	#endregion

	#region Player settings
	[Header("Player settings")]
	[SerializeField] Transform playerSlotList;

	[SerializeField] Button addPlayerButton;

	void OnPlayersChanged()
	{
		ReconstructPlayerSlots();
		RefreshFooterArea();
	}

	void ReconstructPlayerSlots()
	{
		GameUtility.ClearChildren(playerSlotList);

		foreach(var player in Lobby.Current.Players)
			LobbyPlayerSlot.Make(player, playerSlotList);

		addPlayerButton.interactable = Lobby.Current.IsHost && Lobby.Current.Players.Count < 4;
	}

	public void OnAddPlayerButtonClicked()
	{
		HostLobby.Current?.AddPlayer();
	}
	#endregion

	#region Match rule
	[Header("Match Rule")]
	[SerializeField] Dropdown matchModeDropdown;
	[SerializeField] Text boardSizeText;
	[SerializeField] Slider boardSizeSlider;
	[SerializeField] Text stoneHardnessText;
	[SerializeField] Slider stoneHardnessSlider;

	readonly List<MatchModeConfig> matchModeOptions = new();
	void SetMatchModeOptions(IReadOnlyList<MatchModeConfig> options)
	{
		matchModeOptions.Clear();
		matchModeOptions.AddRange(options.Where(o => o != null));

		matchModeDropdown.options = matchModeOptions
			.Select(m => new Dropdown.OptionData(m.DisplayName))
			.ToList();

		int index = matchModeOptions.FindIndex(m => m.ModeId == Lobby.Current.MatchRule.modeId);
		if(index == -1)
		{
			if(matchModeOptions.Count == 0)
			{
				matchModeDropdown.value = 0;
				return;
			}
			index = 0;
		}

		matchModeDropdown.value = index;
	}

	void OnMatchModeDropdownValueChanged(int index)
	{
		if(!matchModeOptions.IsValidIndex(index))
			return;

		var rule = Lobby.Current.MatchRule;
		rule.modeId = matchModeOptions[index].ModeId;
		HostLobby.Current?.SetMatchRule(rule);
	}

	void OnBoardSizeSliderValueChanged(float value)
	{
		var rule = Lobby.Current.MatchRule;
		rule.boardSize = Mathf.RoundToInt(value);
		HostLobby.Current?.SetMatchRule(rule);
	}

	void OnStoneHardnessSliderValueChanged(float value)
	{
		var rule = Lobby.Current.MatchRule;
		rule.stoneHardness = value;
		HostLobby.Current?.SetMatchRule(rule);
	}

	void OnMatchRuleChanged()
	{
		RefreshMatchRuleArea();
		RefreshFooterArea();
	}

	void RefreshMatchRuleArea()
	{
		var rule = Lobby.Current.MatchRule;
		boardSizeText.text = rule.boardSize.ToString();
		boardSizeSlider.value = rule.boardSize;
		stoneHardnessText.text = rule.stoneHardness.ToString("F2");
		stoneHardnessSlider.value = rule.stoneHardness;
	}
	#endregion

	#region Footer
	[Header("Footer")]
	[SerializeField] Button startButton;
	[SerializeField] Text errorText;

	void RefreshFooterArea()
	{
		if(!Lobby.Current.IsHost)
		{
			errorText.gameObject.SetActive(false);
			return;
		}

		bool valid = Lobby.Current.ValidateStartingCondition(out string errorMessage);
		startButton.interactable = valid;
		errorText.gameObject.SetActive(!valid);
		errorText.text = errorMessage;
	}

	public void OnStartButtonClicked()
	{
		HostLobby.Current?.StartMatch();
	}
	#endregion
}
