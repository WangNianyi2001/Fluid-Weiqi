using UnityEngine;
using System;

public class MatchInput : MonoBehaviour
{
	const float PreviewPositionEpsilon = 1e-3f;

	Camera Camera => GameManager.Instance != null ? GameManager.Instance.Camera : null;
	Board Board => Match.Current.Board;
	LayerMask RaycastMask => Physics.DefaultRaycastLayers;
	[SerializeField] private KeyCode passKey = KeyCode.None;

	bool hasCursorPosition;
	Vector2 lastCursorPosition;

	public event Action<Vector2> OnCursorEnter;
	public event Action<Vector2> OnCursorMove;
	public event Action OnCursorExit;
	public event Action<Vector2> OnPlace;
	public event Action OnPass;

	protected void Update()
	{
		if(Camera == null || Board == null)
		{
			EmitCursorExitIfNeeded();
			return;
		}

		if(!TryGetBoardHit(out RaycastHit hit))
		{
			EmitCursorExitIfNeeded();
			return;
		}

		Vector2 logicalPosition = Board.WorldToLogicalPosition(hit.point);
		bool freePlace = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
		if(!freePlace)
		{
			float maxCoord = Board.Size - 1;
			logicalPosition = new Vector2(
				Mathf.Clamp(Mathf.Round(logicalPosition.x), 0, maxCoord),
				Mathf.Clamp(Mathf.Round(logicalPosition.y), 0, maxCoord)
			);
		}

		if(!hasCursorPosition)
		{
			hasCursorPosition = true;
			lastCursorPosition = logicalPosition;
			OnCursorEnter?.Invoke(logicalPosition);
			OnCursorMove?.Invoke(logicalPosition);
		}
		else if((logicalPosition - lastCursorPosition).sqrMagnitude > PreviewPositionEpsilon)
		{
			lastCursorPosition = logicalPosition;
			OnCursorMove?.Invoke(logicalPosition);
		}

		if(Input.GetMouseButtonDown(0))
		{
			OnPlace?.Invoke(logicalPosition);
			OnCursorMove?.Invoke(logicalPosition);
		}

		if(passKey != KeyCode.None && Input.GetKeyDown(passKey))
			OnPass?.Invoke();
	}

	public void EmitPass()
	{
		OnPass?.Invoke();
	}

	void EmitCursorExitIfNeeded()
	{
		if(!hasCursorPosition)
			return;

		hasCursorPosition = false;
		OnCursorExit?.Invoke();
	}

	bool TryGetBoardHit(out RaycastHit hit)
	{
		if(Board == null)
		{
			hit = default;
			return false;
		}

		Vector3 mousePosition = Input.mousePosition;
		if(!float.IsNormal(mousePosition.sqrMagnitude))
		{
			hit = default;
			return false;
		}
		Ray ray = Camera.ScreenPointToRay(mousePosition);
		if(!Physics.Raycast(ray, out hit, Mathf.Infinity, RaycastMask, QueryTriggerInteraction.Ignore))
			return false;

		return hit.collider.transform.IsChildOf(Board.transform);
	}
}
