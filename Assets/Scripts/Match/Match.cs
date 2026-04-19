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
		Board.Current.TryPreviewStone(currentPlayerIndex, logicalPosition);
	}

	protected virtual void OnCursorMove(Vector2 logicalPosition)
	{
		Board.Current.TryPreviewStone(currentPlayerIndex, logicalPosition);
	}

	protected virtual void OnCursorExit()
	{
		Board.Current.ClearPreview();
	}

	protected virtual void OnPlace(Vector2 logicalPosition)
	{
		if(!Board.Current.TryPlaceStone(currentPlayerIndex, logicalPosition))
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
