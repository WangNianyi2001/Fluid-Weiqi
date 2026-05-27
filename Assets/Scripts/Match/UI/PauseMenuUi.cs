using UnityEngine;

public class PauseMenuUi : MonoBehaviour
{
	[SerializeField] GameObject pauseMenu;
	TraditionalMatchEndingUi endingUi;

	protected void Awake()
	{
		if(pauseMenu != null)
			pauseMenu.SetActive(false);

		if(Lobby.Current != null)
		{
			Lobby.Current.OnMatchEnded += OnLobbyMatchEnded;
			Lobby.Current.OnDismissed += OnLobbyDismissed;
		}
	}

	protected void OnDestroy()
	{
		if(Lobby.Current != null)
		{
			Lobby.Current.OnMatchEnded -= OnLobbyMatchEnded;
			Lobby.Current.OnDismissed -= OnLobbyDismissed;
		}
	}

	public void OpenPauseMenu()
	{
		if(pauseMenu != null)
			pauseMenu.SetActive(true);
		if(Match.Current != null)
			Match.Current.InputEnabled = false;
	}

	public void ClosePauseMenu()
	{
		if(pauseMenu != null)
			pauseMenu.SetActive(false);
		if(Match.Current != null)
			Match.Current.InputEnabled = true;
	}

	public void EndMatch()
	{
		ClosePauseMenu();

		if(Lobby.Current == null || !Lobby.Current.IsOnline)
		{
			GameManager.Instance?.SwitchScene(GameScene.StartMenu);
			return;
		}

		if(Lobby.Current.IsHost)
		{
			HostLobby.Current?.EndMatch();
			return;
		}

		GameManager.Instance?.ExitLobby();
	}

	void OnLobbyMatchEnded()
	{
		if(GameManager.Instance == null)
			return;

		ClosePauseMenu();

		if(Lobby.Current == null || Lobby.Current.IsHost)
		{
			GameManager.Instance.SwitchScene(GameScene.Lobby);
			return;
		}

		if(Match.Current != null)
			Match.Current.InputEnabled = false;

		TraditionalMatchEndingUi ui = ResolveEndingUi();
		if(ui != null)
		{
			if(Lobby.Current.LastMatchEndReason == LobbyMatchEndReason.ConnectionLost)
				ui.ShowMessage("网络连接已断开", GameScene.StartMenu);
			else
				ui.ShowMessage("房主已结束对局", GameScene.Lobby);
			return;
		}

		if(Lobby.Current.LastMatchEndReason == LobbyMatchEndReason.ConnectionLost)
			GameManager.Instance.SwitchScene(GameScene.StartMenu);
		else
			GameManager.Instance.SwitchScene(GameScene.Lobby);
	}

	TraditionalMatchEndingUi ResolveEndingUi()
	{
		if(endingUi != null)
			return endingUi;

		Canvas canvas = GetComponentInParent<Canvas>(true);
		if(canvas != null)
			endingUi = canvas.GetComponentInChildren<TraditionalMatchEndingUi>(true);

		if(endingUi == null)
			endingUi = FindObjectOfType<TraditionalMatchEndingUi>(true);

		return endingUi;
	}

	void OnLobbyDismissed()
	{
		if(GameManager.Instance == null)
			return;
		ClosePauseMenu();
		GameManager.Instance.SwitchScene(GameScene.StartMenu);
	}
}
