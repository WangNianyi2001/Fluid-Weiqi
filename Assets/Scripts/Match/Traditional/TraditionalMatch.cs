using UnityEngine;

public class TraditionalMatch : Match
{
	#region Unity life cycle
	#endregion

	#region UI
	TraditionalMatchUi ui;

	protected override GameObject MakeUi()
	{
		var go = Instantiate(Resources.Load<GameObject>("Prefabs/Traditional Match UI"), transform);
		ui = go.GetComponent<TraditionalMatchUi>();
		return go;
	}
	#endregion
}
