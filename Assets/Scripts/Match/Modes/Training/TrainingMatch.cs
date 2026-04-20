using UnityEngine;

public class TrainingMatch : Match
{
	protected override GameObject MakeUi()
	{
		var go = Instantiate(Resources.Load<GameObject>("Prefabs/Training Match UI"), transform);
		return go;
	}

	#region Input
	protected override void OnRemove(Vector2 position)
	{
		if(Board.Current.State.TryRemoveStoneAtLogicalPosition(position, out BoardState nextState))
			Board.Current.SetState(nextState);
	}

	protected override void OnPass()
	{
		StepPlayerIndex();
	}
	#endregion
}
