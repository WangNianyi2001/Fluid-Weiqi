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

		passCount = 0;
		StepPlayerIndex();
	}

	protected override void OnPass()
	{
		Board.ClearPreview();

		++passCount;
		StepPlayerIndex();
	}
	#endregion

	#region Life cycle
	void StepPlayerIndex()
	{
		CurrentPlayerIndex = (CurrentPlayerIndex + 1) % PlayerCount;
	}

	int passCount = 0;
	#endregion
}
