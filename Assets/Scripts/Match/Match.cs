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

		input = gameObject.AddComponent<MatchInput>();

		input.OnCursorEnter += OnCursorEnter;
		input.OnCursorMove += OnCursorMove;
		input.OnCursorExit += OnCursorExit;
		input.OnPlace += OnPlace;
		input.OnRemove += OnRemove;
		input.OnPass += OnPass;
	}

	protected void Start()
	{
		ui = MakeUi();
		CurrentPlayerIndex = 0;
	}

	protected void OnDestroy()
	{
		if(input != null)
		{
			input.OnCursorEnter -= OnCursorEnter;
			input.OnCursorMove -= OnCursorMove;
			input.OnCursorExit -= OnCursorExit;
			input.OnPlace -= OnPlace;
			input.OnRemove -= OnRemove;
			input.OnPass -= OnPass;

			input = null;
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

	protected bool LastPlacementSucceed { get; private set; } = false;
	#endregion

	#region Input
	MatchInput input;
	protected MatchInput Input => input;
	public bool InputEnabled
	{
		get => Input.enabled;
		set => Input.enabled = value;
	}

	protected virtual void OnCursorEnter(Vector2 position)
	{
		TryPreviewStone(position);
	}

	protected virtual void OnCursorMove(Vector2 position)
	{
		TryPreviewStone(position);
	}

	protected virtual void OnCursorExit()
	{
		Board.Current.ClearPreview();
	}

	protected virtual void OnPlace(Vector2 position)
	{
		Board board = Board.Current;
		if(board == null)
			return;

		LastPlacementSucceed = board.State.TryPlaceStone(
			currentPlayerIndex,
			position,
			GetChainStats,
			GetChainLabelAtLogicalPosition,
			GetStoneChainLabels,
			out BoardState nextState
		);
		if(!LastPlacementSucceed)
			return;

		board.SetState(nextState);
		board.ClearPreview();
		onStateChanged?.Invoke();
	}

	protected virtual void OnRemove(Vector2 position) { }

	protected virtual void OnPass() { }

	bool TryPreviewStone(Vector2 position)
	{
		Board board = Board.Current;
		if(board == null)
			return false;

		if(board.State.HasStoneOverlap(position))
		{
			board.ClearPreview();
			return false;
		}

		if(!board.State.PeekStonePlacement(currentPlayerIndex, position, out BoardState previewState))
		{
			board.ClearPreview();
			return false;
		}

		board.ShowPreview(previewState);
		return true;
	}

	List<BoardUtility.ChainStat> GetChainStats(BoardState renderState)
	{
		AnalyzeState(renderState);
		Board board = Board.Current;
		if(board == null || board.Caches == null)
			return new List<BoardUtility.ChainStat>();
		return BoardUtility.GetChainStats(board.Caches);
	}

	int GetChainLabelAtLogicalPosition(BoardState renderState, Vector2 position)
	{
		AnalyzeState(renderState);
		Board board = Board.Current;
		if(board == null || board.Caches == null)
			return -1;
		return BoardUtility.GetChainLabelAtLogicalPosition(board.Caches, renderState, position);
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

	protected void StepPlayerIndex()
	{
		CurrentPlayerIndex = (CurrentPlayerIndex + 1) % PlayerCount;
	}
	#endregion
}
