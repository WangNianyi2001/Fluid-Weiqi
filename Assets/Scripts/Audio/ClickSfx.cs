using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach this to a Button component to play click sound on click.
/// Automatically hooks into OnClick event.
/// </summary>
public class ClickSfx : MonoBehaviour
{
	protected void OnEnable()
	{
		Button button = GetComponent<Button>();
		if(button == null)
		{
			Debug.LogWarning("ClickSfx must be attached to a Button component.", this);
			return;
		}

		// Register click listener
		button.onClick.AddListener(OnButtonClick);
	}

	protected void OnDisable()
	{
		Button button = GetComponent<Button>();
		if(button != null)
			button.onClick.RemoveListener(OnButtonClick);
	}

	void OnButtonClick()
	{
		if(AudioManager.Instance != null)
			AudioManager.Instance.PlayClickSound();
	}
}
