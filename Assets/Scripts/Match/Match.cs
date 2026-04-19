using UnityEngine;
using System;

public abstract class Match : MonoBehaviour
{
	public static Match Current { get; private set; }
	public static Match Get<T>() where T : Match
		=> Current as T;

	#region References
	public Board Board => GameManager.Instance.Board;
	public BoardState State => Board.State;
	#endregion

	#region Unity life cycle
	protected void Awake()
	{
		Current = this;

		gameObject.AddComponent<MatchInput>();
	}

	protected void Start()
	{
		uiGo = MakeUi();
		CurrentPlayerIndex = 0;
	}

	protected void OnDestroy()
	{
		if(uiGo != null)
		{
			Destroy(uiGo);
			uiGo = null;
		}
	}
	#endregion

	#region UI
	GameObject uiGo;

	protected abstract GameObject MakeUi();
	#endregion

	#region Current player
	[SerializeField] int currentPlayerIndex = 0;
	public int CurrentPlayerIndex
	{
		get => currentPlayerIndex % Board.PlayerCount;
		set
		{
			int playerCount = Mathf.Max(1, Board.PlayerCount);
			currentPlayerIndex = ((value % playerCount) + playerCount) % playerCount;
			Board.RefreshRendering();
			StateCommitted?.Invoke();
			CurrentPlayerChanged?.Invoke(currentPlayerIndex);
		}
	}
	public event Action<int> CurrentPlayerChanged;
	#endregion

	#region Preview
	public event Action StateCommitted;

	public void ClearPreview()
	{
		Board.ClearPreview();
	}
	#endregion

	#region Stone placement
	public bool TryPlaceStone(Vector2 logicalPosition, float strength = 1)
	{
		if(!Board.TryPlaceStone(currentPlayerIndex, logicalPosition, out int nextPlayerIndex, strength))
			return false;

		currentPlayerIndex = nextPlayerIndex;
		StateCommitted?.Invoke();
		CurrentPlayerChanged?.Invoke(currentPlayerIndex);
		return true;
	}

	public bool TryPreviewStone(Vector2 logicalPosition, float strength = 1)
	{
		return Board.TryPreviewStone(currentPlayerIndex, logicalPosition, strength);
	}
	#endregion
}
