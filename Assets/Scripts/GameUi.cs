using UnityEngine;
using System.Collections.Generic;

public class GameUi : MonoBehaviour
{
	[SerializeField] private RectTransform playerListRoot;
	[SerializeField] private GameObject playerRowPrefab;
	[SerializeField] private string[] playerNames = new[] { "Black", "White", "Red", "Blue" };

	[SerializeField] Game game;
	readonly List<PlayerStatusUi> rows = new();

	void OnEnable()
	{
		if(game == null)
			game = GetComponent<Game>();

		RebuildRows();

		game.StateCommitted += RefreshAreas;
		game.TurnChanged += HighlightCurrentPlayer;

		RefreshAreas();
		HighlightCurrentPlayer(game.CurrentPlayerIndex);
	}

	void OnDisable()
	{
		if(game == null)
			return;

		game.StateCommitted -= RefreshAreas;
		game.TurnChanged -= HighlightCurrentPlayer;
	}

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

		for(int i = 0; i < game.Board.PlayerCount; ++i)
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

		int[] areaByPlayer = game.GetPlayerAreaPixels();
		float total = game.Board.ComputeResolution * game.Board.ComputeResolution;
		for(int i = 0; i < rows.Count; ++i)
		{
			int percentage = total > 0 ? Mathf.RoundToInt(areaByPlayer[i] / total * 100f) : 0;
			rows[i].SetPlayerName(GetPlayerName(i));
			rows[i].SetAreaValue(percentage);
		}
	}

	void HighlightCurrentPlayer(int currentPlayer)
	{
		for(int i = 0; i < rows.Count; ++i)
			rows[i].SetCurrentTurn(i == currentPlayer);
	}

	Color GetPlayerColor(int player)
	{
		if(player < game.Board.PlayerColors.Count)
			return game.Board.PlayerColors[player];
		return Color.gray;
	}

	string GetPlayerName(int player)
	{
		if(player < playerNames.Length && !string.IsNullOrWhiteSpace(playerNames[player]))
			return playerNames[player];
		return $"Player {player + 1}";
	}
}
