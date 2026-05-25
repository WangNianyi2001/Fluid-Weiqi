using UnityEngine;

public class AudioManager : MonoBehaviour
{
	public static AudioManager Instance { get; private set; }

	#region Cached resources
	AudioClip placeClip;
	AudioClip captureClip;
	AudioClip skipClip;
	AudioClip clickClip;
	AudioClip brushDownClip;
	AudioClip[] brushMoveClips;
	AudioClip brushUpClip;
	float brushMoveFrequencyPerSecond = 6f;
	float brushMoveIntervalJitter = 0.25f;
	float brushDownVolume = 1f;
	float brushMoveVolume = 0.8f;
	float brushUpVolume = 1f;
	float brushMovePitchVariance = 0.08f;
	#endregion

	#region Runtime state
	AudioSource sfxSource;
	bool isBrushStrokeActive;
	float nextBrushMoveSoundTime;
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

		gameObject.AddComponent<AudioListener>();

		// Create or get AudioSource
		sfxSource = GetComponent<AudioSource>();
		if(sfxSource == null)
			sfxSource = gameObject.AddComponent<AudioSource>();

		LoadAudioSettings();
	}

	protected void OnDestroy()
	{
		isBrushStrokeActive = false;
		if(Instance == this)
			Instance = null;
	}

	protected void Update()
	{
		UpdateBrushStrokeLoop();
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
			float variance = pitchVariance * UnityEngine.Random.Range(-1f, 1f);
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

	public void BeginBrushStroke()
	{
		if(isBrushStrokeActive)
			return;

		isBrushStrokeActive = true;
		PlaySfx(brushDownClip, brushDownVolume, 0f);
		nextBrushMoveSoundTime = Time.unscaledTime + GetNextBrushMoveInterval();
	}

	public void EndBrushStroke()
	{
		if(!isBrushStrokeActive)
			return;

		isBrushStrokeActive = false;
		nextBrushMoveSoundTime = 0f;
		PlaySfx(brushUpClip, brushUpVolume, 0f);
	}
	#endregion

	void UpdateBrushStrokeLoop()
	{
		if(!isBrushStrokeActive)
			return;

		if(Time.unscaledTime < nextBrushMoveSoundTime)
			return;

		PlayBrushMoveSoundFromPool();
		nextBrushMoveSoundTime = Time.unscaledTime + GetNextBrushMoveInterval();
	}

	void LoadAudioSettings()
	{
		GameSettings settings = GameSettings.Instance;
		if(settings == null)
		{
			Debug.LogWarning("AudioManager: GameSettings asset not found.");
			brushMoveClips = new AudioClip[0];
			return;
		}

		placeClip = settings.PlaceSoundClip;
		captureClip = settings.CaptureSoundClip;
		skipClip = settings.SkipSoundClip;
		clickClip = settings.ClickSoundClip;

		brushDownClip = settings.BrushDownSoundClip;
		brushUpClip = settings.BrushUpSoundClip;
		if(settings.BrushMoveSoundClips != null)
		{
			int count = settings.BrushMoveSoundClips.Count;
			brushMoveClips = new AudioClip[count];
			for(int i = 0; i < count; ++i)
				brushMoveClips[i] = settings.BrushMoveSoundClips[i];
		}
		else
		{
			brushMoveClips = new AudioClip[0];
		}

		brushMoveFrequencyPerSecond = Mathf.Max(0.1f, settings.BrushMoveFrequencyPerSecond);
		brushMoveIntervalJitter = Mathf.Clamp01(settings.BrushMoveIntervalJitter);
		brushDownVolume = Mathf.Clamp01(settings.BrushDownVolume);
		brushMoveVolume = Mathf.Clamp01(settings.BrushMoveVolume);
		brushUpVolume = Mathf.Clamp01(settings.BrushUpVolume);
		brushMovePitchVariance = Mathf.Max(0f, settings.BrushMovePitchVariance);
	}

	void PlayBrushMoveSoundFromPool()
	{
		if(brushMoveClips == null || brushMoveClips.Length == 0)
			return;

		int index = UnityEngine.Random.Range(0, brushMoveClips.Length);
		PlaySfx(brushMoveClips[index], brushMoveVolume, brushMovePitchVariance);
	}

	float GetNextBrushMoveInterval()
	{
		float frequency = Mathf.Max(0.1f, brushMoveFrequencyPerSecond);
		float baseInterval = 1f / frequency;
		float jitterScale = 1f + UnityEngine.Random.Range(-brushMoveIntervalJitter, brushMoveIntervalJitter);
		return Mathf.Max(0.02f, baseInterval * jitterScale);
	}
}
