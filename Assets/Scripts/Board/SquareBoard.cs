using UnityEngine;

public class SquareBoard : Board
{
	public override Bounds GetWorldBounds()
	{
		if(BoardRenderer != null)
			return BoardRenderer.bounds;

		return new Bounds(transform.position, Vector3.zero);
	}

	public override Vector2 WorldToBoardLocalPosition(Vector3 worldPosition)
	{
		Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
		return new Vector2(localPosition.x, localPosition.y);
	}

	public override Vector3 BoardLocalToWorldPosition(Vector2 boardLocalPosition)
	{
		return transform.TransformPoint(new Vector3(boardLocalPosition.x, boardLocalPosition.y, 0));
	}

	public override Vector2 BoardLocalToAbsolutePosition(Vector2 boardLocalPosition)
	{
		float span = State.BoardStateExtent;
		return new Vector2((boardLocalPosition.x + .5f) * span, (boardLocalPosition.y + .5f) * span);
	}

	public override Vector2 AbsoluteToBoardLocalPosition(Vector2 absolutePosition)
	{
		float span = State.BoardStateExtent;
		return new Vector2(
			absolutePosition.x / span - .5f,
			absolutePosition.y / span - .5f
		);
	}

	protected override void UpdateGridMaterialParameters()
	{
		base.UpdateGridMaterialParameters();

		if(GridMaterial == null)
			return;

		int boardSize = Mathf.Max(2, Mathf.RoundToInt(State.Size));
		GridMaterial.SetFloat("_BoardSize", boardSize);
		GridMaterial.SetFloat("_StarEdgeOffset", BoardUtility.GetStarEdgeOffset(boardSize));
	}
}
