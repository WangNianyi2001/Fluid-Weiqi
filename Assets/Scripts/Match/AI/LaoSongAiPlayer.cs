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
	float lastPlacementTime = -1f;
	float penLiftCooldownUntil = -1f;

	public void Initialize(Match match, int playerIndex, MatchRule rule, LaoSongAiConfig config)
	{
		base.Initialize(match, playerIndex, rule, config);
		laoSongConfig = config;
		if(Match != null)
			Match.OnPlayerScoringRequestStateChanged += OnPlayerScoringRequestStateChanged;
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

	void OnPlayerScoringRequestStateChanged()
	{
		TryRespondToScoringRequest();
	}

	void TryRespondToScoringRequest()
	{
		if(Match == null || Match.IsEnded)
			return;
		if(!Match.SupportsRequestScoringAction)
			return;
		if(Match.IsPlayerResigned(PlayerIndex) || Match.IsPlayerScoringRequested(PlayerIndex))
			return;

		bool hasPeerRequest = false;
		for(int i = 0; i < Match.PlayerCount; ++i)
		{
			if(i == PlayerIndex || Match.IsPlayerResigned(i))
				continue;
			if(Match.IsPlayerScoringRequested(i))
			{
				hasPeerRequest = true;
				break;
			}
		}

		if(!hasPeerRequest)
			return;

		if(!ShouldApproveScoringRequest())
			return;

		Match.TrySubmitSystemAction(PlayerIndex, MatchActionType.RequestScoring);
	}

	bool ShouldApproveScoringRequest()
	{
		if(Board.Current == null || Board.Current.State == null)
			return false;

		Color[] playerColors = new Color[Mathf.Min(Match.PlayerCount, BoardUtility.MaxPlayers)];
		for(int i = 0; i < playerColors.Length; ++i)
			playerColors[i] = Match.PlayerInfos[i].color;

		BoardUtility.RenderAnalysis(Board.Current.Caches, Board.Current.State, playerColors);
		float[] areaByPlayer = BoardUtility.GetPlayerAreasByDominance(Board.Current, Match.PlayerCount);
		if(areaByPlayer == null || areaByPlayer.Length == 0 || PlayerIndex < 0 || PlayerIndex >= areaByPlayer.Length)
			return false;

		float occupiedRatio = GetDominanceOccupiedRatio(areaByPlayer);
		if(occupiedRatio < 0.5f)
			return false;

		float myArea = Mathf.Max(0f, areaByPlayer[PlayerIndex]);
		bool greaterThanAll = true;
		bool hasPlayerFarAhead = false;
		for(int i = 0; i < areaByPlayer.Length; ++i)
		{
			if(i == PlayerIndex)
				continue;

			float otherArea = Mathf.Max(0f, areaByPlayer[i]);
			if(!(myArea > otherArea))
				greaterThanAll = false;
			if(otherArea > myArea * 1.5f)
				hasPlayerFarAhead = true;
		}

		return greaterThanAll || hasPlayerFarAhead;
	}

	float GetDominanceOccupiedRatio(float[] areaByPlayer)
	{
		if(Board.Current == null || Board.Current.State == null || areaByPlayer == null)
			return 0f;

		float occupied = 0f;
		for(int i = 0; i < areaByPlayer.Length; ++i)
			occupied += Mathf.Max(0f, areaByPlayer[i]);

		float total = Mathf.Pow(Board.Current.State.Size, 2f);
		if(total <= 0f)
			return 0f;

		return Mathf.Clamp01(occupied / total);
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
		float minInterval = laoSongConfig != null ? laoSongConfig.PaintingModeMinPlacementInterval : 0f;
		if(lastPlacementTime >= 0f && now - lastPlacementTime < minInterval)
			return;

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

		float elapsed = lastPlacementTime < 0f ? GetContinuousStepDuration() : Mathf.Max(0f, Time.time - lastPlacementTime);

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
			if(ShouldAvoidOwnedTerritoryPlacement(state, candidate))
				continue;

			if(!Match.ReceivePlace(PlayerIndex, candidate, placementStrength))
				continue;

			brushIsDown = true;
			hasLastPlacement = true;
			lastPlacement = candidate;
			lastPlacementTime = Time.time;
			NotifyMadeMove();
			return true;
		}

		return false;
	}

	bool ShouldAvoidOwnedTerritoryPlacement(BoardState state, Vector2 candidate)
	{
		if(state == null || Board.Current == null || Board.Current.Caches == null)
			return false;
		if(!IsContinuousMode)
			return false;
		if(!TryGetModeConfig(out MatchModeConfig modeConfig) || modeConfig.IsTurnBased)
			return false;
		if(!BoardUtility.IsOccupiedAtAbsolutePosition(Board.Current.Caches, state, candidate))
			return false;

		return BoardUtility.GetTerritoryOwnerAtAbsolutePosition(Board.Current.Caches, state, candidate) == PlayerIndex;
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
		lastPlacementTime = -1f;
		penLiftCooldownUntil = -1f;
	}

	void OnDestroy()
	{
		if(Match != null)
			Match.OnPlayerScoringRequestStateChanged -= OnPlayerScoringRequestStateChanged;
	}
}
