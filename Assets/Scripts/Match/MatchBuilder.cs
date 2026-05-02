using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class MatchBuilder : MonoBehaviour
{
	#region References
	[SerializeField] Transform uiRoot;
	[SerializeField] Transform boardAnchor;

	GameObject standardBoardPrefab;

	Match match;
	Board board;
	#endregion

	#region Build
	void BuildMatch()
	{
		board = MakeStandardBoard();
		InitializeBoard(board);
		match = MakeMatchController(Lobby.Current.MatchRule.mode);
		InitializeMatch(match, board);

		switch(Lobby.Current.MatchRule.mode)
		{
			case MatchMode.Traditional:
				break;

			case MatchMode.Training:
				break;

			default:
				// TODO
				throw new System.NotSupportedException($"Cannot build match for {Lobby.Current.MatchRule.mode}, not supported.");
		}
	}

	Board MakeStandardBoard()
	{
		if(standardBoardPrefab == null)
			standardBoardPrefab = Resources.Load<GameObject>("Prefabs/Boards/Standard");
		var go = Instantiate(standardBoardPrefab, boardAnchor);
		return go.GetComponent<Board>();
	}

	Match MakeMatchController(MatchMode mode)
	{
		GameObject host = uiRoot != null ? uiRoot.gameObject : gameObject;
		return mode switch
		{
			MatchMode.Traditional => host.AddComponent<TraditionalMatch>(),
			MatchMode.Training => host.AddComponent<TrainingMatch>(),
			_ => throw new System.NotSupportedException($"Cannot create match controller for {mode}, not supported."),
		};
	}

	void InitializeBoard(Board targetBoard)
	{
		if(targetBoard == null)
			throw new MissingReferenceException("Failed to create board for match build.");

		MatchRule rule = Lobby.Current.MatchRule;
		List<PlayerInfo> playerInfos = BuildPlayerInfos();
		targetBoard.SetState(new BoardState(playerInfos.Count, rule.boardSize));
		targetBoard.PlayerColors = playerInfos.Select(info => info.color).ToArray();
	}

	void InitializeMatch(Match targetMatch, Board targetBoard)
	{
		if(targetMatch == null)
			throw new MissingReferenceException("Failed to create match controller for match build.");

		targetMatch.PlayerInfos = BuildPlayerInfos();
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
			GameManager.Instance.LoadDefaultLobby();
		}
		BuildMatch();
	}
	#endregion
}
