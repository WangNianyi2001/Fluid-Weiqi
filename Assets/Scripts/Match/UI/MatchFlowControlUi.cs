using UnityEngine;
using UnityEngine.UI;

public class MatchFlowControlUi : MonoBehaviour
{
	[SerializeField] GameObject panelRoot;
	[SerializeField] GameObject passButtonRoot;
	[SerializeField] GameObject requestScoringButtonRoot;
	[SerializeField] GameObject resignButtonRoot;
	[SerializeField] Text autoScoringCountdownText;

	GameObject PanelRoot => panelRoot != null ? panelRoot : gameObject;
	Match Match => Match.Current;

	protected void Awake()
	{
		if(Match != null)
			Match.OnPlayerMoveRightChanged += OnPlayerMoveRightChanged;
	}

	protected void Start()
	{
		EnsureCountdownText();
		RefreshVisibility();
		RefreshCountdownText();
	}

	protected void Update()
	{
		RefreshVisibility();
		RefreshCountdownText();
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
		Match match = Match;
		if(match == null)
		{
			SetRootActive(passButtonRoot, false);
			SetRootActive(requestScoringButtonRoot, false);
			SetRootActive(resignButtonRoot, false);
			SetRootActive(PanelRoot, false);
			return;
		}

		bool hasLocalMoveRight = HasLocallyControllableMoveRight(match);
		bool passVisible = match.SupportsPassAction && hasLocalMoveRight;
		bool requestScoringVisible = match.SupportsRequestScoringAction;
		bool resignVisible = match.SupportsResignAction;
		bool countdownVisible = ShouldShowCountdown(match);

		SetRootActive(passButtonRoot, passVisible);
		SetRootActive(requestScoringButtonRoot, requestScoringVisible);
		SetRootActive(resignButtonRoot, resignVisible);
		SetRootActive(PanelRoot, true);
	}

	bool HasLocallyControllableMoveRight(Match match)
	{
		if(match == null)
			return false;

		foreach(var entry in match.PlayerMoveRights)
		{
			if(entry.Value && match.IsPlayerLocallyControllable(entry.Key))
				return true;
		}

		return false;
	}

	bool ShouldShowCountdown(Match match)
	{
		if(match is not PaintingMatch painting)
			return false;
		return painting.IsAutoScoringArmed && !painting.IsEnded;
	}

	void EnsureCountdownText()
	{
		if(autoScoringCountdownText != null)
			return;

		RectTransform parent = PanelRoot != null ? PanelRoot.GetComponent<RectTransform>() : null;
		if(parent == null)
			return;

		GameObject go = new GameObject("AutoScoringCountdown", typeof(RectTransform), typeof(Text));
		go.transform.SetParent(parent, false);

		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(1f, 0f);
		rect.anchorMax = new Vector2(1f, 0f);
		rect.pivot = new Vector2(1f, 0f);
		rect.anchoredPosition = new Vector2(0f, 0f);
		rect.sizeDelta = new Vector2(320f, 24f);

		Text text = go.GetComponent<Text>();
		text.alignment = TextAnchor.MiddleRight;
		text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		text.fontSize = 18;
		text.color = Color.white;
		text.text = string.Empty;

		autoScoringCountdownText = text;
	}

	void RefreshCountdownText()
	{
		if(autoScoringCountdownText == null)
			return;

		if(Match is not PaintingMatch painting)
		{
			autoScoringCountdownText.gameObject.SetActive(false);
			return;
		}

		bool visible = painting.IsAutoScoringArmed && !painting.IsEnded;
		autoScoringCountdownText.gameObject.SetActive(visible);
		if(!visible)
			return;

		autoScoringCountdownText.text = $"自动点目倒计时 {painting.AutoScoringRemainingSeconds:F1}s";
	}

	void SetRootActive(GameObject root, bool active)
	{
		if(root != null)
			root.SetActive(active);
	}

	public void OnPassButtonClicked()
	{
		MatchInput input = MatchInput.Shared;
		if(input == null && Match.Current != null)
			input = MatchInput.GetOrCreate(Match.Current);
		input?.SubmitPass();
	}

	public void OnRequestScoringButtonClicked()
	{
		Match?.OnRequestScoringButtonClicked();
	}

	public void OnResignButtonClicked()
	{
		Match?.OnResignButtonClicked();
	}
}