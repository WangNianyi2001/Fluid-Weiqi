using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class TraditionalMatchEndingUi : MonoBehaviour
{
	[SerializeField] Text resultText;
	[SerializeField] GameObject panelRoot;
	GameScene endButtonTargetScene = GameScene.StartMenu;

	protected void Awake()
	{
		if(panelRoot == null && resultText != null)
			panelRoot = resultText.transform.parent != null ? resultText.transform.parent.gameObject : null;

		if(panelRoot != null && panelRoot != gameObject)
			panelRoot.SetActive(false);

		if(Match.Current != null)
			Match.Current.OnEnd += OnMatchEnded;
	}

	protected void Start()
	{
		gameObject.SetActive(false);
	}

	protected void OnDestroy()
	{
		if(Match.Current != null)
			Match.Current.OnEnd -= OnMatchEnded;
	}

	void OnMatchEnded()
	{
		Match match = Match.Current;
		if(match == null)
			return;

		endButtonTargetScene = GameScene.StartMenu;

		if(panelRoot != null)
			panelRoot.SetActive(true);

		MatchResultSummary summary = match.LastResultSummary ?? match.CalculateResultSummary();
		if(summary == null)
			return;

		List<string> lines = new();

		lines.Add(string.Join("\n", summary.playerResults.Select(
			player => $"{player.playerName}：{player.area.ToString("F2")} 目{(player.isResigned ? "（已认输）" : string.Empty)}"
		)));

		if(!summary.hasWinner)
		{
			lines.Add("无人获胜");
			resultText.text = string.Join("\n", lines);
			return;
		}

		if(summary.isDraw)
			lines.Add("平局");
		else
			lines.Add($"{string.Join("、", summary.winnerPlayerIndexes.Select(i => match.PlayerInfos[i].name))}胜");

		resultText.text = string.Join("\n", lines);
	}

	public void ShowMessage(string message, GameScene returnScene)
	{
		endButtonTargetScene = returnScene;

		if(panelRoot != null)
			panelRoot.SetActive(true);

		if(resultText != null)
			resultText.text = message;
	}

	public void OnEndButtonClicked()
	{
		if(GameManager.Instance == null)
			return;
		GameManager.Instance.SwitchScene(endButtonTargetScene);
	}
}
