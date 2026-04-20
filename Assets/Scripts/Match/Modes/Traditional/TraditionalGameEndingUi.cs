using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class TraditionalGameEndingUi : MonoBehaviour
{
	[SerializeField] Text resultText;

	protected void OnEnable()
	{
		float[] results = BoardUtility.GetPlayerAreasByDominance(Board.Current, Match.Current.PlayerCount);
		List<string> lines = new();

		lines.Add(string.Join("　", results.Select(
			(float area, int i) => $"{Match.Current.PlayerInfos[i].name}：{area.ToString("F2")} 目"
		)));

		float maxArea = results.Max();
		var winners = results
			.Select((float area, int i) => (area, i))
			.Where(pair => pair.area >= maxArea)
			.Select(pair => pair.i)
			.ToArray();
		if(winners.Length == results.Length)
			lines.Add("平局");
		else
			lines.Add($"{string.Join("、", winners.Select(i => Match.Current.PlayerInfos[i].name))}胜");

		resultText.text = string.Join("\n", lines);
	}
}
