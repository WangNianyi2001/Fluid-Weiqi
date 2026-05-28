using UnityEngine;

[RequireComponent(typeof(UiManager))]
public class StartMenu : MonoBehaviour
{
	public static StartMenu Instance { get; private set; }
	UiManager uiManager;

	[SerializeField] UiPanel mainPanel, lobbyPanel, browseLobbyPanel, preferencesPanel, aboutPanel;
	public UiPanel MainPanel => mainPanel;
	public UiPanel LobbyPanel => lobbyPanel;
	public UiPanel BrowseLobbyPanel => browseLobbyPanel;
	public UiPanel PreferencesPanel => preferencesPanel;
	public UiPanel AboutPanel => aboutPanel;

	protected void Awake()
	{
		uiManager = GetComponent<UiManager>();
		Instance = this;
	}

	public void ReturnToMain()
	{
		SwitchToPanel(mainPanel);
	}

	public void SwitchToPanel(UiPanel panel)
	{
		if(uiManager.CurrentPanel != null && uiManager.CurrentPanel != mainPanel.gameObject)
			uiManager.CloseCurrentPanel();
		if(panel != mainPanel)
			uiManager.OpenPanelFromScene(panel.gameObject);
	}

	public void OnQuitGameButtonClicked()
	{
		GameManager.Instance.QuitGame();
	}
}
