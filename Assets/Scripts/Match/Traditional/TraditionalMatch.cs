using UnityEngine;

public class TraditionalMatch : Match
{
	TraditionalMatchUi ui;

	protected new void Awake()
	{
		base.Awake();
		
		ui = Instantiate(Resources.Load<GameObject>("Prefabs/Traditional Match UI"), transform).GetComponent<TraditionalMatchUi>();
	}

	protected void OnDestroy()
	{
		Destroy(ui.gameObject);
		ui = null;
	}
}
