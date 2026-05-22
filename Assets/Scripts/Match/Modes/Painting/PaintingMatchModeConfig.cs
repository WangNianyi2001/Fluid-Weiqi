using UnityEngine;

[CreateAssetMenu(fileName = "PaintingMatchModeConfig", menuName = "FluidWeiqi/Match Mode Config/Painting")]
public class PaintingMatchModeConfig : MatchModeConfig
{
	[SerializeField] float placementFrequencyPerSecond = 12f;
	[SerializeField] float placementMaxWeightPerSecond = 1f;

	public float PlacementFrequencyPerSecond => Mathf.Max(1f, placementFrequencyPerSecond);
	public float PlacementMaxWeightPerSecond => Mathf.Max(0.0001f, placementMaxWeightPerSecond);

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
				errorMessage = "Painting 模式不支持多个本地玩家。";
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
