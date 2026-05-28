using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public struct UiState
{
	public bool isInstantiated;
	public bool canBeHidden;
	public bool hidePrevious;
	public bool isTemporary;

	public static UiState Default => new()
	{
		isInstantiated = false,
		hidePrevious = true,
		canBeHidden = true,
		isTemporary = false,
	};
}

public class UiManager : MonoBehaviour
{
	public static UiManager Instance { get; private set; }

	readonly Stack<(GameObject, UiState)> panelStack = new();
	public GameObject CurrentPanel => panelStack.Count > 0 ? panelStack.Peek().Item1 : null;

	[SerializeField] List<GameObject> managedPanelsAtStart;
	/// <summary>Scene objects only.</summary>
	[SerializeField] GameObject startPanel;

	protected void Awake()
	{
		Instance = this;
	}

	protected void Start()
	{
		foreach(var panel in managedPanelsAtStart)
			HidePanel(panel);
		if(startPanel)
			OpenPanelFromScene(startPanel);
	}

	protected void OnDestroy()
	{
		Instance = null;
	}

	public void OpenPanel(GameObject newPanel, UiState newState)
	{
		// Close top panels till first non-temporary
		while(panelStack.Count > 0)
		{
			var (panel, state) = panelStack.Peek();
			if(!state.isTemporary)
				break;
			ClosePanel(panel, state);
			panelStack.Pop();
		}

		// Hide current panel
		if(newState.hidePrevious)
		{
			if(panelStack.Count > 0)
			{
				var (panel, state) = panelStack.Peek();
				HidePanel(panel, state);
			}
		}

		// Show new panel
		bool wasActive = newPanel != null && newPanel.activeSelf;
		if(newState.isInstantiated)
			newPanel = Instantiate(newPanel);
		newPanel.SetActive(true);
		panelStack.Push((newPanel, newState));

		// If the panel was already active, OnEnable won't run again.
		if(wasActive)
			newPanel.SendMessage("OnPanelOpened", SendMessageOptions.DontRequireReceiver);
	}

	/// <summary>
	/// Infer params from <c>UiPanel</c> configs.
	/// </summary>
	public void OpenPanel_Auto(GameObject panel)
	{
		OpenPanel(panel, GetUiState(panel));
	}

	public void OpenPanelFromScene(GameObject panel)
	{
		var state = GetUiState(panel);
		state.isInstantiated = false;
		OpenPanel(panel, state);
	}

	public void TogglePanelFromScene(GameObject panel)
	{
		var state = GetUiState(panel);
		state.isInstantiated = false;
		bool isOpen = panel.activeSelf;
		if(!isOpen)
			OpenPanel(panel, state);
		else
		{
			if(CurrentPanel == panel)
				CloseCurrentPanel();
			else
			{
				ClosePanel(panel, state);
				var filtered = panelStack.Where(pair => pair.Item1 != panel).ToArray();
				panelStack.Clear();
				foreach(var pair in filtered)
					panelStack.Push(pair);
			}
		}
	}

	public void OpenPanelFromPrefab(GameObject panel)
	{
		var state = GetUiState(panel);
		state.isInstantiated = true;
		OpenPanel(panel, state);
	}

	public void CloseCurrentPanel()
	{
		// Close current panel
		if(panelStack.Count > 0)
		{
			var (panel, state) = panelStack.Pop();
			ClosePanel(panel, state);
		}

		// Show previous panel
		if(panelStack.Count > 0)
		{
			var (panel, state) = panelStack.Peek();
			if(state.canBeHidden)
				panel.SetActive(true);
		}
	}

	#region Internal
	UiState GetUiState(GameObject panel)
	{
		UiState state = UiState.Default;
		if(panel.TryGetComponent<UiPanel>(out var config))
			state = config.state;
		return state;
	}

	void HidePanel(GameObject panel, UiState? state = null)
	{
		if(state?.canBeHidden ?? true)
			panel.SetActive(false);
	}

	void ClosePanel(GameObject panel, UiState? state = null)
	{
		if(state?.isInstantiated ?? false)
			Destroy(panel);
		else
			panel.SetActive(false);
	}
	#endregion
}
