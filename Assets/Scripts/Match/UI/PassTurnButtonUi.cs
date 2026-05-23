using UnityEngine;

public class PassTurnButtonUi : MonoBehaviour
{
	[SerializeField] GameObject passButtonRoot;

	GameObject PassButtonRoot => passButtonRoot != null ? passButtonRoot : gameObject;
	Match Match => Match.Current;

	protected void Awake()
	{
		if(Match != null)
			Match.OnPlayerMoveRightChanged += OnPlayerMoveRightChanged;
	}

	protected void Start()
	{
		RefreshVisibility();
	}

	protected void OnDestroy()
	{
		if(Match != null)
			Match.OnPlayerMoveRightChanged -= OnPlayerMoveRightChanged;
	}

	void OnPlayerMoveRightChanged()
	{
		RefreshVisibility();
	}

	void RefreshVisibility()
	{
		if(PassButtonRoot == null)
			return;

		bool visible = false;
		if(Match != null)
		{
			foreach(var entry in Match.PlayerMoveRights)
			{
				if(entry.Value && Match.IsPlayerLocallyControllable(entry.Key))
				{
					visible = true;
					break;
				}
			}
		}
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
