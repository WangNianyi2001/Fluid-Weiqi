using UnityEngine;

public class TraditionalMatch : Match
{
	public override bool SupportsRequestScoringAction => true;
	public override bool SupportsResignAction => true;

	public override int GetCurrentTurnNumber()
	{
		BoardState state = Board.Current?.State;
		if(state == null)
			return 1;

		int totalStoneCount = 0;
		for(int i = 0; i < state.PlayerCount; ++i)
			totalStoneCount += state.GetStones(i).Count;

		return totalStoneCount / Mathf.Max(1, PlayerCount) + 1;
	}

	#region Input
	protected override void OnPlace(Vector2 position)
	{
		base.OnPlace(position);

		if(LastPlacementSucceed)
			passCount = 0;
	}

	int passCount = 0;
	protected override void OnPass()
	{
		Board.Current.ClearPreview();

		if(AudioManager.Instance != null)
			AudioManager.Instance.PlaySkipSound();

		SetPlayerPassState(CurrentPlayerIndex, true);
		++passCount;
		if(passCount >= GetActivePlayerCount())
		{
			EndMatch();
			return;
		}
	}

	int GetActivePlayerCount()
	{
		int count = 0;
		for(int i = 0; i < PlayerCount; ++i)
		{
			if(!IsPlayerResigned(i))
				count += 1;
		}

		return Mathf.Max(1, count);
	}
	#endregion
}
