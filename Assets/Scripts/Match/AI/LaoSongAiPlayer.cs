using UnityEngine;
using System.Collections;

public class LaoSongAiPlayer : AiPlayer
{
	LaoSongAiConfig laoSongConfig;
	bool cancelled;

	public void Initialize(Match match, int playerIndex, MatchRule rule, LaoSongAiConfig config)
	{
		base.Initialize(match, playerIndex, rule, config);
		laoSongConfig = config;
	}

	public override void RequestMove(BoardState state)
	{
		cancelled = false;
		if(state == null || Match.IsEnded)
			return;

		float delay = GetDelay();
		if(delay > 0f)
			StartCoroutine(ExecuteAfterDelay(state, delay));
		else
			ExecuteMove(state);
	}

	public override void CancelMove()
	{
		cancelled = true;
		StopAllCoroutines();
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
		yield return new WaitForSeconds(delay);
		if(!cancelled && !Match.IsEnded)
			ExecuteMove(state);
	}

	void ExecuteMove(BoardState state)
	{
		int rollCount = laoSongConfig != null ? laoSongConfig.MaxRollCount : 3;
		float size = Mathf.Max(1f, state.Size);

		for(int i = 0; i < rollCount; ++i)
		{
			if(cancelled || Match.IsEnded)
				return;

			Vector2 candidate = new Vector2(Random.value, Random.value) * size;
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
