using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class LobbyPlayerSlot : MonoBehaviour
{
	protected void OnDestroy()
	{
		if(Lobby.Current != null)
			Lobby.Current.OnMatchRuleChanged -= OnMatchRuleChangedRefreshAi;
	}
	static GameObject prefab;

	protected void Awake()
	{
		prefab ??= Resources.Load<GameObject>("UI/Lobby/Player Slot");
	}

	public static LobbyPlayerSlot Make(PlayerDescriptor descriptor, Transform root = null)
	{
		var go = Instantiate(prefab, root);
		var slot = go.GetComponent<LobbyPlayerSlot>();
		slot.Descriptor = descriptor;
		return slot;
	}

	public PlayerDescriptor Descriptor { get; set; }
	int Index => Descriptor?.Index ?? -1;
	readonly List<PlayerType> typeOptions = new() { PlayerType.Local, PlayerType.Ai };

	[SerializeField] Graphic colorGraphic;
	[SerializeField] Dropdown typeDropdown;
	[SerializeField] Dropdown colorDropdown;
	[SerializeField] Text nameText;
	[SerializeField] Dropdown aiDropdown;
	[SerializeField] Button removeButton;

	readonly List<AiConfig> aiOptions = new();
	readonly List<PlayerColorOption> colorOptions = new();

	protected void Start()
	{
		if(Lobby.Current == null || Descriptor == null)
		{
			Destroy(this);
			return;
		}

		if(Lobby.Current.IsOnline)
			typeOptions.Add(PlayerType.Online);
		typeDropdown.options = typeOptions.Select(t => new Dropdown.OptionData(t.ToLocalizedString())).ToList();
		typeDropdown.value = typeOptions.IndexOf(Descriptor.type);

		RefreshColorDropdown();
		colorGraphic.color = Descriptor.color;

		bool isAi = Descriptor.type == PlayerType.Ai;
		nameText.gameObject.SetActive(!isAi);
		aiDropdown.gameObject.SetActive(isAi);
		if(!isAi)
			nameText.text = Descriptor.GetLocalizedName();
		else
			RefreshAiDropdown();

		if(Lobby.Current.Players.Count <= 2)
			removeButton.interactable = false;
		if(Descriptor.type == PlayerType.Local && Lobby.Current.Players.Count(p => p.type == PlayerType.Local) < 2)
			removeButton.interactable = false;

		if(!Lobby.Current.IsHost)
		{
			typeDropdown.interactable = false;
			colorDropdown.interactable = false;
			aiDropdown.interactable = false;
			removeButton.gameObject.SetActive(false);
		}
		else
		{
			typeDropdown.onValueChanged.AddListener(OnTypeDropdownValueChanged);
			colorDropdown.onValueChanged.AddListener(OnColorDropdownValueChanged);
			aiDropdown.onValueChanged.AddListener(OnAiDropdownValueChanged);
			Lobby.Current.OnMatchRuleChanged += OnMatchRuleChangedRefreshAi;
		}
	}

	void RefreshAiDropdown()
	{
		aiOptions.Clear();
		string modeId = Lobby.Current?.MatchRule.modeId;
		if(GameManager.Instance != null && !string.IsNullOrWhiteSpace(modeId))
		{
			foreach(AiConfig config in GameManager.Instance.LegacyAiConfigs)
			{
				if(config != null && config.SupportsMode(modeId))
					aiOptions.Add(config);
			}
		}

		aiDropdown.options = aiOptions.Select(a => new Dropdown.OptionData(a.AiName)).ToList();
		int selected = string.IsNullOrWhiteSpace(Descriptor?.aiId)
			? 0
			: Mathf.Max(0, aiOptions.FindIndex(a => a.AiId == Descriptor.aiId));
		aiDropdown.SetValueWithoutNotify(selected);
	}

	void RefreshColorDropdown()
	{
		colorOptions.Clear();
		if(GameSettings.Instance != null)
		{
			foreach(PlayerColorOption option in GameSettings.Instance.AvailablePlayerColors)
				colorOptions.Add(option);
		}

		colorDropdown.options = colorOptions.Select(c => new Dropdown.OptionData(c.name)).ToList();
		int selected = Mathf.Clamp(Descriptor?.colorIndex ?? 0, 0, Mathf.Max(0, colorOptions.Count - 1));
		colorDropdown.SetValueWithoutNotify(selected);
	}

	void OnMatchRuleChangedRefreshAi()
	{
		if(Descriptor?.type == PlayerType.Ai)
			RefreshAiDropdown();
	}

	void OnColorDropdownValueChanged(int i)
	{
		if(!colorOptions.IsValidIndex(i) || Descriptor == null)
			return;
		
		Descriptor.colorIndex = i;
		colorGraphic.color = Descriptor.color;
		HostLobby.Current?.SetPlayerColor(Index, i);
	}

	void OnAiDropdownValueChanged(int i)
	{
		if(!aiOptions.IsValidIndex(i))
			return;
		HostLobby.Current?.SetPlayerAi(Index, aiOptions[i].AiId);
	}

	void OnTypeDropdownValueChanged(int i)
	{
		HostLobby.Current?.SetPlayerType(Index, typeOptions[i]);
	}

	public void OnRemoveButtonClicked()
	{
		HostLobby.Current?.RemovePlayer(Index);
	}
}
