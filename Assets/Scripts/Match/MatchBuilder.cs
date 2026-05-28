using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class MatchBuilder : MonoBehaviour
{
	#region References
	[SerializeField] Transform uiRoot;
	#endregion

	#region Build
	void BuildMatch()
	{
		if(GameManager.Instance == null)
			throw new MissingReferenceException("GameManager is missing while building match.");

		MatchRule rule = Lobby.Current.MatchRule;
		if(!GameManager.Instance.TryGetMatchModeConfig(rule.modeId, out MatchModeConfig modeConfig))
			throw new System.NotSupportedException($"Cannot build match, mode config not found: {rule.modeId}");

		List<PlayerInfo> playerInfos = BuildPlayerInfos();
		GameObject matchSkinPrefab = rule.boardShape == BoardShape.Sphere
			? GameManager.Instance.DefaultSphericalMatchSkinPrefab ?? GameManager.Instance.DefaultMatchSkinPrefab
			: GameManager.Instance.DefaultMatchSkinPrefab;
		MatchBuildContext context = new MatchBuildContext()
		{
			Rule = rule,
			PlayerInfos = playerInfos,
			UiRoot = uiRoot,
			MatchRoot = transform,
			MatchSkinPrefab = matchSkinPrefab,
		};

		modeConfig.BuildMatch(context);
	}

	List<PlayerInfo> BuildPlayerInfos()
	{
		return Lobby.Current.Players
			.Select(player => new PlayerInfo
			{
				name = player.GetLocalizedName(),
				color = player.color,
			})
			.ToList();
	}
	#endregion

	#region Unity ife cycle
	protected void Start()
	{
		if(Lobby.Current == null)
		{
			Debug.LogWarning("No lobby present, building default match.");
			GameManager.Instance.CreateLobby();
			Destroy(this);
			return;
		}

		if(!Lobby.Current.ValidateStartingCondition(out string errorMessage, out List<string> _))
		{
			Debug.LogError($"Failed to build match: {errorMessage}");
			GameManager.Instance.LoadDefaultLobby();
			GameManager.Instance.SwitchScene(GameScene.StartMenu);
			Destroy(this);
			return;
		}

		BuildMatch();
		Destroy(this);
	}
	#endregion
}

public sealed class MatchBuildContext
{
	public MatchRule Rule { get; set; }
	public IReadOnlyList<PlayerInfo> PlayerInfos { get; set; }
	public Transform UiRoot { get; set; }
	public Transform MatchRoot { get; set; }
	public GameObject MatchSkinPrefab { get; set; }
}
