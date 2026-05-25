using UnityEngine;

public class PaintingMatch : Match
{
	PaintingMatchModeConfig modeConfig;
	float pendingShrinkMargin;
	float lastShrinkSampleTime = -1f;
	float lastPlacementTime = -1f;
	float nextAutoScoringSampleTime;
	bool autoScoringArmed;
	float autoScoringRemainingSeconds;

	public bool IsAutoScoringArmed => autoScoringArmed;
	public float AutoScoringRemainingSeconds => Mathf.Max(0f, autoScoringRemainingSeconds);

	PaintingMatchModeConfig GetModeConfig()
	{
		if(modeConfig != null)
			return modeConfig;

		if(GameManager.Instance != null
			&& !string.IsNullOrWhiteSpace(Rule.modeId)
			&& GameManager.Instance.TryGetMatchModeConfig(Rule.modeId, out MatchModeConfig config))
		{
			modeConfig = config as PaintingMatchModeConfig;
		}

		return modeConfig;
	}

	protected override bool CanPlayerMakeMoveNow(int playerIndex)
	{
		return !IsEnded && playerIndex >= 0 && playerIndex < PlayerCount;
	}

	protected override void OnPlayerMoveAccepted(int playerIndex)
	{
		AccumulateShrinkByElapsedTime();

		// Keep a global move sequence for UI/network bookkeeping without rotating ownership.
		IncrementTurnSequence();
	}

	protected override float GetShrinkDeltaMarginForAcceptedMove()
	{
		float delta = Mathf.Max(0f, pendingShrinkMargin);
		pendingShrinkMargin = 0f;
		return delta;
	}

	protected override void BeginMoveWindow()
	{
		if(IsEnded || RuntimePlayerCount == 0)
			return;

		if(lastShrinkSampleTime < 0f)
			lastShrinkSampleTime = Time.time;
		if(lastPlacementTime < 0f)
			lastPlacementTime = Time.unscaledTime;

		CancelAllPlayers();
		for(int i = 0; i < RuntimePlayerCount; ++i)
		{
			SetPlayerPassState(i, false);
			SetPlayerMoveRight(i, true);
		}
	}

	void AccumulateShrinkByElapsedTime()
	{
		float now = Time.time;
		if(lastShrinkSampleTime < 0f)
		{
			lastShrinkSampleTime = now;
			return;
		}

		float elapsed = Mathf.Max(0f, now - lastShrinkSampleTime);
		lastShrinkSampleTime = now;
		if(elapsed <= 0f)
			return;

		float shrinkSpeedPerSecond = Mathf.Max(0f, Rule.shrinkSpeed);
		pendingShrinkMargin += elapsed * shrinkSpeedPerSecond;
	}

	protected override void OnPass()
	{
		// Painting mode does not use pass to advance turn or end the match.
		SetPlayerPassState(ActivePlayerIndex, false);
	}

	protected override void OnPlacementAccepted(int playerIndex, Vector2 position, float strength)
	{
		lastPlacementTime = Time.unscaledTime;
		nextAutoScoringSampleTime = 0f; // force immediate resample next Update
	}

	protected void Update()
	{
		if(IsEnded || Board.Current == null || Board.Current.State == null)
			return;

		bool canTriggerEnd = Lobby.Current == null || !Lobby.Current.IsOnline || Lobby.Current.IsHost;

		if(Time.unscaledTime < nextAutoScoringSampleTime)
			return;

		nextAutoScoringSampleTime = Time.unscaledTime + 0.1f;

		PaintingMatchModeConfig config = GetModeConfig();
		float threshold = config != null ? config.AutoScoringAreaThreshold : 0.5f;
		float idleSeconds = config != null ? config.AutoScoringIdleSeconds : 5f;

		float occupiedRatio = GetDominanceOccupiedRatio();
		if(occupiedRatio < threshold)
		{
			autoScoringArmed = false;
			autoScoringRemainingSeconds = idleSeconds;
			return;
		}

		autoScoringArmed = true;
		float elapsedWithoutPlacement = Mathf.Max(0f, Time.unscaledTime - lastPlacementTime);
		autoScoringRemainingSeconds = Mathf.Max(0f, idleSeconds - elapsedWithoutPlacement);
		if(autoScoringRemainingSeconds <= 0f && canTriggerEnd)
		{
			EndMatch();
			BroadcastSnapshotToClientsForSystemEvent();
		}
	}

	float GetDominanceOccupiedRatio()
	{
		Board board = Board.Current;
		if(board == null || board.State == null)
			return 0f;

		Color[] playerColors = new Color[Mathf.Min(PlayerCount, BoardUtility.MaxPlayers)];
		for(int i = 0; i < playerColors.Length; ++i)
			playerColors[i] = PlayerInfos[i].color;

		BoardUtility.RenderAnalysis(board.Caches, board.State, playerColors);
		float[] areaByPlayer = BoardUtility.GetPlayerAreasByDominance(board, PlayerCount);
		if(areaByPlayer == null || areaByPlayer.Length == 0)
			return 0f;

		float occupied = 0f;
		for(int i = 0; i < areaByPlayer.Length; ++i)
			occupied += Mathf.Max(0f, areaByPlayer[i]);

		float total = Mathf.Pow(board.State.Size, 2f);
		if(total <= 0f)
			return 0f;

		return Mathf.Clamp01(occupied / total);
	}

	public override bool UseContinuousPlacement => true;
	public override bool SupportsPassAction => false;
	public override bool SupportsRequestScoringAction => true;
	public override bool SupportsResignAction => true;

	public override float ContinuousPlacementFrequencyPerSecond
	{
		get
		{
			PaintingMatchModeConfig config = GetModeConfig();
			return config != null ? config.PlacementFrequencyPerSecond : 12f;
		}
	}

	public override float ContinuousPlacementMaxWeightPerSecond
	{
		get
		{
			PaintingMatchModeConfig config = GetModeConfig();
			return config != null ? config.PlacementMaxWeightPerSecond : 1f;
		}
	}
}
