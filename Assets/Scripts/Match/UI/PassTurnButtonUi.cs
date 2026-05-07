using UnityEngine;

public class PassTurnButtonUi : MonoBehaviour
{
	[SerializeField] GameObject passButtonRoot;

	GameObject PassButtonRoot => passButtonRoot != null ? passButtonRoot : gameObject;
	Match Match => Match.Current;

	protected void Awake()
	{
		if(Match != null)
			Match.OnCurrentPlayerChanged += OnCurrentPlayerChanged;
	}

	protected void Start()
	{
		RefreshVisibility();
	}

	protected void OnDestroy()
	{
		if(Match != null)
			Match.OnCurrentPlayerChanged -= OnCurrentPlayerChanged;
	}

	void OnCurrentPlayerChanged(int _)
	{
		RefreshVisibility();
	}

	void RefreshVisibility()
	{
		if(PassButtonRoot == null)
			return;

		bool visible = Match != null && Match.IsCurrentPlayerLocallyControllable;
		PassButtonRoot.SetActive(visible);
	}

	public void OnPassButtonClicked()
	{
		MatchInput input = MatchInput.Shared;
		if(input == null && Match.Current != null)
			input = MatchInput.GetOrCreate(Match.Current);
		input?.SubmitPass();
	}
}
