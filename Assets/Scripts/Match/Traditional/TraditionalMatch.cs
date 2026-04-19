using UnityEngine;

public class TraditionalMatch : Match
{
	#region UI
	protected override GameObject MakeUi()
	{
		var go = Instantiate(Resources.Load<GameObject>("Prefabs/Traditional Match UI"), transform);
		return go;
	}
	#endregion

	#region Input
	protected override void OnPlace(Vector2 position)
	{
		base.OnPlace(position);
		StepPlayerIndex();
	}

	protected override void OnPass()
	{
		Board.ClearPreview();
		StepPlayerIndex();
	}
	#endregion

	void StepPlayerIndex()
	{
		CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Board.PlayerCount;
	}
}
