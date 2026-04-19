using UnityEngine;
using System;

public abstract class Match : MonoBehaviour
{
	public static Match Current { get; private set; }
	public static Match Get<T>() where T : Match
		=> Current as T;

	#region References
	public Board Board => GameManager.Instance.Board;
	#endregion

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

	#region Input
	MatchInput matchInput;

	protected virtual void OnCursorEnter(Vector2 logicalPosition)
	{
		Board.TryPreviewStone(currentPlayerIndex, logicalPosition);
	}

	protected virtual void OnCursorMove(Vector2 logicalPosition)
	{
		Board.TryPreviewStone(currentPlayerIndex, logicalPosition);
	}

	protected virtual void OnCursorExit()
	{
		Board.ClearPreview();
	}

	protected virtual void OnPlace(Vector2 logicalPosition)
	{
		if(!Board.TryPlaceStone(currentPlayerIndex, logicalPosition))
			return;
		onStateChanged?.Invoke();
	}

	protected virtual void OnPass() { }
	#endregion

	#region UI
	GameObject ui;

	protected abstract GameObject MakeUi();
	#endregion

	#region Current player
	int currentPlayerIndex = 0;
	public int CurrentPlayerIndex
	{
		get => currentPlayerIndex % Board.PlayerCount;
		protected set
		{
			int playerCount = Mathf.Max(1, Board.PlayerCount);
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

	#region State
	public BoardState State => Board.State;

	protected Action onStateChanged;
	public event Action OnStateChanged
	{
		add => onStateChanged += value;
		remove => onStateChanged -= value;
	}
	#endregion
}
