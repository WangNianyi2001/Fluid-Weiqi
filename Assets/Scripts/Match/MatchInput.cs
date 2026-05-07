using UnityEngine;
using System;

public class MatchInput : MonoBehaviour
{
	const float PreviewPositionEpsilon = 1e-3f;

	Camera Camera => Camera.main;
	LayerMask RaycastMask => Physics.DefaultRaycastLayers;

	bool hasCursorPosition;
	Vector2 lastCursorPosition;

	bool shiftDown = false, capslocked = false;

	public event Action<Vector2> OnCursorEnter;
	public event Action<Vector2> OnCursorMove;
	public event Action OnCursorExit;
	public event Action<Vector2> OnPlace;
	public event Action<Vector2> OnRemove;
	public event Action OnPass;

	protected void Update()
	{
		ProcessKeyboard();
		ProcessMouse();
	}

	void ProcessKeyboard()
	{
		if(Input.GetKeyDown(KeyCode.CapsLock))
			capslocked = !capslocked;
		shiftDown = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

		if(Input.GetKeyDown(KeyCode.P))
			OnPass?.Invoke();
	}

	void ProcessMouse()
	{
		if(Camera == null || Board.Current == null)
		{
			EmitCursorExitIfNeeded();
			return;
		}

		if(!TryGetBoardHit(out RaycastHit hit))
		{
			EmitCursorExitIfNeeded();
			return;
		}

		Vector2 absolutePosition = Board.Current.WorldToAbsolutePosition(hit.point);
		bool freePlace = shiftDown ^ capslocked;
		if(!freePlace)
			absolutePosition = Board.Current.NormalizeAbsolutePosition(absolutePosition);

		if(!hasCursorPosition)
		{
			hasCursorPosition = true;
			lastCursorPosition = absolutePosition;
			OnCursorEnter?.Invoke(absolutePosition);
			OnCursorMove?.Invoke(absolutePosition);
		}
		else if((absolutePosition - lastCursorPosition).sqrMagnitude > PreviewPositionEpsilon)
		{
			lastCursorPosition = absolutePosition;
			OnCursorMove?.Invoke(absolutePosition);
		}

		if(Input.GetMouseButtonDown(0))
		{
			OnPlace?.Invoke(absolutePosition);
			OnCursorMove?.Invoke(absolutePosition);
		}
		if(Input.GetMouseButtonDown(1))
		{
			if(Board.Current.Topology == BoardUtility.BoardTopology.Sphere)
				return;
			OnRemove?.Invoke(absolutePosition);
			OnCursorMove?.Invoke(absolutePosition);
		}
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
		if(Board.Current == null)
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

		return hit.collider.transform.IsChildOf(Board.Current.transform);
	}
}
