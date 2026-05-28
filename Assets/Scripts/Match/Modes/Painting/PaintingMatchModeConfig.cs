using UnityEngine;

[CreateAssetMenu(fileName = "PaintingMatchModeConfig", menuName = "FluidWeiqi/Match Mode Config/Painting")]
public class PaintingMatchModeConfig : MatchModeConfig
{
	[SerializeField] float placementFrequencyPerSecond = 12f;
	[SerializeField] float placementMaxWeightPerSecond = 1f;
	[SerializeField, Range(0.01f, 1f)] float autoScoringAreaThreshold = 0.5f;
	[SerializeField] float autoScoringIdleSeconds = 5f;

	public float PlacementFrequencyPerSecond => Mathf.Max(1f, placementFrequencyPerSecond);
	public float PlacementMaxWeightPerSecond => Mathf.Max(0.0001f, placementMaxWeightPerSecond);
	public float AutoScoringAreaThreshold => Mathf.Clamp(autoScoringAreaThreshold, 0.01f, 1f);
	public float AutoScoringIdleSeconds => Mathf.Max(0.2f, autoScoringIdleSeconds);

	public override bool ValidateRules(MatchRule rule, Lobby lobby, out string errorMessage)
	{
		if(!base.ValidateRules(rule, lobby, out errorMessage))
			return false;

		if(lobby?.Players != null)
		{
			int localPlayerCount = 0;
			for(int i = 0; i < lobby.Players.Count; ++i)
			{
				if(lobby.Players[i] != null && lobby.Players[i].type == PlayerType.Local)
					localPlayerCount += 1;
			}

			if(localPlayerCount > 1)
			{
				errorMessage = "画笔模式不支持多个本地玩家。";
				return false;
			}
		}

		return true;
	}

	protected override Match CreateMatchController(GameObject host)
	{
		return host.AddComponent<PaintingMatch>();
	}
}
