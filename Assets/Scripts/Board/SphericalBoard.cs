using UnityEngine;
using System.Collections.Generic;

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
	[SerializeField] Transform boardAnchor;
	MatchInput input;

	Transform BoardAnchor => boardAnchor != null ? boardAnchor : (transform.parent != null ? transform.parent : transform);

	// ── Topology ─────────────────────────────────────────────────────────

	public override BoardUtility.BoardTopology Topology => BoardUtility.BoardTopology.Sphere;

	public override Vector2 SampleUniformAbsolutePosition()
	{
		// Unity's onUnitSphere is uniform on sphere surface area.
		Vector3 direction = Random.onUnitSphere;
		Vector3 worldPoint = transform.TransformPoint(direction * SphereLocalRadius);
		Vector2 boardLocal = WorldToBoardLocalPosition(worldPoint);
		return BoardLocalToAbsolutePosition(boardLocal);
	}

	/// <summary>
	/// Sphere has no edge; use half-circumference as a stable surrogate distance.
	/// </summary>
	public override float ComputeDistanceToBoundary(Vector2 absolutePosition)
	{
		return Mathf.Max(0f, State.Size);
	}

	public override BoardState TryShrink(BoardState current, float deltaMargin)
	{
		deltaMargin = Mathf.Max(0f, deltaMargin);
		float currentSize = Mathf.Max(1f, current.Size);
		float nextSize = currentSize - 2f * deltaMargin;
		if(nextSize < 2f)
			return null;

		BoardState nextState = new(current);
		nextState.SetSize(nextSize);
		nextState.SetShrinkMargin(current.ShrinkMargin + (currentSize - nextSize));

		float longitudeRatio = nextSize / currentSize;
		float latitudeRatio = nextSize / currentSize;
		float nextLatitudeMax = nextSize - 1f;

		// Keep angular locations stable while switching to the smaller index domain.
		for(int player = nextState.PlayerCount - 1; player >= 0; --player)
		{
			IReadOnlyList<StonePlacement> stones = nextState.GetStones(player);
			for(int stoneIndex = stones.Count - 1; stoneIndex >= 0; --stoneIndex)
			{
				StonePlacement stone = stones[stoneIndex];
				nextState.RemoveStoneAt(player, stoneIndex);
				Vector2 remapped = new Vector2(
					Mathf.Repeat(stone.position.x * longitudeRatio, 2f * nextSize),
					Mathf.Clamp(stone.position.y * latitudeRatio, 0f, nextLatitudeMax));
				nextState.AddStone(player, remapped, stone.strength);
			}
		}

		// Remove stones without liberties due to shrinking
		RemoveDeadStones(nextState);

		return nextState;
	}

	protected override void UpdateBoardScale()
	{
		// For sphere, scale the radius by the effective size ratio
		float size = Mathf.Max(1f, State.Size);
		float initialSize = Mathf.Max(size, size + State.ShrinkMargin);
		float scaleRatio = size / initialSize;
		BoardAnchor.localScale = Vector3.one * scaleRatio;
	}

	void RemoveDeadStones(BoardState state)
	{
		if(Caches == null || !Caches.isInitialized)
			return;

		Color[] playerColors = new Color[state.PlayerCount];
		for(int i = 0; i < playerColors.Length; ++i)
			playerColors[i] = Color.white;
		
		BoardUtility.RenderAnalysis(Caches, state, playerColors);
		List<BoardUtility.ChainStat> chainStats = BoardUtility.GetChainStats(Caches);

		HashSet<int> capturedRoots = new();
		for(int i = 0; i < chainStats.Count; ++i)
		{
			if(chainStats[i].hasLiberty == 0)
				capturedRoots.Add(chainStats[i].rootLabel);
		}

		if(capturedRoots.Count == 0)
			return;

		// Remove captured stones
		List<List<int>> stoneChainLabels = BoardUtility.GetStoneChainLabels(Caches, state);
		for(int player = 0; player < state.PlayerCount; ++player)
		{
			List<int> playerLabels = stoneChainLabels[player];
			for(int stoneIndex = playerLabels.Count - 1; stoneIndex >= 0; --stoneIndex)
			{
				if(capturedRoots.Contains(playerLabels[stoneIndex]))
					state.RemoveStoneAt(player, stoneIndex);
			}
		}
	}

	protected new void Start()
	{
		base.Start();

		TryBindInput();
	}

	protected void Update()
	{
		if(input != null)
			return;

		TryBindInput();
	}

	protected new void OnDestroy()
	{
		if(input != null)
			input.OnRotateDrag -= OnRotateDrag;

		base.OnDestroy();
	}

	void TryBindInput()
	{
		if(input != null || Match.Current == null)
			return;

		input = MatchInput.GetOrCreate(Match.Current);
		if(input != null)
			input.OnRotateDrag += OnRotateDrag;
	}

	void OnRotateDrag(Vector2 delta)
	{
		if(delta.sqrMagnitude <= 0f)
			return;

		Camera cam = Camera.main;
		if(cam == null)
			return;

		float dx = delta.x;
		float dy = delta.y;
		Vector3 camUp = cam.transform.up;
		Vector3 camForward = cam.transform.forward;
		Vector3 camRight = Vector3.Cross(camForward, camUp);
		if(camRight.sqrMagnitude < 1e-6f)
			camRight = cam.transform.right;
		else
			camRight.Normalize();

		Transform anchor = BoardAnchor;
		anchor.Rotate(camUp, -dx * RotateSpeed * Time.unscaledDeltaTime, Space.World);
		anchor.Rotate(camRight, -dy * RotateSpeed * Time.unscaledDeltaTime, Space.World);
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
		GridMaterial.SetFloat("_GridDisplayMode", 2f);
	}
}
