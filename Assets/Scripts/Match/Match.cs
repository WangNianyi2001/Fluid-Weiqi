using UnityEngine;
using System;
using System.Collections.Generic;

public abstract class Match : MonoBehaviour
{
	public static Match Current { get; private set; }
	public static Match Get<T>() where T : Match
		=> Current as T;

	#region Unity life cycle
	protected void Awake()
	{
		Current = this;

		matchInput = gameObject.AddComponent<MatchInput>();

		matchInput.OnCursorEnter += OnCursorEnter;
		matchInput.OnCursorMove += OnCursorMove;
		matchInput.OnCursorExit += OnCursorExit;
		matchInput.OnPlace += OnPlace;
		matchInput.OnPass += OnPass;
	}

	protected void Start()
	{
		ui = MakeUi();
		CurrentPlayerIndex = 0;
	}

	protected void OnDestroy()
	{
		if(matchInput != null)
		{
			matchInput.OnCursorEnter -= OnCursorEnter;
			matchInput.OnCursorMove -= OnCursorMove;
			matchInput.OnCursorExit -= OnCursorExit;
			matchInput.OnPlace -= OnPlace;
			matchInput.OnPass -= OnPass;

			matchInput = null;
		}

		if(ui != null)
		{
			Destroy(ui);
			ui = null;
		}
	}
	#endregion

	#region Board
	protected Action onStateChanged;
	public event Action OnStateChanged
	{
		add => onStateChanged += value;
		remove => onStateChanged -= value;
	}
	#endregion

	#region Input
	MatchInput matchInput;

	protected virtual void OnCursorEnter(Vector2 logicalPosition)
	{
		TryPreviewStone(logicalPosition);
	}

	protected virtual void OnCursorMove(Vector2 logicalPosition)
	{
		TryPreviewStone(logicalPosition);
	}

	protected virtual void OnCursorExit()
	{
		Board.Current.ClearPreview();
	}

	protected virtual void OnPlace(Vector2 logicalPosition)
	{
		Board board = Board.Current;
		if(board == null)
			return;

		if(!board.State.TryPlaceStone(
			currentPlayerIndex,
			logicalPosition,
			IsOccupiedAtLogicalPosition,
			GetChainStats,
			GetChainLabelAtLogicalPosition,
			GetStoneChainLabels,
			out BoardState nextState))
			return;

		board.SetState(nextState);
		board.ClearPreview();
		onStateChanged?.Invoke();
	}

	protected virtual void OnPass() { }

	bool TryPreviewStone(Vector2 logicalPosition)
	{
		Board board = Board.Current;
		if(board == null)
			return false;

		if(IsOccupiedAtLogicalPosition(board.State, logicalPosition))
		{
			board.ClearPreview();
			return false;
		}

		if(!board.State.PeekStonePlacement(currentPlayerIndex, logicalPosition, out BoardState previewState))
		{
			board.ClearPreview();
			return false;
		}

		board.ShowPreview(previewState);
		return true;
	}

	bool IsOccupiedAtLogicalPosition(BoardState renderState, Vector2 logicalPosition)
	{
		AnalyzeState(renderState);
		Board board = Board.Current;
		if(board == null || board.Caches == null)
			return false;
		return BoardUtility.IsOccupiedAtLogicalPosition(board.Caches, renderState, logicalPosition);
	}

	List<BoardUtility.ChainStat> GetChainStats(BoardState renderState)
	{
		AnalyzeState(renderState);
		Board board = Board.Current;
		if(board == null || board.Caches == null)
			return new List<BoardUtility.ChainStat>();
		return BoardUtility.GetChainStats(board.Caches);
	}

	int GetChainLabelAtLogicalPosition(BoardState renderState, Vector2 logicalPosition)
	{
		AnalyzeState(renderState);
		Board board = Board.Current;
		if(board == null || board.Caches == null)
			return -1;
		return BoardUtility.GetChainLabelAtLogicalPosition(board.Caches, renderState, logicalPosition);
	}

	List<List<int>> GetStoneChainLabels(BoardState renderState)
	{
		AnalyzeState(renderState);
		Board board = Board.Current;
		if(board == null || board.Caches == null)
			return new List<List<int>>();
		return BoardUtility.GetStoneChainLabels(board.Caches, renderState);
	}

	void AnalyzeState(BoardState renderState)
	{
		Board board = Board.Current;
		if(renderState == null || board == null || board.Caches == null || !board.Caches.isInitialized)
			return;

		Color[] playerColors = new Color[Mathf.Min(PlayerCount, BoardUtility.MaxPlayers)];
		for(int i = 0; i < playerColors.Length; ++i)
			playerColors[i] = PlayerInfos[i].color;
		BoardUtility.RenderAnalysis(board.Caches, renderState, playerColors);
	}
	#endregion

	#region UI
	GameObject ui;

	protected abstract GameObject MakeUi();
	#endregion

	#region Players
	public IReadOnlyList<PlayerInfo> PlayerInfos { get; set; }
	public int PlayerCount => PlayerInfos.Count;

	int currentPlayerIndex = 0;
	public int CurrentPlayerIndex
	{
		get => currentPlayerIndex % PlayerCount;
		protected set
		{
			int playerCount = Mathf.Max(1, PlayerCount);
			currentPlayerIndex = ((value % playerCount) + playerCount) % playerCount;
			onCurrentPlayerChanged?.Invoke(currentPlayerIndex);
		}
	}

	protected Action<int> onCurrentPlayerChanged;
	public event Action<int> OnCurrentPlayerChanged
	{
		add => onCurrentPlayerChanged += value;
		remove => onCurrentPlayerChanged -= value;
	}
	#endregion
}
