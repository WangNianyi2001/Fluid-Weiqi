using UnityEngine;

public class TraditionalMatch : Match
{
	#region UI
	TraditionalGameUi ui;

	protected override GameObject MakeUi()
	{
		var go = Instantiate(Resources.Load<GameObject>("Prefabs/Traditional Match UI"), transform);
		ui = go.GetComponent<TraditionalGameUi>();
		return go;
	}
	#endregion

	#region Input
	protected override void OnPlace(Vector2 position)
	{
		base.OnPlace(position);

		if(LastPlacementSucceed)
		{
			passCount = 0;
			StepPlayerIndex();
		}
	}

	protected override void OnPass()
	{
		Board.Current.ClearPreview();

		++passCount;
		if(passCount == PlayerCount)
		{
			ui.ShowEnding();
			Input.enabled = false;
			return;
		}
		// TODO: Show pass UI

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
