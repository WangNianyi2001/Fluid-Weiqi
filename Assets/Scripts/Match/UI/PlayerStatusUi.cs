using UnityEngine;
using System.Collections.Generic;

public class PlayerStatusUi : MonoBehaviour
{
	Match Match => Match.Current;

	#region Unity life cycle
	protected void Awake()
	{
		Match.OnStateChanged += RefreshAreas;
		Match.OnPlayerPassStateChanged += OnPassStateChanged;
		Match.OnPlayerMoveRightChanged += OnPlayerMoveRightChanged;
	}

	protected void Start()
	{
		RebuildRows();
		RefreshAreas();
		RefreshPassStates();
		RefreshActivePlayers();
	}

	protected void OnDestroy()
	{
		if(Match != null)
		{
			Match.OnStateChanged -= RefreshAreas;
			Match.OnPlayerPassStateChanged -= OnPassStateChanged;
			Match.OnPlayerMoveRightChanged -= OnPlayerMoveRightChanged;
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
			row.IsPassed = false;
			rows.Add(row);
		}
	}

	void RefreshAreas()
	{
		if(rows.Count == 0 || Board.Current == null)
			return;

		Color[] playerColors = new Color[Mathf.Min(Match.PlayerCount, BoardUtility.MaxPlayers)];
		for(int i = 0; i < playerColors.Length; ++i)
			playerColors[i] = Match.PlayerInfos[i].color;
		BoardUtility.RenderAnalysis(Board.Current.Caches, Board.Current.State, playerColors);

		float[] areaByPlayer = BoardUtility.GetPlayerAreasByDominance(Board.Current, Match.PlayerCount);
		float total = Mathf.Pow(Board.Current.State.Size, 2);
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

	void RefreshActivePlayers()
	{
		var moveRights = Match.PlayerMoveRights;
		bool hasAnyActive = false;
		for(int i = 0; i < rows.Count; ++i)
		{
			bool isActive = moveRights != null && moveRights.TryGetValue(i, out bool canMove) && canMove;
			rows[i].IsCurrent = isActive;
			hasAnyActive |= isActive;
		}

		if(hasAnyActive)
			return;

		int currentPlayer = Match.CurrentPlayerIndex;
		for(int i = 0; i < rows.Count; ++i)
			rows[i].IsCurrent = i == currentPlayer;
	}

	void OnPlayerMoveRightChanged()
	{
		RefreshActivePlayers();
	}

	void RefreshPassStates()
	{
		var passStates = Match.PlayerPassStates;
		for(int i = 0; i < rows.Count; ++i)
		{
			bool passed = passStates != null && passStates.TryGetValue(i, out bool value) && value;
			rows[i].IsPassed = passed;
		}
	}

	void OnPassStateChanged()
	{
		RefreshPassStates();
	}
	#endregion
}
