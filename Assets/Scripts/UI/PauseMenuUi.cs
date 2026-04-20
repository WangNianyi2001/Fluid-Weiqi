using UnityEngine;

public class PauseMenuUi : MonoBehaviour
{
	[SerializeField] GameObject pauseMenu;

	protected void Awake()
	{
		pauseMenu.SetActive(false);
	}

	public void OpenPauseMenu()
	{
		pauseMenu.SetActive(true);
		Match.Current.InputEnabled = false;
	}

	public void ClosePauseMenu()
	{
		pauseMenu.SetActive(false);
		Match.Current.InputEnabled = true;
	}

	public void EndMatch()
	{
		GameManager.Instance.EndMatch();
	}
}
