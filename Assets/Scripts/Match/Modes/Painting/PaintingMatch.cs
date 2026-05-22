using UnityEngine;

public class PaintingMatch : Match
{
	PaintingMatchModeConfig modeConfig;

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
		// Keep a global move sequence for UI/network bookkeeping without rotating ownership.
		IncrementTurnSequence();
	}

	protected override void BeginMoveWindow()
	{
		if(IsEnded || RuntimePlayerCount == 0)
			return;

		CancelAllPlayers();
		for(int i = 0; i < RuntimePlayerCount; ++i)
		{
			SetPlayerPassState(i, false);
			SetPlayerMoveRight(i, true);
		}
	}

	protected override void OnPass()
	{
		// Painting mode does not use pass to advance turn or end the match.
		SetPlayerPassState(ActivePlayerIndex, false);
	}

	public override bool UseContinuousPlacement => true;

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
