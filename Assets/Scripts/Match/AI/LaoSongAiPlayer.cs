using UnityEngine;
using System.Collections;

public class LaoSongAiPlayer : AiPlayer
{
	LaoSongAiConfig laoSongConfig;
	bool isActive;
	bool cancelled;

	public void Initialize(Match match, int playerIndex, MatchRule rule, LaoSongAiConfig config)
	{
		base.Initialize(match, playerIndex, rule, config);
		laoSongConfig = config;
	}

	public override void SetMoveRight(bool canMove)
	{
		if(canMove == isActive)
			return;
		isActive = canMove;

		if(!canMove)
		{
			cancelled = true;
			StopAllCoroutines();
			return;
		}

		cancelled = false;
		BoardState snapshot = Board.Current != null ? new BoardState(Board.Current.State) : null;
		if(snapshot == null || Match.IsEnded)
			return;

		StartCoroutine(ExecuteAfterDelay(snapshot, GetDelay()));
	}

	float GetDelay()
	{
		if(laoSongConfig == null)
			return 0f;
		if(GameManager.Instance == null)
			return 0f;
		if(!GameManager.Instance.TryGetMatchModeConfig(Rule.modeId, out MatchModeConfig modeConfig))
			return 0f;
		return modeConfig.IsTurnBased ? laoSongConfig.TurnBasedModeDelay : 0f;
	}

	IEnumerator ExecuteAfterDelay(BoardState state, float delay)
	{
		if(delay > 0f)
			yield return new WaitForSeconds(delay);
		else
			yield return null;
		if(!cancelled && !Match.IsEnded)
			ExecuteMove(state);
	}

	void ExecuteMove(BoardState state)
	{
		int rollCount = laoSongConfig != null ? laoSongConfig.MaxRollCount : 3;

		for(int i = 0; i < rollCount; ++i)
		{
			if(cancelled || Match.IsEnded)
				return;

			Vector2 candidate = Board.Current.SampleUniformAbsolutePosition();
			if(Match.ReceivePlace(candidate))
			{
				NotifyMadeMove();
				return;
			}
		}

		if(cancelled || Match.IsEnded)
			return;

		Match.ReceivePass();
		NotifyMadeMove();
	}
}
