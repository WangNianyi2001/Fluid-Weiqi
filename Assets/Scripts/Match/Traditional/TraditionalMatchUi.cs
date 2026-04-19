using UnityEngine;
using System.Collections.Generic;

public class TraditionalMatchUi : MonoBehaviour
{
	#region References
	[SerializeField] Match match;
	#endregion

	#region Unity life cycle
	void Awake()
	{
		match = transform.GetComponentInParent<Match>();
	}

	void OnEnable()
	{
		if(match == null)
			match = GetComponent<Match>();

		RebuildRows();

		match.StateCommitted += RefreshAreas;
		match.CurrentPlayerChanged += HighlightCurrentPlayer;

		RefreshAreas();
		HighlightCurrentPlayer(match.CurrentPlayerIndex);
	}

	void OnDisable()
	{
		if(match == null)
			return;

		match.StateCommitted -= RefreshAreas;
		match.CurrentPlayerChanged -= HighlightCurrentPlayer;
	}
	#endregion

	#region Life cycle
	void RebuildRows()
	{
		if(playerListRoot == null || playerRowPrefab == null)
		{
			Debug.LogError("GameUi requires playerListRoot and playerRowPrefab references.", this);
			return;
		}

		for(int count = playerListRoot.childCount, i = count; i > 0; --i)
			Destroy(playerListRoot.GetChild(i - 1).gameObject);
		rows.Clear();

		for(int i = 0; i < match.Board.PlayerCount; ++i)
		{
			GameObject rowGo = Instantiate(playerRowPrefab, playerListRoot);
			PlayerStatusUi row = rowGo.GetComponent<PlayerStatusUi>();
			row.gameObject.name = $"PlayerRow{i}";
			row.SetPlayerName(GetPlayerName(i));
			row.SetPlayerColor(GetPlayerColor(i));
			rows.Add(row);
		}
	}

	void RefreshAreas()
	{
		if(rows.Count == 0)
			return;

		int[] areaByPlayer = match.Board.GetPlayerAreaPixelsByDominance();
		float total = match.Board.ComputeResolution * match.Board.ComputeResolution;
		for(int i = 0; i < rows.Count; ++i)
		{
			int percentage = total > 0 ? Mathf.RoundToInt(areaByPlayer[i] / total * 100f) : 0;
			rows[i].SetPlayerName(GetPlayerName(i));
			rows[i].SetAreaValue(percentage);
		}
	}
	#endregion

	#region Players
	[SerializeField] private RectTransform playerListRoot;
	[SerializeField] private GameObject playerRowPrefab;
	[SerializeField] private string[] playerNames = new[] { "黑", "白", "红", "蓝" };
	readonly List<PlayerStatusUi> rows = new();

	void HighlightCurrentPlayer(int currentPlayer)
	{
		for(int i = 0; i < rows.Count; ++i)
			rows[i].SetCurrentTurn(i == currentPlayer);
	}

	Color GetPlayerColor(int player)
	{
		if(player < match.Board.PlayerColors.Count)
			return match.Board.PlayerColors[player];
		return Color.gray;
	}

	string GetPlayerName(int player)
	{
		if(player < playerNames.Length && !string.IsNullOrWhiteSpace(playerNames[player]))
			return playerNames[player];
		return $"Player {player + 1}";
	}
	#endregion
}
