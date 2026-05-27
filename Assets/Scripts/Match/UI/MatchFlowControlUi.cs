using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class MatchFlowControlUi : MonoBehaviour
{
	[SerializeField] GameObject panelRoot;
	[SerializeField] GameObject passButtonRoot;
	[SerializeField] GameObject requestScoringButtonRoot;
	[SerializeField] GameObject approveScoringButtonRoot;
	[SerializeField] GameObject rejectScoringButtonRoot;
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
		EnsureScoringResponseButtons();
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
			SetRootActive(approveScoringButtonRoot, false);
			SetRootActive(rejectScoringButtonRoot, false);
			SetRootActive(resignButtonRoot, false);
			SetRootActive(PanelRoot, false);
			return;
		}

		bool hasLocalMoveRight = false;
		bool canRequestScoring = false;
		bool hasOtherScoringRequests = false;

		for(int i = 0; i < match.PlayerCount; ++i)
		{
			if(!match.IsPlayerLocallyControllable(i))
				continue;
			if(!match.CanPlayerMove(i))
				continue;

			hasLocalMoveRight = true;

			bool hasRequestFromOthers = match.HasScoringRequestFromOtherPlayers(i);
			hasOtherScoringRequests |= hasRequestFromOthers;

			bool canThisPlayerRequest = !match.IsPlayerScoringRequested(i)
				&& !match.IsPlayerResigned(i)
				&& !hasRequestFromOthers;
			canRequestScoring |= canThisPlayerRequest;
		}

		bool passVisible = match.SupportsPassAction && hasLocalMoveRight;
		canRequestScoring = match.SupportsRequestScoringAction && canRequestScoring;
		bool approveVisible = match.SupportsRequestScoringAction && hasLocalMoveRight && hasOtherScoringRequests;
		bool rejectVisible = match.SupportsRequestScoringAction && hasLocalMoveRight && hasOtherScoringRequests;
		bool resignVisible = match.SupportsResignAction && hasLocalMoveRight;
		bool countdownVisible = ShouldShowCountdown(match);

		SetRootActive(passButtonRoot, passVisible);
		SetRootActive(requestScoringButtonRoot, canRequestScoring);
		SetRootActive(approveScoringButtonRoot, approveVisible);
		SetRootActive(rejectScoringButtonRoot, rejectVisible);
		SetRootActive(resignButtonRoot, resignVisible);
		bool anyVisible = passVisible || canRequestScoring || approveVisible || rejectVisible || resignVisible || countdownVisible;
		SetRootActive(PanelRoot, anyVisible);
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

	void EnsureScoringResponseButtons()
	{
		if(requestScoringButtonRoot == null)
			return;

		if(approveScoringButtonRoot == null)
		{
			approveScoringButtonRoot = Instantiate(requestScoringButtonRoot, requestScoringButtonRoot.transform.parent);
			approveScoringButtonRoot.name = "Approve Scoring Button";
			ConfigureButton(approveScoringButtonRoot, "同意点目", OnApproveScoringButtonClicked);
		}

		if(rejectScoringButtonRoot == null)
		{
			rejectScoringButtonRoot = Instantiate(requestScoringButtonRoot, requestScoringButtonRoot.transform.parent);
			rejectScoringButtonRoot.name = "Reject Scoring Button";
			ConfigureButton(rejectScoringButtonRoot, "拒绝点目", OnRejectScoringButtonClicked);
		}
	}

	void ConfigureButton(GameObject root, string label, UnityAction onClick)
	{
		if(root == null)
			return;

		Text text = root.GetComponentInChildren<Text>(true);
		if(text != null)
			text.text = label;

		Button button = root.GetComponentInChildren<Button>(true);
		if(button == null)
			return;

		button.onClick.RemoveAllListeners();
		if(onClick != null)
			button.onClick.AddListener(onClick);
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

	public void OnApproveScoringButtonClicked()
	{
		Match?.OnRequestScoringButtonClicked();
	}

	public void OnRejectScoringButtonClicked()
	{
		Match?.OnRejectScoringButtonClicked();
	}

	public void OnResignButtonClicked()
	{
		Match?.OnResignButtonClicked();
	}
}