using UnityEngine;
using System;
using System.Collections.Generic;

public enum MatchMode
{
	Traditional = 1,
	Training = 0xffff,
}

[System.Serializable]
public struct MatchRule
{
	public MatchMode mode;
	public int boardSize;
	public float stoneHardness;
}

public struct PlayerInfo
{
	public string name;
	public Color color;
}

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

		BoardState currentState = board.State;
		AnalyzeState(currentState);

		LastPlacementSucceed = BoardUtility.TryPlaceStoneStandard(
			board.Caches, currentState, currentPlayerIndex, position, out BoardState nextState);
		if(!LastPlacementSucceed)
			return;

		if(AudioManager.Instance != null)
			AudioManager.Instance.PlayPlaceStoneSound();

		int capturedStoneCount = CountCapturedStones(currentState, nextState, currentPlayerIndex);
		if(capturedStoneCount > 0 && AudioManager.Instance != null)
			AudioManager.Instance.PlayCaptureSound();

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

		BoardState state = board.State;

		if(IsOccupiedAtLogicalPosition(board, state, position))
		{
			board.ClearPreview();
			return false;
		}

		if(position.x < 0 || position.x >= state.Size || position.y < 0 || position.y >= state.Size)
		{
			board.ClearPreview();
			return false;
		}

		BoardState previewState = new(state);
		previewState.AddStone(currentPlayerIndex, position);
		board.ShowPreview(previewState);
		return true;
	}

	bool IsOccupiedAtLogicalPosition(Board board, BoardState renderState, Vector2 position)
	{
		AnalyzeState(renderState);
		if(board == null || board.Caches == null)
			return false;
		return BoardUtility.IsOccupiedAtLogicalPosition(board.Caches, renderState, position);
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

	int CountCapturedStones(BoardState oldState, BoardState newState, int placedPlayer)
	{
		int oldOpponentTotal = CountOpponentStones(oldState, placedPlayer);
		int newOpponentTotal = CountOpponentStones(newState, placedPlayer);
		return Mathf.Max(0, oldOpponentTotal - newOpponentTotal);
	}

	int CountOpponentStones(BoardState state, int excludedPlayer)
	{
		int count = 0;
		for(int player = 0; player < state.PlayerCount; ++player)
		{
			if(player == excludedPlayer)
				continue;

			var stones = state.GetStones(player);
			count += stones.Count;
		}
		return count;
	}
	#endregion
}
