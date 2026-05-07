using UnityEngine;

public class TrainingMatch : Match
{
	#region Input
	// Training placement: place the stone but do not advance the turn.
	protected override void OnPlace(Vector2 position)
	{
		base.OnPlace(position);
		LastPlacementSucceed = false;
	}

	protected override void OnRemove(Vector2 position)
	{
		if(Board.Current.State.TryRemoveStoneAtLogicalPosition(position, out BoardState nextState))
		{
			Board.Current.SetState(nextState);
			AudioManager.Instance.PlayCaptureSound();
		}
	}

	// Training-only pass semantics: do not mark pass state, only hand control to next player.
	protected override void OnPass()
	{
		for(int i = 0; i < PlayerCount; ++i)
			SetPlayerPassState(i, false);
	}
	#endregion
}
