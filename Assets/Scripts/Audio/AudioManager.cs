using UnityEngine;

public class AudioManager : MonoBehaviour
{
	public static AudioManager Instance { get; private set; }

	#region Constants
	const string PlaceSoundPath = "Sounds/Place";
	const string CaptureSoundPath = "Sounds/Capture";
	const string SkipSoundPath = "Sounds/Skip";
	const string ClickSoundPath = "Sounds/Click";
	#endregion

	#region Cached resources
	AudioClip placeClip;
	AudioClip captureClip;
	AudioClip skipClip;
	AudioClip clickClip;
	#endregion

	#region Runtime state
	AudioSource sfxSource;
	#endregion

	#region Unity life cycle
	protected void Awake()
	{
		if(Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}

		Instance = this;
		DontDestroyOnLoad(gameObject);

		// Create or get AudioSource
		sfxSource = GetComponent<AudioSource>();
		if(sfxSource == null)
			sfxSource = gameObject.AddComponent<AudioSource>();

		// Load sound clips on demand
		placeClip = Resources.Load<AudioClip>(PlaceSoundPath);
		captureClip = Resources.Load<AudioClip>(CaptureSoundPath);
		skipClip = Resources.Load<AudioClip>(SkipSoundPath);
		clickClip = Resources.Load<AudioClip>(ClickSoundPath);

		if(placeClip == null)
			Debug.LogWarning($"AudioManager: Place sound not found at '{PlaceSoundPath}'");
		if(captureClip == null)
			Debug.LogWarning($"AudioManager: Capture sound not found at '{CaptureSoundPath}'");
		if(skipClip == null)
			Debug.LogWarning($"AudioManager: Skip sound not found at '{SkipSoundPath}'");
		if(clickClip == null)
			Debug.LogWarning($"AudioManager: Click sound not found at '{ClickSoundPath}'");
	}

	protected void OnDestroy()
	{
		if(Instance == this)
			Instance = null;
	}
	#endregion

	#region Core SFX playback
	/// <summary>
	/// Play a sound effect with optional pitch variance.
	/// </summary>
	/// <param name="clip">AudioClip to play.</param>
	/// <param name="volume">Volume [0, 1].</param>
	/// <param name="pitchVariance">Pitch randomization factor, e.g., 0.1 means ±10% of base pitch.</param>
	public void PlaySfx(AudioClip clip, float volume = 1f, float pitchVariance = 0f)
	{
		if(clip == null || sfxSource == null)
			return;

		float pitch = 1f;
		if(pitchVariance > 0f)
		{
			float variance = pitchVariance * Random.Range(-1f, 1f);
			pitch = 1f + variance;
		}

		sfxSource.pitch = pitch;
		sfxSource.PlayOneShot(clip, volume);
	}
	#endregion

	#region Gameplay SFX wrappers
	/// <summary>
	/// Play sound when a stone is placed.
	/// </summary>
	public void PlayPlaceStoneSound(float volume = 1f, float pitchVariance = 0.05f)
	{
		PlaySfx(placeClip, volume, pitchVariance);
	}

	/// <summary>
	/// Play sound when stones are captured.
	/// </summary>
	public void PlayCaptureSound(float volume = 1f, float pitchVariance = 0.1f)
	{
		PlaySfx(captureClip, volume, pitchVariance);
	}

	/// <summary>
	/// Play sound when player skips their turn.
	/// </summary>
	public void PlaySkipSound(float volume = 1f, float pitchVariance = 0f)
	{
		PlaySfx(skipClip, volume, pitchVariance);
	}

	/// <summary>
	/// Play sound when UI button is clicked.
	/// </summary>
	public void PlayClickSound(float volume = 0.8f, float pitchVariance = 0f)
	{
		PlaySfx(clickClip, volume, pitchVariance);
	}
	#endregion
}
