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
	[SerializeField] List<MatchModeConfig> legacyMatchModes = new();
	[SerializeField] List<AiConfig> legacyAis = new();
	[SerializeField] List<PlayerColorOption> availablePlayerColors = new();

	public string DefaultMatchModeId => defaultMatchModeId;
	public GameObject DefaultMatchSkinPrefab => defaultMatchSkinPrefab;
	public IReadOnlyList<MatchModeConfig> LegacyMatchModes => legacyMatchModes;
	public IReadOnlyList<AiConfig> LegacyAis => legacyAis;
	public IReadOnlyList<PlayerColorOption> AvailablePlayerColors => availablePlayerColors;

	public Color GetPlayerColor(int colorIndex)
	{
		if(availablePlayerColors == null || availablePlayerColors.Count == 0)
			return Color.white;
		int safeIndex = Mathf.Clamp(colorIndex, 0, availablePlayerColors.Count - 1);
		return availablePlayerColors[safeIndex].color;
	}
}
