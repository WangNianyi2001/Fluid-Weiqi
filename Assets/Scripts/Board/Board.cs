using UnityEngine;
using UnityEngine.Rendering;

public class Board : MonoBehaviour
{
	public static Board Current {  get; private set; }

	#region Constants
	const int RenderTextureSize = 1024;
	const string DisplayShaderResourcePath = "Shaders/BoardDisplay";
	const string GridMaterialResourcePath = "Materials/BoardGrid";
	const string GridObjectName = "BoardGrid";
	#endregion

	#region Inspector
	[SerializeField] new Renderer renderer;
	#endregion

	#region Properties
	public Color[] PlayerColors { get; set; }
	public int PlayerCount => PlayerColors.Length;
	public BoardState State => state ??= new BoardState();
	public BoardUtility.BoardCaches Caches => caches;
	#endregion

	#region Runtime state
	BoardState state;
	bool hasPreview;
	BoardUtility.BoardCaches caches;
	Material material;
	Material displayMaterial;
	Material gridMaterial;
	RenderTexture mainTexture;
	Shader displayShader;
	GameObject gridGo;
	#endregion

	#region Unity life cycle
	protected void Awake()
	{
		Current = this;

		BoardUtility.Initialize(caches = new BoardUtility.BoardCaches());

		displayShader = Resources.Load<Shader>(DisplayShaderResourcePath);
		mainTexture = CreateRenderTexture(CreateMainTextureDescriptor());

		material = new(renderer.sharedMaterial);
		renderer.material = material;
		material.mainTexture = mainTexture;

		InitializeGridOverlay();

		if(displayShader != null)
			displayMaterial = new Material(displayShader);
	}

	protected void OnDestroy()
	{
		if(material != null)
		{
			Destroy(material);
			material = null;
		}

		if(displayMaterial != null)
		{
			Destroy(displayMaterial);
			displayMaterial = null;
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

		ReleaseRenderTexture(ref mainTexture);

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
		if(mainTexture == null || renderState == null || caches == null || !caches.isInitialized)
			return;

		Color[] colors = PlayerColors ?? new Color[] { Color.black, Color.white };
		BoardUtility.RenderAnalysis(caches, renderState, colors);
		if(displayMaterial != null)
		{
			displayMaterial.SetTexture("_DistributionMap", caches.distributionMap);
			displayMaterial.SetFloat("_Threshold", renderState.Threshold);
			int playerCount = colors.Length;
			for(int player = 0; player < BoardUtility.MaxPlayers; ++player)
			{
				Color playerColor = player < playerCount ? colors[player] : Color.magenta;
				displayMaterial.SetColor($"_PlayerColor{player}", playerColor);
			}
			Graphics.Blit(caches.distributionMap, mainTexture, displayMaterial);
		}
		else
			Graphics.Blit(caches.distributionMap, mainTexture);
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
	public Vector2 WorldToLogicalPosition(Vector3 worldPosition)
	{
		Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
		float span = State.Size - 1;
		return new Vector2((localPosition.x + .5f) * span, (localPosition.y + .5f) * span);
	}

	public Vector3 LogicalToWorldPosition(Vector2 logicalPosition)
	{
		float span = State.Size - 1;
		Vector3 localPosition = new(
			logicalPosition.x / span - .5f,
			logicalPosition.y / span - .5f,
			0
		);
		return transform.TransformPoint(localPosition);
	}
	#endregion

	#region Rendering texture helpers
	RenderTextureDescriptor CreateMainTextureDescriptor()
	{
		return new RenderTextureDescriptor(RenderTextureSize, RenderTextureSize, RenderTextureFormat.ARGB32, 0)
		{
			enableRandomWrite = false,
			sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear,
			msaaSamples = 1,
			useMipMap = false,
			autoGenerateMips = false,
		};
	}

	RenderTexture CreateRenderTexture(RenderTextureDescriptor descriptor)
	{
		RenderTexture rt = new(descriptor)
		{
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Bilinear,
		};
		rt.Create();
		return rt;
	}

	void ReleaseRenderTexture(ref RenderTexture rt)
	{
		if(rt == null)
			return;

		if(rt.IsCreated())
			rt.Release();
		Destroy(rt);
		rt = null;
	}
	#endregion
}
