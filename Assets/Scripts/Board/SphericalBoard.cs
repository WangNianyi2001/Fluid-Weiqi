using UnityEngine;

/// <summary>
/// Spherical (lat-long) board.
///
/// Coordinate conventions:
///   boardLocal  — (longitude/2π, latitude/π), both in [-0.5, 0.5]
///   absolute    — (ix, iy) where ix ∈ [0, 2N) (longitude), iy ∈ [0, N) (latitude), N = boardSize
///   sphere mesh — unit sphere in local space (radius = 0.5); scale the GameObject for desired world size
///   seam        — at longitude = ±π (boardLocal.x = ±0.5); ix wraps: 2N-1 neighbours 0
/// </summary>
public class SphericalBoard : Board
{
	const float TwoPi = Mathf.PI * 2f;
	const float SphereLocalRadius = 0.5f;
	const float RotateSpeed = 180f;

	// ── Topology ─────────────────────────────────────────────────────────

	public override BoardUtility.BoardTopology Topology => BoardUtility.BoardTopology.Sphere;

	/// <summary>
	/// Sphere snap: x wraps in [0, 2N), y clamps in [0, N-1].
	/// </summary>
	public override Vector2 NormalizeAbsolutePosition(Vector2 absolutePosition)
	{
		float N    = State.Size;
		float absX = Mathf.Repeat(Mathf.Round(absolutePosition.x), 2f * N);
		float absY = Mathf.Clamp(Mathf.Round(absolutePosition.y), 0f, N - 1f);
		return new Vector2(absX, absY);
	}

	protected void Update()
	{
		if(!Input.GetMouseButton(1))
			return;

		float dx = Input.GetAxisRaw("Mouse X");
		float dy = Input.GetAxisRaw("Mouse Y");
		if(Mathf.Approximately(dx, 0f) && Mathf.Approximately(dy, 0f))
			return;

		transform.Rotate(Vector3.up, dx * RotateSpeed * Time.unscaledDeltaTime, Space.World);
		transform.Rotate(transform.right, -dy * RotateSpeed * Time.unscaledDeltaTime, Space.World);
	}

	// ── Coordinate conversion ────────────────────────────────────────────

	public override Bounds GetWorldBounds()
	{
		if(BoardRenderer != null)
			return BoardRenderer.bounds;
		return new Bounds(transform.position, Vector3.zero);
	}

	/// <summary>
	/// World position on sphere surface → boardLocal (longitude/2π, latitude/π).
	/// </summary>
	public override Vector2 WorldToBoardLocalPosition(Vector3 worldPosition)
	{
		Vector3 local = transform.InverseTransformPoint(worldPosition).normalized;
		float theta = Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f));
		float phi   = Mathf.Atan2(local.x, local.z);          // ∈ (-π, π]
		return new Vector2(phi / TwoPi, theta / Mathf.PI);    // ∈ (-0.5, 0.5]
	}

	/// <summary>
	/// boardLocal → world position on sphere surface.
	/// </summary>
	public override Vector3 BoardLocalToWorldPosition(Vector2 boardLocalPosition)
	{
		float phi   = boardLocalPosition.x * TwoPi;
		float theta = boardLocalPosition.y * Mathf.PI;
		float cosT  = Mathf.Cos(theta);
		Vector3 sphereLocal = new Vector3(
			cosT * Mathf.Sin(phi),
			Mathf.Sin(theta),
			cosT * Mathf.Cos(phi)
		) * SphereLocalRadius;
		return transform.TransformPoint(sphereLocal);
	}

	/// <summary>
	/// boardLocal → absolute grid index (ix, iy).
	/// ix ∈ [0, 2N), iy ∈ [0, N).
	/// </summary>
	public override Vector2 BoardLocalToAbsolutePosition(Vector2 boardLocalPosition)
	{
		float n = State.Size;
		float absX = (boardLocalPosition.x + 0.5f) * 2f * n;
		float absY = (boardLocalPosition.y + 0.5f) * n;
		return new Vector2(absX, absY);
	}

	/// <summary>
	/// Absolute grid index → boardLocal.
	/// </summary>
	public override Vector2 AbsoluteToBoardLocalPosition(Vector2 absolutePosition)
	{
		float n = State.Size;
		float u = absolutePosition.x / (2f * n) - 0.5f;
		float v = absolutePosition.y / n - 0.5f;
		return new Vector2(u, v);
	}

	// ── Grid material ────────────────────────────────────────────────────

	protected override void UpdateGridMaterialParameters()
	{
		base.UpdateGridMaterialParameters();

		if(GridMaterial == null)
			return;

		int boardSize = Mathf.Max(2, Mathf.RoundToInt(State.Size));
		GridMaterial.SetFloat("_BoardSize", boardSize);
		GridMaterial.SetFloat("_ShowStarPoints", 0f);
		GridMaterial.SetFloat("_ShowGridLines", 0f);
	}
}
