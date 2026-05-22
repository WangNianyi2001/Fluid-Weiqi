using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LaoWangAiPlayer : AiPlayer
{
	const float MinDistanceEpsilon = 0.0001f;

	LaoWangAiConfig laoWangConfig;
	bool isActive;
	bool cancelled;
	readonly BoardUtility.BoardCaches evaluationCaches = new();

	public void Initialize(Match match, int playerIndex, MatchRule rule, LaoWangAiConfig config)
	{
		base.Initialize(match, playerIndex, rule, config);
		laoWangConfig = config;
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

		StartCoroutine(EvaluateAndMove(snapshot));
	}

	void OnDestroy()
	{
		BoardUtility.Dispose(evaluationCaches);
	}

	IEnumerator EvaluateAndMove(BoardState state)
	{
		yield return null; // ensure async relative to SetMoveRight caller

		int sampleCount = laoWangConfig != null ? laoWangConfig.SampleCount : 12;
		float evaluationDelay = laoWangConfig != null ? laoWangConfig.PerCandidateEvaluationDelay : 0f;

		Vector2 bestCandidate = default;
		float bestLoss = float.PositiveInfinity;
		bool hasCandidate = false;

		for(int i = 0; i < sampleCount; ++i)
		{
			if(cancelled || Match.IsEnded)
				yield break;

			if(evaluationDelay > 0f)
				yield return new WaitForSeconds(evaluationDelay);

			Board board = Board.Current;
			Vector2 point = board != null
				? board.SampleUniformAbsolutePosition()
				: new Vector2(Random.Range(0, Mathf.Max(1, Mathf.RoundToInt(state.Size))), Random.Range(0, Mathf.Max(1, Mathf.RoundToInt(state.Size))));
			if(!IsLegalPlacement(state, point))
				continue;

			float loss = EvaluateLoss(state, point);
			if(!hasCandidate || loss < bestLoss)
			{
				hasCandidate = true;
				bestLoss = loss;
				bestCandidate = point;
			}
		}

		if(cancelled || Match.IsEnded)
			yield break;

		float placementStrength = Match.PlacementStrengthPerPlacement;

		if(hasCandidate && Match.ReceivePlace(PlayerIndex, bestCandidate, placementStrength))
		{
			NotifyMadeMove();
			yield break;
		}

		Match.ReceivePass(PlayerIndex);
		NotifyMadeMove();
	}

	bool IsLegalPlacement(BoardState state, Vector2 point)
	{
		if(!evaluationCaches.isInitialized)
			BoardUtility.Initialize(evaluationCaches);

		evaluationCaches.topology = Board.Current.Topology;

		Color[] playerColors = BuildPlayerColors(state.PlayerCount);
		BoardUtility.RenderAnalysis(evaluationCaches, state, playerColors);
		return BoardUtility.TryPlaceStoneStandard(evaluationCaches, state, PlayerIndex, point, out _);
	}

	float EvaluateLoss(BoardState state, Vector2 point)
	{
		float distance = ComputeNearestDistanceToOwnStoneOrEdge(state, point);
		float idealDistance = ComputeIdealDistance(state);

		float safeDistance = Mathf.Max(MinDistanceEpsilon, distance);
		float safeIdeal = Mathf.Max(MinDistanceEpsilon, idealDistance);
		float delta = Mathf.Log(safeDistance) - Mathf.Log(safeIdeal);
		return Mathf.Exp(delta * delta);
	}

	float ComputeIdealDistance(BoardState state)
	{
		float boardSize = Mathf.Max(1f, state.Size);
		int turnNumber = Match.GetCurrentTurnNumber();
		if(turnNumber <= laoWangConfig.startingTurnCount)
			return Mathf.Min(laoWangConfig.startingIdealDistance, boardSize * 0.5f);

		float initial = laoWangConfig != null ? laoWangConfig.InitialIdealDistance : 3f;
		float end = laoWangConfig != null ? laoWangConfig.FinalIdealDistance : 0.75f;
		float decayRate = laoWangConfig != null ? laoWangConfig.IdealDistanceDecayRate : 0.2f;
		float t = Mathf.Max(0, turnNumber - 2);
		return end + (initial - end) * Mathf.Exp(-decayRate * t);
	}

	float ComputeNearestDistanceToOwnStoneOrEdge(BoardState state, Vector2 point)
	{
		Board board = Board.Current;
		float edgeDistance = board != null
			? board.ComputeDistanceToBoundary(point)
			: Mathf.Min(
				Mathf.Min(point.x, Mathf.Max(0f, state.Size - 1f) - point.x),
				Mathf.Min(point.y, Mathf.Max(0f, state.Size - 1f) - point.y));

		float nearestOwnStoneDistance = float.PositiveInfinity;
		IReadOnlyList<StonePlacement> ownStones = state.GetStones(PlayerIndex);
		for(int i = 0; i < ownStones.Count; ++i)
		{
			float dist = Vector2.Distance(point, ownStones[i].position);
			if(dist < nearestOwnStoneDistance)
				nearestOwnStoneDistance = dist;
		}

		return Mathf.Min(edgeDistance, nearestOwnStoneDistance);
	}

	Color[] BuildPlayerColors(int playerCount)
	{
		Color[] colors = new Color[Mathf.Max(0, playerCount)];
		IReadOnlyList<PlayerInfo> infos = Match.PlayerInfos;
		if(infos == null)
			return colors;

		int length = Mathf.Min(colors.Length, infos.Count);
		for(int i = 0; i < length; ++i)
			colors[i] = infos[i].color;
		return colors;
	}
}
