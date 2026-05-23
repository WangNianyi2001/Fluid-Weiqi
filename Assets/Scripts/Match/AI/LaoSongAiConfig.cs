using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "LaoSongAiConfig", menuName = "FluidWeiqi/AI/Lao Song")]
public class LaoSongAiConfig : AiConfig<LaoSongAiConfig>
{
	[SerializeField] int maxRollCount = 3;
	[SerializeField] float turnBasedModeDelay = 0.5f;
	[SerializeField] float paintingModeMaxMoveRate = 4f;
	[SerializeField] float paintingModePenLiftDelay = 0.35f;
	public int MaxRollCount => Mathf.Max(1, maxRollCount);
	public float TurnBasedModeDelay => Mathf.Max(0f, turnBasedModeDelay);
	public float PaintingModeMaxMoveRate => Mathf.Max(0f, paintingModeMaxMoveRate);
	public float PaintingModePenLiftDelay => Mathf.Max(0f, paintingModePenLiftDelay);

	public override IEnumerable<string> EnumerateSupportedModeIds()
	{
		if(GameManager.Instance == null)
			yield break;

		IReadOnlyList<MatchModeConfig> modes = GameManager.Instance.LoadedMatchModeConfigs;
		if(modes == null)
			yield break;

		for(int i = 0; i < modes.Count; ++i)
		{
			MatchModeConfig mode = modes[i];
			if(mode == null || string.IsNullOrWhiteSpace(mode.ModeId))
				continue;
			yield return mode.ModeId;
		}
	}

	public override bool SupportsMode(string modeId)
	{
		if(string.IsNullOrWhiteSpace(modeId) || GameManager.Instance == null)
			return false;

		return GameManager.Instance.TryGetMatchModeConfig(modeId, out _);
	}

	public override AiPlayer CreatePlayer(Match match, int playerIndex, MatchRule rule)
	{
		LaoSongAiPlayer player = match.gameObject.AddComponent<LaoSongAiPlayer>();
		player.Initialize(match, playerIndex, rule, this);
		return player;
	}
}
