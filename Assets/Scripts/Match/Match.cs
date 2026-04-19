using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

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

		if(Board != null)
		{
			Destroy(Board.gameObject);
			Board = null;
		}

		if(ui != null)
		{
			Destroy(ui);
			ui = null;
		}
	}
	#endregion

	#region Life cycle
	public void Construct(int boardSize, IReadOnlyList<PlayerInfo> playerInfos)
	{
		PlayerInfos = playerInfos.ToArray();

		Board = Instantiate(Resources.Load<GameObject>("Prefabs/Board"), transform).GetComponent<Board>();
		Board.PlayerCount = PlayerCount;
		Board.Size = boardSize;
	}
	#endregion

	#region Board
	public Board Board { get; private set; }
	public BoardState State => Board.State;

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

	#region Players
	public IReadOnlyList<PlayerInfo> PlayerInfos { get; private set; }
	public int PlayerCount => PlayerInfos.Count;
	public IReadOnlyList<Color> PlayerColors => PlayerInfos.Select(i => i.color).ToArray();

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
