using UnityEngine;

public abstract class AiPlayer : MatchPlayer
{
	protected MatchRule Rule { get; private set; }
	protected AiConfig Config { get; private set; }

	public override bool IsAlive => true;

	public virtual void Initialize(Match match, int playerIndex, MatchRule rule, AiConfig config)
	{
		base.Initialize(match, playerIndex);
		Rule = rule;
		Config = config;
	}

	protected bool IsContinuousMode => Match != null && Match.UseContinuousPlacement;

	protected float GetContinuousStepDuration()
	{
		if(Match == null || !Match.UseContinuousPlacement)
			return 0f;

		float frequency = Mathf.Max(1f, Match.ContinuousPlacementFrequencyPerSecond);
		return 1f / frequency;
	}

	protected float GetElapsedSince(ref float timestamp)
	{
		float now = Time.time;
		if(timestamp < 0f)
		{
			timestamp = now;
			return 0f;
		}

		float elapsed = Mathf.Max(0f, now - timestamp);
		timestamp = now;
		return elapsed;
	}

	protected bool TryGetModeConfig(out MatchModeConfig modeConfig)
	{
		modeConfig = null;
		if(GameManager.Instance == null || string.IsNullOrWhiteSpace(Rule.modeId))
			return false;

		return GameManager.Instance.TryGetMatchModeConfig(Rule.modeId, out modeConfig);
	}
}
