using UnityEngine;

[RequireComponent(typeof(Game))]
public class GameInput : MonoBehaviour
{
	const float PreviewPositionEpsilon = 0.0001f;

	[SerializeField] private Camera inputCamera;
	[SerializeField] private LayerMask raycastMask = Physics.DefaultRaycastLayers;
	[SerializeField] private float placementStrength = 1;

	Game game;
	bool hasPreviewPosition;
	Vector2 lastPreviewPosition;

	void Awake()
	{
		game = GetComponent<Game>();
		if(inputCamera == null)
			inputCamera = Camera.main;
	}

	void Update()
	{
		if(inputCamera == null)
		{
			game.ClearPreview();
			return;
		}

		if(!TryGetBoardHit(out RaycastHit hit))
		{
			game.ClearPreview();
			hasPreviewPosition = false;
			return;
		}

		Vector2 logicalPosition = game.Board.WorldToLogicalPosition(hit.point);
		bool needPreviewRefresh = !hasPreviewPosition || (logicalPosition - lastPreviewPosition).sqrMagnitude > PreviewPositionEpsilon;
		if(needPreviewRefresh)
		{
			bool previewSucceeded = game.TryPreviewStone(logicalPosition, placementStrength);
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
			if(game.TryPlaceStone(logicalPosition, placementStrength))
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

		return hit.collider.transform.IsChildOf(game.Board.transform);
	}
}
