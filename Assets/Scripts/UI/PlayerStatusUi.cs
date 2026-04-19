using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PlayerStatusUi : MonoBehaviour
{
	Match Match => Match.Current;

	#region Unity life cycle
	protected void Awake()
	{
		Match.OnStateChanged += RefreshAreas;
		Match.OnCurrentPlayerChanged += HighlightCurrentPlayer;
	}

	protected void Start()
	{
		RebuildRows();
		RefreshAreas();
		HighlightCurrentPlayer(Match.CurrentPlayerIndex);
	}

	protected void OnDestroy()
	{
		if(Match != null)
		{
			Match.OnStateChanged -= RefreshAreas;
			Match.OnCurrentPlayerChanged -= HighlightCurrentPlayer;
		}
	}
	#endregion

	#region Life cycle
	void RebuildRows()
	{
		for(int count = transform.childCount, i = count; i > 0; --i)
			Destroy(transform.GetChild(i - 1).gameObject);
		rows.Clear();

		for(int i = 0; i < Match.PlayerCount; ++i)
		{
			GameObject rowGo = Instantiate(rowPrefab, transform);
			PlayerStatusRow row = rowGo.GetComponent<PlayerStatusRow>();
			row.gameObject.name = $"PlayerRow{i}";
			row.Name = Match.PlayerInfos[i].name;
			row.Color = Match.PlayerInfos[i].color;
			rows.Add(row);
		}
	}

	void RefreshAreas()
	{
		if(rows.Count == 0)
			return;

		int[] areaByPlayer = Match.Board.GetPlayerAreaPixelsByDominance();
		float total = Match.Board.ComputeResolution * Match.Board.ComputeResolution;
		for(int i = 0; i < rows.Count; ++i)
		{
			rows[i].Name = Match.PlayerInfos[i].name;
			rows[i].AreaValue = total > 0 ? areaByPlayer[i] / total : 0;
		}
	}
	#endregion

	#region Players
	[SerializeField] GameObject rowPrefab;
	readonly List<PlayerStatusRow> rows = new();

	void HighlightCurrentPlayer(int currentPlayer)
	{
		for(int i = 0; i < rows.Count; ++i)
			rows[i].IsCurrent = i == currentPlayer;
	}
	#endregion
}
