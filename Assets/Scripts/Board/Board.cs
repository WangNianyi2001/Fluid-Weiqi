using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

public class Board : MonoBehaviour
{
	public static Board Current {  get; private set; }

	#region Constants
	const string DisplayShaderResourcePath = "Shaders/BoardDisplay";
	const string BoardTerritoryMaterialResourcePath = "Materials/Board Territory";
	const string GridMaterialResourcePath = "Materials/BoardGrid";
	const string StoneModelResourcePath = "Models/Stone";
	const string StoneMaterialResourcePath = "Materials/Stone";
	const string StoneTransparentMaterialResourcePath = "Materials/Stone-transparent";
	const string GridObjectName = "BoardGrid";
	const string StoneRootName = "Stones";
	#endregion

	#region Inspector
	[SerializeField] new Renderer renderer;
	#endregion

	#region Properties
	public Color[] PlayerColors
	{
		get => playerColors;
		set
		{
			playerColors = value;
			RefreshStoneVisualMaterials();
			RefreshStoneTransparentMaterials();
			SyncStoneVisuals();
		}
	}
	public int PlayerCount => PlayerColors != null ? PlayerColors.Length : 0;
	public BoardState State => state ??= new BoardState();
	public BoardUtility.BoardCaches Caches => caches;
	#endregion

	#region Runtime state
	Color[] playerColors;
	BoardState state;
	bool hasPreview;
	BoardUtility.BoardCaches caches;
	Material material;
	Material gridMaterial;
	Material stoneMaterialTemplate;
	Material[] stoneSharedMaterials;
	Material stoneTransparentMaterialTemplate;
	Material[] stoneSharedTransparentMaterials;
	Shader displayShader;
	GameObject gridGo;
	GameObject stoneRoot;
	GameObject stonePrefab;
	bool loggedMissingStonePrefab;
	bool loggedMissingStoneMaterial;
	readonly Dictionary<int, GameObject> stoneVisuals = new();
	readonly Dictionary<int, GameObject> previewStoneVisuals = new();
	#endregion

	#region Unity life cycle
	protected void Awake()
	{
		Current = this;

		BoardUtility.Initialize(caches = new BoardUtility.BoardCaches());

		displayShader = Resources.Load<Shader>(DisplayShaderResourcePath);
		Material displayMaterialTemplate = Resources.Load<Material>(BoardTerritoryMaterialResourcePath);
		if(displayMaterialTemplate != null)
			material = new Material(displayMaterialTemplate);
		else if(displayShader != null)
			material = new Material(displayShader);
		else
			material = new(renderer.sharedMaterial);
		renderer.material = material;

		InitializeGridOverlay();
		InitializeStoneVisuals();
	}

	protected void OnDestroy()
	{
		if(material != null)
		{
			Destroy(material);
			material = null;
		}

		if(gridMaterial != null)
		{
			Destroy(gridMaterial);
			gridMaterial = null;
		}

		if(gridGo != null)
		{
			Destroy(gridGo);
			gridGo = null;
		}

		DestroyStoneVisuals();
		ClearPreviewStoneVisuals();
		DestroyStoneMaterials();
		DestroyTransparentStoneMaterials();
		if(stoneRoot != null)
		{
			Destroy(stoneRoot);
			stoneRoot = null;
		}

		if(caches != null)
		{
			BoardUtility.Dispose(caches);
			caches = null;
		}

		hasPreview = false;
	}

	protected void Start()
	{
		RefreshRendering();
	}
	#endregion

	#region State management
	public void SetState(BoardState newState)
	{
		state = newState;
		ClearPreviewStoneVisuals();
		RefreshStoneVisualMaterials();
		RefreshStoneTransparentMaterials();
		SyncStoneVisuals();
		UpdateGridMaterialParameters();
		RefreshRendering();
	}

	public void RefreshRendering()
	{
		UpdateGridMaterialParameters();
		RefreshRendering(State);
	}

	public void RefreshRendering(BoardState renderState)
	{
		if(renderState == null || caches == null || !caches.isInitialized)
			return;

		Color[] colors = PlayerColors ?? new Color[] { Color.black, Color.white };
		BoardUtility.RenderAnalysis(caches, renderState, colors);
		if(material == null)
			return;

		if(material.HasProperty("_DistributionMap"))
		{
			material.SetTexture("_DistributionMap", caches.distributionMap);
			material.SetFloat("_Threshold", renderState.Threshold);
			int playerCount = colors.Length;
			for(int player = 0; player < BoardUtility.MaxPlayers; ++player)
			{
				Color playerColor = player < playerCount ? colors[player] : Color.magenta;
				material.SetColor($"_PlayerColor{player}", playerColor);
			}
		}
		else if(material.HasProperty("_MainTex"))
			material.mainTexture = caches.distributionMap;
	}
	#endregion

	#region Grid overlay
	void InitializeGridOverlay()
	{
		if(gridGo != null || gridMaterial != null)
			return;

		if(!TryGetSourceBoardMesh(out Mesh sourceMesh))
			return;

		Material gridMaterialAsset = Resources.Load<Material>(GridMaterialResourcePath);
		if(gridMaterialAsset == null)
		{
			Debug.LogWarning($"Board grid material not found in Resources at '{GridMaterialResourcePath}'.", this);
			return;
		}

		gridMaterial = new Material(gridMaterialAsset);

		gridGo = new GameObject(GridObjectName);
		gridGo.transform.SetParent(transform, false);
		gridGo.layer = gameObject.layer;

		MeshFilter gridFilter = gridGo.AddComponent<MeshFilter>();
		gridFilter.sharedMesh = sourceMesh;

		MeshRenderer gridRenderer = gridGo.AddComponent<MeshRenderer>();
		gridRenderer.sharedMaterial = gridMaterial;
		gridRenderer.shadowCastingMode = ShadowCastingMode.Off;
		gridRenderer.receiveShadows = false;

		UpdateGridMaterialParameters();
	}

	bool TryGetSourceBoardMesh(out Mesh sourceMesh)
	{
		sourceMesh = null;

		MeshFilter sourceFilter = GetComponent<MeshFilter>();
		if(sourceFilter == null)
		{
			Debug.LogWarning("Board requires a MeshFilter on the same GameObject to build grid overlay.", this);
			return false;
		}

		sourceMesh = sourceFilter.sharedMesh;
		if(sourceMesh == null)
		{
			Debug.LogWarning("Board MeshFilter has no shared mesh for grid overlay.", this);
			return false;
		}

		return true;
	}

	void UpdateGridMaterialParameters()
	{
		if(gridMaterial == null)
			return;

		int boardSize = Mathf.Max(2, Mathf.RoundToInt(State.Size));
		gridMaterial.SetFloat("_BoardSize", boardSize);
		gridMaterial.SetFloat("_StarEdgeOffset", BoardUtility.GetStarEdgeOffset(boardSize));
	}
	#endregion

	#region Stone visuals
	void InitializeStoneVisuals()
	{
		stonePrefab = Resources.Load<GameObject>(StoneModelResourcePath);
		stoneMaterialTemplate = Resources.Load<Material>(StoneMaterialResourcePath);
		stoneTransparentMaterialTemplate = Resources.Load<Material>(StoneTransparentMaterialResourcePath);

		stoneRoot = new GameObject(StoneRootName);
		stoneRoot.transform.SetParent(transform, false);
		stoneRoot.layer = gameObject.layer;

		RefreshStoneVisualMaterials();
		RefreshStoneTransparentMaterials();
		SyncStoneVisuals();
	}

	void SyncStoneVisuals()
	{
		if(stoneRoot == null || state == null)
			return;

		HashSet<int> activeStoneIds = new();
		for(int player = 0; player < state.PlayerCount; ++player)
		{
			Material playerMaterial = GetStoneMaterial(player);
			IReadOnlyList<StonePlacement> playerStones = state.GetStones(player);
			for(int stoneIndex = 0; stoneIndex < playerStones.Count; ++stoneIndex)
			{
				StonePlacement stone = playerStones[stoneIndex];
				activeStoneIds.Add(stone.id);

				GameObject visual = GetOrCreateStoneVisual(stone.id);
				if(visual == null)
					continue;

				visual.transform.localPosition = LogicalToLocalPosition(stone.position);
				visual.transform.localScale = StoneLocalScale();
				ApplyStoneMaterial(visual, playerMaterial);
			}
		}

		List<int> staleStoneIds = new();
		foreach(KeyValuePair<int, GameObject> entry in stoneVisuals)
		{
			if(!activeStoneIds.Contains(entry.Key))
				staleStoneIds.Add(entry.Key);
		}

		for(int i = 0; i < staleStoneIds.Count; ++i)
		{
			int stoneId = staleStoneIds[i];
			Destroy(stoneVisuals[stoneId]);
			stoneVisuals.Remove(stoneId);
		}
	}

	GameObject GetOrCreateStoneVisual(int stoneId)
	{
		if(stoneVisuals.TryGetValue(stoneId, out GameObject visual) && visual != null)
			return visual;

		if(stonePrefab == null)
		{
			if(!loggedMissingStonePrefab)
			{
				Debug.LogWarning($"Board stone prefab not found in Resources at '{StoneModelResourcePath}'.", this);
				loggedMissingStonePrefab = true;
			}
			return null;
		}

		visual = Instantiate(stonePrefab, stoneRoot.transform);
		visual.name = $"Stone{stoneId}";
		stoneVisuals[stoneId] = visual;
		return visual;
	}

	void RefreshStoneVisualMaterials()
	{
		if(stoneMaterialTemplate == null)
		{
			if(!loggedMissingStoneMaterial)
			{
				Debug.LogWarning($"Board stone material not found in Resources at '{StoneMaterialResourcePath}'.", this);
				loggedMissingStoneMaterial = true;
			}
			DestroyStoneMaterials();
			return;
		}

		int materialCount = PlayerColors != null ? PlayerColors.Length : 0;
		if(materialCount == 0)
		{
			DestroyStoneMaterials();
			return;
		}

		if(stoneSharedMaterials == null || stoneSharedMaterials.Length != materialCount)
		{
			DestroyStoneMaterials();
			stoneSharedMaterials = new Material[materialCount];
			for(int player = 0; player < materialCount; ++player)
				stoneSharedMaterials[player] = new Material(stoneMaterialTemplate);
		}

		for(int player = 0; player < materialCount; ++player)
			stoneSharedMaterials[player].color = PlayerColors[player];
	}

	Material GetStoneMaterial(int player)
	{
		if(stoneSharedMaterials == null || player < 0 || player >= stoneSharedMaterials.Length)
			return null;
		return stoneSharedMaterials[player];
	}

	void ApplyStoneMaterial(GameObject visual, Material sharedMaterial)
	{
		if(visual == null || sharedMaterial == null)
			return;

		Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
		for(int i = 0; i < renderers.Length; ++i)
			renderers[i].sharedMaterial = sharedMaterial;
	}

	void DestroyStoneVisuals()
	{
		foreach(GameObject visual in stoneVisuals.Values)
		{
			if(visual != null)
				Destroy(visual);
		}
		stoneVisuals.Clear();
	}

	void DestroyStoneMaterials()
	{
		if(stoneSharedMaterials == null)
			return;

		for(int i = 0; i < stoneSharedMaterials.Length; ++i)
		{
			if(stoneSharedMaterials[i] != null)
				Destroy(stoneSharedMaterials[i]);
		}
		stoneSharedMaterials = null;
	}

	void SyncPreviewStoneVisuals(BoardState previewState)
	{
		if(stoneRoot == null || previewState == null)
		{
			ClearPreviewStoneVisuals();
			return;
		}

		HashSet<int> committedIds = new();
		if(state != null)
		{
			for(int player = 0; player < state.PlayerCount; ++player)
			{
				IReadOnlyList<StonePlacement> ps = state.GetStones(player);
				for(int i = 0; i < ps.Count; ++i)
					committedIds.Add(ps[i].id);
			}
		}

		HashSet<int> newPreviewIds = new();
		for(int player = 0; player < previewState.PlayerCount; ++player)
		{
			Material mat = GetStoneTransparentMaterial(player);
			IReadOnlyList<StonePlacement> ps = previewState.GetStones(player);
			for(int i = 0; i < ps.Count; ++i)
			{
				StonePlacement stone = ps[i];
				if(committedIds.Contains(stone.id))
					continue;

				newPreviewIds.Add(stone.id);
				GameObject visual = GetOrCreatePreviewStoneVisual(stone.id);
				if(visual == null)
					continue;

				visual.transform.localPosition = LogicalToLocalPosition(stone.position);
				visual.transform.localScale = StoneLocalScale();
				ApplyStoneMaterial(visual, mat);
			}
		}

		List<int> staleIds = new();
		foreach(int id in previewStoneVisuals.Keys)
		{
			if(!newPreviewIds.Contains(id))
				staleIds.Add(id);
		}
		for(int i = 0; i < staleIds.Count; ++i)
		{
			Destroy(previewStoneVisuals[staleIds[i]]);
			previewStoneVisuals.Remove(staleIds[i]);
		}
	}

	void ClearPreviewStoneVisuals()
	{
		foreach(GameObject visual in previewStoneVisuals.Values)
		{
			if(visual != null)
				Destroy(visual);
		}
		previewStoneVisuals.Clear();
	}

	GameObject GetOrCreatePreviewStoneVisual(int stoneId)
	{
		if(previewStoneVisuals.TryGetValue(stoneId, out GameObject visual) && visual != null)
			return visual;

		if(stonePrefab == null)
			return null;

		visual = Instantiate(stonePrefab, stoneRoot.transform);
		visual.name = $"PreviewStone{stoneId}";
		previewStoneVisuals[stoneId] = visual;
		return visual;
	}

	void RefreshStoneTransparentMaterials()
	{
		if(stoneTransparentMaterialTemplate == null)
		{
			DestroyTransparentStoneMaterials();
			return;
		}

		int materialCount = PlayerColors != null ? PlayerColors.Length : 0;
		if(materialCount == 0)
		{
			DestroyTransparentStoneMaterials();
			return;
		}

		if(stoneSharedTransparentMaterials == null || stoneSharedTransparentMaterials.Length != materialCount)
		{
			DestroyTransparentStoneMaterials();
			stoneSharedTransparentMaterials = new Material[materialCount];
			for(int player = 0; player < materialCount; ++player)
				stoneSharedTransparentMaterials[player] = new Material(stoneTransparentMaterialTemplate);
		}

		float templateAlpha = stoneTransparentMaterialTemplate.color.a;
		for(int player = 0; player < materialCount; ++player)
		{
			Color c = PlayerColors[player];
			c.a = templateAlpha;
			stoneSharedTransparentMaterials[player].color = c;
		}
	}

	Material GetStoneTransparentMaterial(int player)
	{
		if(stoneSharedTransparentMaterials == null || player < 0 || player >= stoneSharedTransparentMaterials.Length)
			return null;
		return stoneSharedTransparentMaterials[player];
	}

	void DestroyTransparentStoneMaterials()
	{
		if(stoneSharedTransparentMaterials == null)
			return;

		for(int i = 0; i < stoneSharedTransparentMaterials.Length; ++i)
		{
			if(stoneSharedTransparentMaterials[i] != null)
				Destroy(stoneSharedTransparentMaterials[i]);
		}
		stoneSharedTransparentMaterials = null;
	}

	Vector3 StoneLocalScale()
	{
		return Vector3.one * (1f / Mathf.Max(1, Mathf.RoundToInt(State.Size) - 1));
	}
	#endregion

 	#region Preview
	public void ClearPreview()
	{
		if(!hasPreview)
			return;

		hasPreview = false;
		ClearPreviewStoneVisuals();
		RefreshRendering();
	}

	public void ShowPreview(BoardState stateToPreview)
	{
		hasPreview = stateToPreview != null;
		if(stateToPreview == null)
		{
			ClearPreviewStoneVisuals();
			RefreshRendering();
		}
		else
		{
			SyncPreviewStoneVisuals(stateToPreview);
			RefreshRendering(stateToPreview);
		}
	}
	#endregion

	#region Coordinate conversion
	public Vector2 WorldToLogicalPosition(Vector3 worldPosition)
	{
		Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
		float span = State.Size - 1;
		return new Vector2((localPosition.x + .5f) * span, (localPosition.y + .5f) * span);
	}

	public Vector3 LogicalToWorldPosition(Vector2 logicalPosition)
	{
		return transform.TransformPoint(LogicalToLocalPosition(logicalPosition));
	}

	Vector3 LogicalToLocalPosition(Vector2 logicalPosition)
	{
		float span = State.Size - 1;
		return new Vector3(
			logicalPosition.x / span - .5f,
			logicalPosition.y / span - .5f,
			0
		);
	}
	#endregion

}
