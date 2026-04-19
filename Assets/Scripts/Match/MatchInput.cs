using UnityEngine;

public class MatchInput : MonoBehaviour
{
	const float PreviewPositionEpsilon = 0.0001f;

	[SerializeField] private Camera inputCamera;
	[SerializeField] private LayerMask raycastMask = Physics.DefaultRaycastLayers;
	[SerializeField] private float placementStrength = 1;

	Match match;
	bool hasPreviewPosition;
	Vector2 lastPreviewPosition;

	protected void Awake()
	{
		match = GetComponent<Match>();
		if(inputCamera == null)
			inputCamera = Camera.main;
	}

	protected void Update()
	{
		if(inputCamera == null)
		{
			match.ClearPreview();
			return;
		}

		if(!TryGetBoardHit(out RaycastHit hit))
		{
			match.ClearPreview();
			hasPreviewPosition = false;
			return;
		}

		Vector2 logicalPosition = match.Board.WorldToLogicalPosition(hit.point);
		bool freePlace = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
		if(!freePlace)
		{
			float maxCoord = match.Board.Size - 1;
			logicalPosition = new Vector2(
				Mathf.Clamp(Mathf.Round(logicalPosition.x), 0, maxCoord),
				Mathf.Clamp(Mathf.Round(logicalPosition.y), 0, maxCoord)
			);
		}
		bool needPreviewRefresh = !hasPreviewPosition || (logicalPosition - lastPreviewPosition).sqrMagnitude > PreviewPositionEpsilon;
		if(needPreviewRefresh)
		{
			bool previewSucceeded = match.TryPreviewStone(logicalPosition, placementStrength);
			if(previewSucceeded)
			{
				lastPreviewPosition = logicalPosition;
				hasPreviewPosition = true;
			}
			else
			{
				hasPreviewPosition = false;
			}
		}

		if(Input.GetMouseButtonDown(0))
		{
			if(match.TryPlaceStone(logicalPosition, placementStrength))
				hasPreviewPosition = false;
		}
	}

	bool TryGetBoardHit(out RaycastHit hit)
	{
		Vector3 mousePosition = Input.mousePosition;
		if(!float.IsNormal(mousePosition.sqrMagnitude))
		{
			hit = default;
			return false;
		}
		Ray ray = inputCamera.ScreenPointToRay(mousePosition);
		if(!Physics.Raycast(ray, out hit, Mathf.Infinity, raycastMask, QueryTriggerInteraction.Ignore))
			return false;

		return hit.collider.transform.IsChildOf(match.Board.transform);
	}
}
