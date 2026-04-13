using UnityEngine;

[RequireComponent(typeof(GameInput))]
public class Game : MonoBehaviour
{
	[SerializeField] Board board;
	[SerializeField] int currentPlayerIndex = 0;
	BoardState previewState;
	bool hasPreview;

	public int CurrentPlayerIndex => currentPlayerIndex;
	public Board Board => board;
	public BoardState State => board.State;

	void Start()
	{
		board.RefreshRendering();
	}

	public bool TryPlaceStone(Vector2 logicalPosition, float strength = 1)
	{
		if(!State.PlaceStone(currentPlayerIndex, logicalPosition, strength))
			return false;

		hasPreview = false;
		previewState = null;
		board.RefreshRendering();

		currentPlayerIndex = (currentPlayerIndex + 1) % board.PlayerCount;
		return true;
	}

	public bool TryPreviewStone(Vector2 logicalPosition, float strength = 1)
	{
		if(!State.PeekStonePlacement(currentPlayerIndex, logicalPosition, out BoardState newState, strength))
		{
			ClearPreview();
			return false;
		}

		hasPreview = true;
		previewState = newState;
		board.RefreshRendering(previewState);
		return true;
	}

	public void ClearPreview()
	{
		if(!hasPreview)
			return;

		hasPreview = false;
		previewState = null;
		board.RefreshRendering();
	}
}
