using UnityEngine;
using UnityEngine.Rendering;

public abstract class Board : MonoBehaviour
{
	public static Board Current { get; private set; }

	#region Constants
	const string DisplayShaderResourcePath = "Shaders/BoardDisplay";
	const string BoardTerritoryMaterialResourcePath = "Materials/Board Territory";
	const string GridMaterialResourcePath = "Materials/BoardGrid";
	const string GridObjectName = "BoardGrid";
	#endregion

	#region Inspector
	[SerializeField] new Renderer renderer;
	protected Renderer BoardRenderer => renderer;
	protected Material GridMaterial => gridMaterial;
	#endregion

	#region Properties
	public Color[] PlayerColors
	{
		get => playerColors;
		set
		{
			playerColors = value;
			RefreshRendering();
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
	Shader displayShader;
	GameObject gridGo;
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

		caches.topology = Topology;

		Color[] colors = PlayerColors ?? new Color[] { Color.black, Color.white };
		BoardUtility.RenderAnalysis(caches, renderState, colors);
		if(material == null)
			return;

		if(material.HasProperty("_DistributionMap"))
		{
			material.SetTexture("_DistributionMap", caches.distributionMap);
			if(material.HasProperty("_Topology"))
				material.SetFloat("_Topology", (float)Topology);
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

	protected virtual void UpdateGridMaterialParameters()
	{
		if(GridMaterial == null)
			return;

		if(GridMaterial.HasProperty("_Topology"))
			GridMaterial.SetFloat("_Topology", (float)Topology);
		if(GridMaterial.HasProperty("_GridDisplayMode"))
			GridMaterial.SetFloat("_GridDisplayMode", 0f);
	}
	#endregion

 	#region Preview
	public void ClearPreview()
	{
		if(!hasPreview)
			return;

		hasPreview = false;
		RefreshRendering();
	}

	public void ShowPreview(BoardState stateToPreview)
	{
		hasPreview = stateToPreview != null;
		if(stateToPreview == null)
			RefreshRendering();
		else
			RefreshRendering(stateToPreview);
	}
	#endregion

	#region Coordinate conversion
	public virtual BoardUtility.BoardTopology Topology => BoardUtility.BoardTopology.Flat;

	/// <summary>
	/// Uniformly sample a legal absolute grid position on the board.
	/// Override for non-square topologies.
	/// </summary>
	public virtual Vector2 SampleUniformAbsolutePosition()
	{
		float boardSize = Mathf.Max(1, Mathf.RoundToInt(State.Size));
		float x = Random.Range(0, boardSize);
		float y = Random.Range(0, boardSize);
		return new Vector2(x, y);
	}

	/// <summary>
	/// Distance from a point to board boundary in absolute-grid units.
	/// Override for topologies without physical boundary.
	/// </summary>
	public virtual float ComputeDistanceToBoundary(Vector2 position)
	{
		float boardExtent = Mathf.Max(0f, State.BoardStateExtent);
		return Mathf.Min(
			Mathf.Min(position.x, boardExtent - position.x),
			Mathf.Min(position.y, boardExtent - position.y));
	}

	public abstract Bounds GetWorldBounds();
	public abstract Vector2 WorldToBoardLocalPosition(Vector3 worldPosition);
	public abstract Vector3 BoardLocalToWorldPosition(Vector2 boardLocalPosition);
	public abstract Vector2 BoardLocalToAbsolutePosition(Vector2 boardLocalPosition);
	public abstract Vector2 AbsoluteToBoardLocalPosition(Vector2 absolutePosition);

	public Vector2 WorldToAbsolutePosition(Vector3 worldPosition)
	{
		return BoardLocalToAbsolutePosition(WorldToBoardLocalPosition(worldPosition));
	}

	public Vector3 AbsoluteToWorldPosition(Vector2 absolutePosition)
	{
		return BoardLocalToWorldPosition(AbsoluteToBoardLocalPosition(absolutePosition));
	}

	public Vector2 WorldToLogicalPosition(Vector3 worldPosition)
	{
		return WorldToAbsolutePosition(worldPosition);
	}

	public Vector3 LogicalToWorldPosition(Vector2 logicalPosition)
	{
		return AbsoluteToWorldPosition(logicalPosition);
	}
	#endregion
}
