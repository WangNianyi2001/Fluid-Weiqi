using UnityEngine;
using System.Collections;

public class LaoSongAiPlayer : AiPlayer
{
	LaoSongAiConfig laoSongConfig;
	bool isActive;
	bool cancelled;
	bool brushIsDown;
	bool hasLastPlacement;
	Vector2 lastPlacement;
	float penLiftCooldownUntil = -1f;
	float continuousDecisionTimestamp = -1f;

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
			ResetContinuousStrokeState();
			StopAllCoroutines();
			return;
		}

		cancelled = false;
		ResetContinuousStrokeState();
		if(Match.IsEnded)
			return;

		StartCoroutine(ExecuteLoop(GetDelay()));
	}

	float GetDelay()
	{
		if(laoSongConfig == null)
			return 0f;
		if(!TryGetModeConfig(out MatchModeConfig modeConfig))
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

			if(IsContinuousMode)
				ExecuteContinuousMove(state);
			else
				ExecuteTurnBasedMove(state);

			if(!IsContinuousMode)
				yield break;

			yield return new WaitForSeconds(GetContinuousStepDuration());
		}
	}

	void ExecuteTurnBasedMove(BoardState state)
	{
		if(TryPlaceGlobal(state))
			return;

		if(cancelled || Match.IsEnded)
			return;

		Match.ReceivePass(PlayerIndex);
		NotifyMadeMove();
	}

	void ExecuteContinuousMove(BoardState state)
	{
		if(cancelled || Match.IsEnded)
			return;

		float now = Time.time;
		if(now < penLiftCooldownUntil)
			return;

		if(!brushIsDown)
		{
			TryPlaceGlobal(state);
			return;
		}

		if(TryPlaceNearLast(state))
			return;

		if(TryPlaceGlobal(state))
			return;

		LiftPen(now);
	}

	bool TryPlaceGlobal(BoardState state)
	{
		return TryPlaceFromSampler(state, _ => Board.Current.SampleUniformAbsolutePosition());
	}

	bool TryPlaceNearLast(BoardState state)
	{
		if(!hasLastPlacement || Board.Current == null)
			return false;

		float elapsed = GetElapsedSince(ref continuousDecisionTimestamp);
		float stepDuration = Mathf.Max(0f, GetContinuousStepDuration());
		if(elapsed <= 0f)
			elapsed = stepDuration;

		float maxMoveRate = laoSongConfig != null ? laoSongConfig.PaintingModeMaxMoveRate : 0f;
		float radius = Mathf.Max(0f, maxMoveRate * elapsed);
		return TryPlaceFromSampler(state, _ => Board.Current.SampleUniformAbsolutePositionInNeighborhood(lastPlacement, radius));
	}

	bool TryPlaceFromSampler(BoardState state, System.Func<BoardState, Vector2> sampler)
	{
		if(Board.Current == null)
			return false;

		int rollCount = laoSongConfig != null ? laoSongConfig.MaxRollCount : 3;
		float placementStrength = Match.PlacementStrengthPerPlacement;

		for(int i = 0; i < rollCount; ++i)
		{
			if(cancelled || Match.IsEnded)
				return false;

			Vector2 candidate = Board.Current.NormalizeAbsolutePosition(sampler(state));
			if(!Match.ReceivePlace(PlayerIndex, candidate, placementStrength))
				continue;

			brushIsDown = true;
			hasLastPlacement = true;
			lastPlacement = candidate;
			continuousDecisionTimestamp = Time.time;
			NotifyMadeMove();
			return true;
		}

		return false;
	}

	void LiftPen(float now)
	{
		brushIsDown = false;
		float delay = laoSongConfig != null ? laoSongConfig.PaintingModePenLiftDelay : 0f;
		penLiftCooldownUntil = now + Mathf.Max(0f, delay);
	}

	void ResetContinuousStrokeState()
	{
		brushIsDown = false;
		hasLastPlacement = false;
		lastPlacement = Vector2.zero;
		penLiftCooldownUntil = -1f;
		continuousDecisionTimestamp = -1f;
	}
}
