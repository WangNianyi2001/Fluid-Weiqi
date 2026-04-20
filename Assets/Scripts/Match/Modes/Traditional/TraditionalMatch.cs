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

	int passCount = 0;
	protected override void OnPass()
	{
		Board.Current.ClearPreview();

		++passCount;
		if(passCount == PlayerCount)
		{
			ui.ShowEnding();
			InputEnabled = false;
			return;
		}
		// TODO: Show pass UI

		StepPlayerIndex();
	}
	#endregion
}
