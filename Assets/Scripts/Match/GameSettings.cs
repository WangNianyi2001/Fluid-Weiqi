using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct PlayerColorOption
{
	public string name;
	public Color color;
}

[CreateAssetMenu(fileName = "GameSettings", menuName = "FluidWeiqi/Game Settings")]
public class GameSettings : ScriptableObject
{
	const string GameSettingsResourcePath = "Game Settings";
	static GameSettings instance;
	public static GameSettings Instance
	{
		get
		{
			if(instance == null)
				instance = Resources.Load<GameSettings>(GameSettingsResourcePath);
			return instance;
		}
	}

	[SerializeField] string defaultMatchModeId;
	[SerializeField] GameObject defaultMatchSkinPrefab;
	[SerializeField] GameObject defaultSphericalMatchSkinPrefab;
	[SerializeField] List<MatchModeConfig> legacyMatchModes = new();
	[SerializeField] List<AiConfig> legacyAis = new();
	[SerializeField] List<PlayerColorOption> availablePlayerColors = new();

	#region Audio Clips
	[Header("Audio Clips")]
	[SerializeField] AudioClip placeSoundClip;
	[SerializeField] AudioClip captureSoundClip;
	[SerializeField] AudioClip skipSoundClip;
	[SerializeField] AudioClip clickSoundClip;
	[SerializeField] AudioClip brushDownSoundClip;
	[SerializeField] AudioClip[] brushMoveSoundClips = new AudioClip[0];
	[SerializeField] AudioClip brushUpSoundClip;
	#endregion

	#region Brush Audio Settings
	[Header("Brush Audio")]
	[SerializeField] float brushMoveFrequencyPerSecond = 6f;
	[SerializeField, Range(0f, 1f)] float brushMoveIntervalJitter = 0.25f;
	[SerializeField] float brushDownVolume = 1f;
	[SerializeField] float brushMoveVolume = 0.8f;
	[SerializeField] float brushUpVolume = 1f;
	[SerializeField] float brushMovePitchVariance = 0.08f;
	#endregion

	public string DefaultMatchModeId => defaultMatchModeId;
	public GameObject DefaultMatchSkinPrefab => defaultMatchSkinPrefab;
	public GameObject DefaultSphericalMatchSkinPrefab => defaultSphericalMatchSkinPrefab;
	public IReadOnlyList<MatchModeConfig> LegacyMatchModes => legacyMatchModes;
	public IReadOnlyList<AiConfig> LegacyAis => legacyAis;
	public IReadOnlyList<PlayerColorOption> AvailablePlayerColors => availablePlayerColors;

	public AudioClip PlaceSoundClip => placeSoundClip;
	public AudioClip CaptureSoundClip => captureSoundClip;
	public AudioClip SkipSoundClip => skipSoundClip;
	public AudioClip ClickSoundClip => clickSoundClip;
	public AudioClip BrushDownSoundClip => brushDownSoundClip;
	public IReadOnlyList<AudioClip> BrushMoveSoundClips => brushMoveSoundClips;
	public AudioClip BrushUpSoundClip => brushUpSoundClip;

	public float BrushMoveFrequencyPerSecond => brushMoveFrequencyPerSecond;
	public float BrushMoveIntervalJitter => brushMoveIntervalJitter;
	public float BrushDownVolume => brushDownVolume;
	public float BrushMoveVolume => brushMoveVolume;
	public float BrushUpVolume => brushUpVolume;
	public float BrushMovePitchVariance => brushMovePitchVariance;

	public Color GetPlayerColor(int colorIndex)
	{
		if(availablePlayerColors == null || availablePlayerColors.Count == 0)
			return Color.white;
		int safeIndex = Mathf.Clamp(colorIndex, 0, availablePlayerColors.Count - 1);
		return availablePlayerColors[safeIndex].color;
	}
}
