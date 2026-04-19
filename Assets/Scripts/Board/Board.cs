using UnityEngine;
using System.Collections.Generic;
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
	public int ComputeResolution => BoardUtility.ComputeResolution;
	public BoardState State => state ??= new BoardState();
	#endregion

	#region Runtime state
	BoardState state;
	BoardState previewState;
	bool hasPreview;
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

		hasPreview = false;
		previewState = null;
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
		if(mainTexture == null || renderState == null || !BoardUtility.IsInitialized)
			return;

		BoardUtility.RenderAnalysis(renderState, PlayerColors);
		if(displayMaterial != null)
		{
			displayMaterial.SetTexture("_DistributionMap", BoardUtility.DistributionMap);
			displayMaterial.SetFloat("_Threshold", renderState.Threshold);
			for(int player = 0; player < BoardUtility.MaxPlayers; ++player)
			{
				Color playerColor = player < PlayerCount ? PlayerColors[player] : Color.magenta;
				displayMaterial.SetColor($"_PlayerColor{player}", playerColor);
			}
			Graphics.Blit(BoardUtility.DistributionMap, mainTexture, displayMaterial);
		}
		else
			Graphics.Blit(BoardUtility.DistributionMap, mainTexture);
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
		gridMaterial.SetFloat("_StarEdgeOffset", GetStarEdgeOffset(boardSize));
	}

	int GetStarEdgeOffset(int boardSize)
	{
		if(boardSize >= 15)
			return 3;

		if(boardSize >= 11)
			return 3;

		if(boardSize >= 9)
			return 2;

		return Mathf.Max(1, Mathf.RoundToInt((boardSize - 1) * 0.5f));
	}
	#endregion

 	#region Game semantics
	public void ClearPreview()
	{
		if(!hasPreview)
			return;

		hasPreview = false;
		previewState = null;
		RefreshRendering();
	}

	public bool TryPreviewStone(int playerIndex, Vector2 logicalPosition, float strength = 1)
	{
		RefreshRendering();

		if(IsOccupiedAtLogicalPosition(State, logicalPosition))
		{
			ClearPreview();
			return false;
		}

		if(!State.PeekStonePlacement(playerIndex, logicalPosition, out BoardState newState, strength))
		{
			ClearPreview();
			return false;
		}

		hasPreview = true;
		previewState = newState;
		RefreshRendering(previewState);
		return true;
	}

	public bool TryPlaceStone(int playerIndex, Vector2 logicalPosition, float strength = 1)
	{
		RefreshRendering();

		if(IsOccupiedAtLogicalPosition(State, logicalPosition))
			return false;

		if(!State.PeekStonePlacement(playerIndex, logicalPosition, out BoardState placedPreviewState, strength))
			return false;

		RefreshRendering(placedPreviewState);

		List<BoardUtility.ChainStat> chainStats = GetChainStats();
		Dictionary<int, BoardUtility.ChainStat> chainStatsByRoot = new(chainStats.Count);
		HashSet<int> capturedRoots = new();

		for(int i = 0; i < chainStats.Count; ++i)
		{
			BoardUtility.ChainStat chainStat = chainStats[i];
			chainStatsByRoot[chainStat.rootLabel] = chainStat;
			if(chainStat.owner != playerIndex && chainStat.hasLiberty == 0)
				capturedRoots.Add(chainStat.rootLabel);
		}

		int placedChainRoot = GetChainLabelAtLogicalPosition(placedPreviewState, logicalPosition);
		bool placedChainHasLiberty = chainStatsByRoot.TryGetValue(placedChainRoot, out BoardUtility.ChainStat placedChainStat) && placedChainStat.hasLiberty != 0;
		if(capturedRoots.Count == 0 && !placedChainHasLiberty)
		{
			RefreshRendering();
			return false;
		}

		if(capturedRoots.Count > 0)
			RemoveCapturedStones(placedPreviewState, capturedRoots, playerIndex);

		SetState(placedPreviewState);
		hasPreview = false;
		previewState = null;

		return true;
	}

	void RemoveCapturedStones(BoardState renderState, HashSet<int> capturedRoots, int currentPlayerIndex)
	{
		List<List<int>> stoneChainLabels = GetStoneChainLabels(renderState);
		for(int player = 0; player < renderState.PlayerCount; ++player)
		{
			if(player == currentPlayerIndex)
				continue;

			List<int> playerLabels = stoneChainLabels[player];
			for(int stoneIndex = playerLabels.Count - 1; stoneIndex >= 0; --stoneIndex)
			{
				if(capturedRoots.Contains(playerLabels[stoneIndex]))
					renderState.RemoveStoneAt(player, stoneIndex);
			}
		}
	}
	#endregion

	#region Analysis wrappers
	public int[] GetPlayerAreaPixelsByDominance()
	{
		if(!BoardUtility.IsInitialized)
			return new int[PlayerCount];
		return BoardUtility.GetPlayerAreaPixelsByDominance(PlayerCount);
	}

	public List<BoardUtility.ChainStat> GetChainStats()
	{
		if(!BoardUtility.IsInitialized)
			return new List<BoardUtility.ChainStat>();
		return BoardUtility.GetChainStats();
	}

	public int GetChainLabelAtLogicalPosition(BoardState renderState, Vector2 logicalPosition)
	{
		if(!BoardUtility.IsInitialized)
			return -1;
		return BoardUtility.GetChainLabelAtLogicalPosition(renderState, logicalPosition);
	}

	public bool IsOccupiedAtLogicalPosition(BoardState renderState, Vector2 logicalPosition)
	{
		if(!BoardUtility.IsInitialized)
			return false;
		return BoardUtility.IsOccupiedAtLogicalPosition(renderState, logicalPosition);
	}

	public List<List<int>> GetStoneChainLabels(BoardState renderState)
	{
		if(!BoardUtility.IsInitialized)
			return new List<List<int>>();
		return BoardUtility.GetStoneChainLabels(renderState);
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
