using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PreferencesUi : MonoBehaviour
{
	[SerializeField] Slider volumeSlider;
	[SerializeField] Toggle playBrushMoveLoopSfxToggle;
	[SerializeField] Dropdown languageDropdown;

	PreferencesData preferences;
	Coroutine initializeCoroutine;
	bool listenersRegistered;

	protected void OnEnable()
	{
		TryInitialize();
		if(preferences == null)
			initializeCoroutine = StartCoroutine(InitializeWhenGameManagerReady());
	}

	protected void OnDisable()
	{
		if(initializeCoroutine != null)
		{
			StopCoroutine(initializeCoroutine);
			initializeCoroutine = null;
		}

		UnregisterListeners();
	}

	void TryInitialize()
	{
		if(GameManager.Instance == null)
			return;

		preferences = GameManager.Instance.GetPreferences();
		ApplyPreferencesToUi();
		RegisterListeners();
	}

	IEnumerator InitializeWhenGameManagerReady()
	{
		while(GameManager.Instance == null)
			yield return null;

		TryInitialize();
		initializeCoroutine = null;
	}

	void RegisterListeners()
	{
		if(listenersRegistered)
			return;

		if(volumeSlider != null)
			volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
		if(playBrushMoveLoopSfxToggle != null)
			playBrushMoveLoopSfxToggle.onValueChanged.AddListener(OnPlayBrushMoveLoopSfxChanged);

		listenersRegistered = true;
	}

	void UnregisterListeners()
	{
		if(!listenersRegistered)
			return;

		if(volumeSlider != null)
			volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
		if(playBrushMoveLoopSfxToggle != null)
			playBrushMoveLoopSfxToggle.onValueChanged.RemoveListener(OnPlayBrushMoveLoopSfxChanged);

		listenersRegistered = false;
	}

	void OnVolumeChanged(float value)
	{
		if(preferences == null)
			return;
		preferences.volume = Mathf.Clamp01(value);
		GameManager.Instance?.SavePreferences();
	}

	void OnPlayBrushMoveLoopSfxChanged(bool isOn)
	{
		if(preferences == null)
			return;
		preferences.playBrushMoveLoopSfx = isOn;
		GameManager.Instance?.SavePreferences();
	}

	void ApplyPreferencesToUi()
	{
		if(preferences == null)
			return;

		if(volumeSlider != null)
		{
			volumeSlider.minValue = 0f;
			volumeSlider.maxValue = 1f;
			volumeSlider.SetValueWithoutNotify(preferences.volume);
		}

		if(playBrushMoveLoopSfxToggle != null)
			playBrushMoveLoopSfxToggle.SetIsOnWithoutNotify(preferences.playBrushMoveLoopSfx);

		if(languageDropdown != null)
		{
			int clampedLanguageIndex = Mathf.Clamp(preferences.languageIndex, 0, Mathf.Max(0, languageDropdown.options.Count - 1));
			preferences.languageIndex = clampedLanguageIndex;
			languageDropdown.SetValueWithoutNotify(clampedLanguageIndex);
			languageDropdown.interactable = false;
		}

		GameManager.Instance?.SavePreferences();
	}
}