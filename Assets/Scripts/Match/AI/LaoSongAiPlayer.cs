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
		if(Match.IsEnded)
			return;

		StartCoroutine(ExecuteLoop(GetDelay()));
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

	IEnumerator ExecuteLoop(float initialDelay)
	{
		if(initialDelay > 0f)
			yield return new WaitForSeconds(initialDelay);

		while(!cancelled && !Match.IsEnded && isActive)
		{
			BoardState state = Board.Current != null ? new BoardState(Board.Current.State) : null;
			if(state == null)
				yield break;

			ExecuteMove(state);

			if(!Match.UseContinuousPlacement)
				yield break;

			float frequency = Mathf.Max(1f, Match.ContinuousPlacementFrequencyPerSecond);
			yield return new WaitForSeconds(1f / frequency);
		}
	}

	void ExecuteMove(BoardState state)
	{
		int rollCount = laoSongConfig != null ? laoSongConfig.MaxRollCount : 3;
		float placementStrength = Match.PlacementStrengthPerPlacement;

		for(int i = 0; i < rollCount; ++i)
		{
			if(cancelled || Match.IsEnded)
				return;

			Vector2 candidate = Board.Current.SampleUniformAbsolutePosition();
			if(Match.ReceivePlace(PlayerIndex, candidate, placementStrength))
			{
				NotifyMadeMove();
				return;
			}
		}

		if(cancelled || Match.IsEnded)
			return;

		if(!Match.UseContinuousPlacement)
		{
			Match.ReceivePass(PlayerIndex);
			NotifyMadeMove();
		}
	}
}
