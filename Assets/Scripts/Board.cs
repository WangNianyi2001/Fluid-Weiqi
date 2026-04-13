using UnityEngine;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public class Board : MonoBehaviour
{
	[StructLayout(LayoutKind.Sequential)]
	struct GpuChainStat
	{
		public int rootLabel;
		public int owner;
		public int pixelCount;
		public int hasLiberty;
	}

	public readonly struct ChainStat
	{
		public ChainStat(int rootLabel, int owner, int pixelCount, bool hasLiberty)
		{
			RootLabel = rootLabel;
			Owner = owner;
			PixelCount = pixelCount;
			HasLiberty = hasLiberty;
		}

		public int RootLabel { get; }
		public int Owner { get; }
		public int PixelCount { get; }
		public bool HasLiberty { get; }
	}

	#region Constants
	const int MaxPlayers = 4;
	const int RenderTextureSize = 1024;
	const int ComputeTextureSize = 128;
	const string DistributionShaderResourcePath = "Shaders/BoardDistribution";
	const string DisplayShaderResourcePath = "Shaders/BoardDisplay";
	const float GizmoHeightScale = 0.1f;
	const int MaxCclIterations = ComputeTextureSize * ComputeTextureSize;
	const int ThreadGroupSize = 8;
	const float AreaEpsilon = 1e-6f;
	#endregion

	#region Inspector
	[SerializeField] private new Renderer renderer;
	[SerializeField] private Color[] playerColors = new Color[] { Color.black, Color.white };
	[SerializeField] private int playerCount = 2;
	[SerializeField] private float size = 19;
	[SerializeField] private float stoneVariance = 1f / Mathf.Sqrt(32);
	[SerializeField] private float threshold = .5f;
	#endregion

	#region Properties
	public Renderer BoardRenderer => renderer;
	public IReadOnlyList<Color> PlayerColors => playerColors;
	public int ComputeResolution => ComputeTextureSize;
	public int PlayerCount
	{
		get => playerCount;
		set => playerCount = Mathf.Clamp(value, 2, MaxPlayers);
	}
	public float Size
	{
		get => size;
		set => size = Mathf.Max(.001f, value);
	}
	public float StoneVariance
	{
		get => stoneVariance;
		set => stoneVariance = Mathf.Max(.0001f, value);
	}
	public float Threshold
	{
		get => threshold;
		set => threshold = Mathf.Max(0, value);
	}
	public BoardState State => EnsureState();
	#endregion

	#region Runtime state
	BoardState state;
	Material material;
	Material displayMaterial;
	RenderTexture mainTexture;
	RenderTexture distributionMap;
	RenderTexture territoryMap;
	ComputeShader distributionShader;
	Shader displayShader;
	ComputeBuffer ownerBuffer;
	ComputeBuffer areaPixelCountBuffer;
	ComputeBuffer labelBufferA;
	ComputeBuffer labelBufferB;
	ComputeBuffer activeLabelBuffer;
	ComputeBuffer cclChangedBuffer;
	ComputeBuffer chainOwnerBuffer;
	ComputeBuffer chainPixelCountBuffer;
	ComputeBuffer chainLibertyBuffer;
	ComputeBuffer compactChainStatBuffer;
	ComputeBuffer compactChainStatCountBuffer;

	int distributionKernel;
	int territoryKernel;
	int clearAreaPixelCountsKernel;
	int accumulateAreaPixelCountsKernel;
	int cclInitKernel;
	int cclPropagateKernel;
	int clearChainStatsKernel;
	int accumulateChainStatsKernel;
	int compactChainStatsKernel;
	#endregion

	#region Unity life cycle
	protected void Awake()
	{
		distributionShader = Resources.Load<ComputeShader>(DistributionShaderResourcePath);
		distributionKernel = distributionShader.FindKernel("CSDistribution");
		territoryKernel = distributionShader.FindKernel("CSTerritory");
		clearAreaPixelCountsKernel = distributionShader.FindKernel("CSClearAreaPixelCounts");
		accumulateAreaPixelCountsKernel = distributionShader.FindKernel("CSAccumulateAreaPixelCounts");
		cclInitKernel = distributionShader.FindKernel("CSInitLabels");
		cclPropagateKernel = distributionShader.FindKernel("CSPropagateLabels");
		clearChainStatsKernel = distributionShader.FindKernel("CSClearChainStats");
		accumulateChainStatsKernel = distributionShader.FindKernel("CSAccumulateChainStats");
		compactChainStatsKernel = distributionShader.FindKernel("CSCompactChainStats");
		displayShader = Resources.Load<Shader>(DisplayShaderResourcePath);

		mainTexture = CreateRenderTexture(CreateMainTextureDescriptor());
		distributionMap = CreateRenderTexture(CreateDistributionMapDescriptor());
		territoryMap = CreateRenderTexture(CreateTerritoryMapDescriptor());
		AllocateConnectivityBuffers();

		material = new(renderer.sharedMaterial);
		renderer.material = material;
		ConfigureMaterialForTransparency(material);
		material.mainTexture = mainTexture;

		if(displayShader != null)
			displayMaterial = new Material(displayShader);
	}

	protected void OnDestroy()
	{
		// Rendering

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

		ReleaseRenderTexture(ref mainTexture);
		ReleaseRenderTexture(ref distributionMap);
		ReleaseRenderTexture(ref territoryMap);

		ownerBuffer?.Release();
		ownerBuffer = null;

		areaPixelCountBuffer?.Release();
		areaPixelCountBuffer = null;

		labelBufferA?.Release();
		labelBufferA = null;

		labelBufferB?.Release();
		labelBufferB = null;

		cclChangedBuffer?.Release();
		cclChangedBuffer = null;

		chainOwnerBuffer?.Release();
		chainOwnerBuffer = null;

		chainPixelCountBuffer?.Release();
		chainPixelCountBuffer = null;

		chainLibertyBuffer?.Release();
		chainLibertyBuffer = null;

		compactChainStatBuffer?.Release();
		compactChainStatBuffer = null;

		compactChainStatCountBuffer?.Release();
		compactChainStatCountBuffer = null;

		activeLabelBuffer = null;
	}

	protected void Start()
	{
		ApplyBoardSettings(EnsureState(), applySize: true);
		RefreshRendering();
	}

	protected void OnValidate()
	{
		PlayerCount = playerCount;
		Size = size;
		StoneVariance = stoneVariance;
		Threshold = threshold;

		if(!Application.isPlaying || state == null)
			return;

		state.StoneVariance = stoneVariance;
		state.Threshold = threshold;
		RefreshRendering();
	}
	#endregion

	#region State management
	BoardState EnsureState()
	{
		if(state == null)
		{
			state = new(playerCount);
			ApplyBoardSettings(state, applySize: true);
		}

		return state;
	}

	void ApplyBoardSettings(BoardState boardState, bool applySize)
	{
		boardState.PlayerCount = playerCount;
		if(applySize)
			boardState.Size = size;
		boardState.StoneVariance = stoneVariance;
		boardState.Threshold = threshold;
	}

	public void RefreshRendering()
	{
		RefreshRendering(EnsureState());
	}

	public void RefreshRendering(BoardState renderState)
	{
		if(mainTexture == null || territoryMap == null || renderState == null)
			return;

		RenderState(renderState);
	}

	public void SetState(BoardState newState)
	{
		state = newState;
		ApplyBoardSettings(state, applySize: true);
		RefreshRendering();
	}
	#endregion

	#region Rendering
	void RenderState(BoardState state)
	{
		RenderDistributionMap(state, distributionMap);
		RenderTerritoryMap(state, distributionMap, territoryMap);
		RunDominantAreaStats(state, distributionMap);
		RunConnectedComponents();
		RunChainStats();

		if(displayMaterial != null)
		{
			displayMaterial.SetTexture("_DistributionMap", distributionMap);
			displayMaterial.SetFloat("_Threshold", state.Threshold);
			for(int player = 0; player < MaxPlayers; ++player)
			{
				Color playerColor = player < playerColors.Length ? playerColors[player] : Color.magenta;
				displayMaterial.SetColor($"_PlayerColor{player}", playerColor);
			}
			Graphics.Blit(distributionMap, mainTexture, displayMaterial);
		}
		else
			Graphics.Blit(distributionMap, mainTexture);
	}

	void RenderDistributionMap(BoardState state, RenderTexture rt)
	{
		ComputeBuffer[] stoneBuffers = new ComputeBuffer[MaxPlayers];

		try
		{
			distributionShader.SetTexture(distributionKernel, "_DistributionOutput", rt);
			distributionShader.SetFloat("_BoardSize", state.Size - 1);
			distributionShader.SetFloat("_StoneVariance", Mathf.Max(0.0001f, state.StoneVariance));
			distributionShader.SetInt("_TextureWidth", rt.width);
			distributionShader.SetInt("_TextureHeight", rt.height);

			for(int player = 0; player < MaxPlayers; ++player)
			{
				int stoneCount = player < state.PlayerCount ? state.GetStones(player).Count : 0;
				stoneBuffers[player] = new ComputeBuffer(Mathf.Max(1, stoneCount), 3 * sizeof(float));
				distributionShader.SetInt($"_Player{player}StoneCount", stoneCount);
				distributionShader.SetBuffer(distributionKernel, $"_Player{player}Stones", stoneBuffers[player]);

				if(stoneCount == 0)
					continue;

				IReadOnlyList<StonePlacement> source = state.GetStones(player);
				StonePlacement[] gpuStones = new StonePlacement[stoneCount];
				for(int i = 0; i < stoneCount; ++i)
				{
					gpuStones[i] = new StonePlacement
					{
						position = source[i].position,
						strength = source[i].strength,
					};
				}

				stoneBuffers[player].SetData(gpuStones);
			}

			int groupsX = Mathf.CeilToInt(rt.width / (float)ThreadGroupSize);
			int groupsY = Mathf.CeilToInt(rt.height / (float)ThreadGroupSize);
			distributionShader.Dispatch(distributionKernel, groupsX, groupsY, 1);
		}
		finally
		{
			for(int i = 0; i < stoneBuffers.Length; ++i)
				stoneBuffers[i]?.Release();
		}
	}

	void RenderTerritoryMap(BoardState state, RenderTexture distributionTexture, RenderTexture targetTerritory)
	{
		distributionShader.SetTexture(territoryKernel, "_DistributionInput", distributionTexture);
		distributionShader.SetTexture(territoryKernel, "_TerritoryOutput", targetTerritory);
		distributionShader.SetBuffer(territoryKernel, "_OwnerBuffer", ownerBuffer);
		distributionShader.SetInt("_TextureWidth", targetTerritory.width);
		distributionShader.SetInt("_TextureHeight", targetTerritory.height);
		distributionShader.SetInt("_PlayerCount", state.PlayerCount);
		distributionShader.SetFloat("_Threshold", state.Threshold);

		for(int player = 0; player < MaxPlayers; ++player)
		{
			Color playerColor = player < playerColors.Length ? playerColors[player] : Color.magenta;
			distributionShader.SetVector($"_PlayerColor{player}", playerColor);
		}

		int groupsX = Mathf.CeilToInt(targetTerritory.width / (float)ThreadGroupSize);
		int groupsY = Mathf.CeilToInt(targetTerritory.height / (float)ThreadGroupSize);
		distributionShader.Dispatch(territoryKernel, groupsX, groupsY, 1);
	}

	void RunDominantAreaStats(BoardState state, Texture distributionTexture)
	{
		distributionShader.SetInt("_TextureWidth", ComputeTextureSize);
		distributionShader.SetInt("_TextureHeight", ComputeTextureSize);
		distributionShader.SetInt("_PlayerCount", state.PlayerCount);
		distributionShader.SetFloat("_AreaEpsilon", AreaEpsilon);
		distributionShader.SetBuffer(clearAreaPixelCountsKernel, "_AreaPixelCountBuffer", areaPixelCountBuffer);
		distributionShader.Dispatch(clearAreaPixelCountsKernel, 1, 1, 1);

		distributionShader.SetTexture(accumulateAreaPixelCountsKernel, "_DistributionInput", distributionTexture);
		distributionShader.SetBuffer(accumulateAreaPixelCountsKernel, "_AreaPixelCountBuffer", areaPixelCountBuffer);
		int groupsX = Mathf.CeilToInt(ComputeTextureSize / (float)ThreadGroupSize);
		int groupsY = Mathf.CeilToInt(ComputeTextureSize / (float)ThreadGroupSize);
		distributionShader.Dispatch(accumulateAreaPixelCountsKernel, groupsX, groupsY, 1);
	}

	void RunConnectedComponents()
	{
		distributionShader.SetInt("_TextureWidth", ComputeTextureSize);
		distributionShader.SetInt("_TextureHeight", ComputeTextureSize);
		distributionShader.SetBuffer(cclInitKernel, "_OwnerBuffer", ownerBuffer);
		distributionShader.SetBuffer(cclInitKernel, "_LabelBufferWrite", labelBufferA);

		int groupsX = Mathf.CeilToInt(ComputeTextureSize / (float)ThreadGroupSize);
		int groupsY = Mathf.CeilToInt(ComputeTextureSize / (float)ThreadGroupSize);
		distributionShader.Dispatch(cclInitKernel, groupsX, groupsY, 1);

		ComputeBuffer readBuffer = labelBufferA;
		ComputeBuffer writeBuffer = labelBufferB;
		int[] changed = new int[1];
		for(int i = 0; i < MaxCclIterations; ++i)
		{
			changed[0] = 0;
			cclChangedBuffer.SetData(changed);

			distributionShader.SetBuffer(cclPropagateKernel, "_OwnerBuffer", ownerBuffer);
			distributionShader.SetBuffer(cclPropagateKernel, "_LabelBufferRead", readBuffer);
			distributionShader.SetBuffer(cclPropagateKernel, "_LabelBufferWrite", writeBuffer);
			distributionShader.SetBuffer(cclPropagateKernel, "_CclChangedBuffer", cclChangedBuffer);
			distributionShader.Dispatch(cclPropagateKernel, groupsX, groupsY, 1);

			(readBuffer, writeBuffer) = (writeBuffer, readBuffer);

			cclChangedBuffer.GetData(changed);
			if(changed[0] == 0)
				break;
		}

		activeLabelBuffer = readBuffer;
	}

	void RunChainStats()
	{
		if(activeLabelBuffer == null)
			return;

		distributionShader.SetInt("_TextureWidth", ComputeTextureSize);
		distributionShader.SetInt("_TextureHeight", ComputeTextureSize);

		int groupsX = Mathf.CeilToInt(ComputeTextureSize / (float)ThreadGroupSize);
		int groupsY = Mathf.CeilToInt(ComputeTextureSize / (float)ThreadGroupSize);

		distributionShader.SetBuffer(clearChainStatsKernel, "_ChainOwnerBuffer", chainOwnerBuffer);
		distributionShader.SetBuffer(clearChainStatsKernel, "_ChainPixelCountBuffer", chainPixelCountBuffer);
		distributionShader.SetBuffer(clearChainStatsKernel, "_ChainHasLibertyBuffer", chainLibertyBuffer);
		distributionShader.Dispatch(clearChainStatsKernel, groupsX, groupsY, 1);

		distributionShader.SetBuffer(accumulateChainStatsKernel, "_OwnerBuffer", ownerBuffer);
		distributionShader.SetBuffer(accumulateChainStatsKernel, "_LabelBufferRead", activeLabelBuffer);
		distributionShader.SetBuffer(accumulateChainStatsKernel, "_ChainOwnerBuffer", chainOwnerBuffer);
		distributionShader.SetBuffer(accumulateChainStatsKernel, "_ChainPixelCountBuffer", chainPixelCountBuffer);
		distributionShader.SetBuffer(accumulateChainStatsKernel, "_ChainHasLibertyBuffer", chainLibertyBuffer);
		distributionShader.Dispatch(accumulateChainStatsKernel, groupsX, groupsY, 1);
	}

	public int[] GetPlayerAreaPixelsByDominance()
	{
		int[] areaByPlayer = new int[PlayerCount];
		if(areaPixelCountBuffer == null)
			return areaByPlayer;

		int[] raw = new int[MaxPlayers];
		areaPixelCountBuffer.GetData(raw);
		for(int i = 0; i < areaByPlayer.Length; ++i)
			areaByPlayer[i] = raw[i];

		return areaByPlayer;
	}

	public List<ChainStat> GetChainStats()
	{
		List<ChainStat> chainStats = new();
		if(activeLabelBuffer == null)
			return chainStats;

		compactChainStatBuffer.SetCounterValue(0);
		distributionShader.SetInt("_TextureWidth", ComputeTextureSize);
		distributionShader.SetInt("_TextureHeight", ComputeTextureSize);
		distributionShader.SetBuffer(compactChainStatsKernel, "_OwnerBuffer", ownerBuffer);
		distributionShader.SetBuffer(compactChainStatsKernel, "_LabelBufferRead", activeLabelBuffer);
		distributionShader.SetBuffer(compactChainStatsKernel, "_ChainOwnerBuffer", chainOwnerBuffer);
		distributionShader.SetBuffer(compactChainStatsKernel, "_ChainPixelCountBuffer", chainPixelCountBuffer);
		distributionShader.SetBuffer(compactChainStatsKernel, "_ChainHasLibertyBuffer", chainLibertyBuffer);
		distributionShader.SetBuffer(compactChainStatsKernel, "_CompactChainStatBuffer", compactChainStatBuffer);

		int groupsX = Mathf.CeilToInt(ComputeTextureSize / (float)ThreadGroupSize);
		int groupsY = Mathf.CeilToInt(ComputeTextureSize / (float)ThreadGroupSize);
		distributionShader.Dispatch(compactChainStatsKernel, groupsX, groupsY, 1);

		ComputeBuffer.CopyCount(compactChainStatBuffer, compactChainStatCountBuffer, 0);
		int[] countArray = new int[1];
		compactChainStatCountBuffer.GetData(countArray);
		int chainCount = countArray[0];
		if(chainCount <= 0)
			return chainStats;

		GpuChainStat[] gpuStats = new GpuChainStat[chainCount];
		compactChainStatBuffer.GetData(gpuStats, 0, 0, chainCount);
		for(int i = 0; i < chainCount; ++i)
		{
			GpuChainStat gpuStat = gpuStats[i];
			chainStats.Add(new ChainStat(gpuStat.rootLabel, gpuStat.owner, gpuStat.pixelCount, gpuStat.hasLiberty != 0));
		}

		return chainStats;
	}

	public int GetChainLabelAtLogicalPosition(BoardState renderState, Vector2 logicalPosition)
	{
		if(activeLabelBuffer == null || renderState == null)
			return -1;

		int pixelIndex = LogicalPositionToPixelIndex(renderState, logicalPosition);
		if(pixelIndex < 0)
			return -1;

		int[] label = new int[1];
		activeLabelBuffer.GetData(label, 0, pixelIndex, 1);
		return label[0];
	}

	public bool IsOccupiedAtLogicalPosition(BoardState renderState, Vector2 logicalPosition)
	{
		if(ownerBuffer == null || renderState == null)
			return false;

		int pixelIndex = LogicalPositionToPixelIndex(renderState, logicalPosition);
		if(pixelIndex < 0)
			return false;

		int[] owner = new int[1];
		ownerBuffer.GetData(owner, 0, pixelIndex, 1);
		return owner[0] >= 0;
	}

	public List<List<int>> GetStoneChainLabels(BoardState renderState)
	{
		List<List<int>> labelsByPlayer = new();
		if(activeLabelBuffer == null || renderState == null)
			return labelsByPlayer;

		for(int player = 0; player < renderState.PlayerCount; ++player)
		{
			IReadOnlyList<StonePlacement> stones = renderState.GetStones(player);
			List<int> playerLabels = new(stones.Count);
			for(int i = 0; i < stones.Count; ++i)
				playerLabels.Add(GetChainLabelAtLogicalPosition(renderState, stones[i].position));

			labelsByPlayer.Add(playerLabels);
		}

		return labelsByPlayer;
	}

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

	RenderTextureDescriptor CreateDistributionMapDescriptor()
	{
		return new RenderTextureDescriptor(ComputeTextureSize, ComputeTextureSize, RenderTextureFormat.ARGBFloat, 0)
		{
			enableRandomWrite = true,
			sRGB = false,
			msaaSamples = 1,
			useMipMap = false,
			autoGenerateMips = false,
		};
	}

	RenderTextureDescriptor CreateTerritoryMapDescriptor()
	{
		return new RenderTextureDescriptor(ComputeTextureSize, ComputeTextureSize, RenderTextureFormat.ARGB32, 0)
		{
			enableRandomWrite = true,
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

	void AllocateConnectivityBuffers()
	{
		int pixelCount = ComputeTextureSize * ComputeTextureSize;
		ownerBuffer = new ComputeBuffer(pixelCount, sizeof(int));
		areaPixelCountBuffer = new ComputeBuffer(MaxPlayers, sizeof(int));
		labelBufferA = new ComputeBuffer(pixelCount, sizeof(int));
		labelBufferB = new ComputeBuffer(pixelCount, sizeof(int));
		cclChangedBuffer = new ComputeBuffer(1, sizeof(int));
		chainOwnerBuffer = new ComputeBuffer(pixelCount, sizeof(int));
		chainPixelCountBuffer = new ComputeBuffer(pixelCount, sizeof(int));
		chainLibertyBuffer = new ComputeBuffer(pixelCount, sizeof(int));
		compactChainStatBuffer = new ComputeBuffer(pixelCount, 4 * sizeof(int), ComputeBufferType.Append);
		compactChainStatCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
	}

	int LogicalPositionToPixelIndex(BoardState renderState, Vector2 logicalPosition)
	{
		float span = renderState.Size - 1;
		if(logicalPosition.x < 0 || logicalPosition.x > span)
			return -1;
		if(logicalPosition.y < 0 || logicalPosition.y > span)
			return -1;

		float normalizedX = Mathf.Clamp01(logicalPosition.x / span);
		float normalizedY = Mathf.Clamp01(logicalPosition.y / span);
		int pixelX = Mathf.Clamp(Mathf.RoundToInt(normalizedX * (ComputeTextureSize - 1)), 0, ComputeTextureSize - 1);
		int pixelY = Mathf.Clamp(Mathf.RoundToInt(normalizedY * (ComputeTextureSize - 1)), 0, ComputeTextureSize - 1);
		return pixelY * ComputeTextureSize + pixelX;
	}

	void ConfigureMaterialForTransparency(Material targetMaterial)
	{
		if(targetMaterial.HasProperty("_Mode"))
			targetMaterial.SetFloat("_Mode", 3f);
		if(targetMaterial.HasProperty("_SrcBlend"))
			targetMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
		if(targetMaterial.HasProperty("_DstBlend"))
			targetMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
		if(targetMaterial.HasProperty("_ZWrite"))
			targetMaterial.SetInt("_ZWrite", 0);

		targetMaterial.DisableKeyword("_ALPHATEST_ON");
		targetMaterial.EnableKeyword("_ALPHABLEND_ON");
		targetMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
		targetMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
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

	float LogicalToLocalScale(float logicalLength)
	{
		return logicalLength / (State.Size - 1);
	}
	#endregion

	#region Gizmos
	protected void OnDrawGizmos()
	{
		if(state == null)
			return;

		for(int player = 0; player < state.PlayerCount; ++player)
		{
			Color gizmoColor = player < playerColors.Length ? playerColors[player] : Color.magenta;
			gizmoColor.a = .6f;
			Gizmos.color = gizmoColor;

			foreach(StonePlacement stone in state.GetStones(player))
				DrawStoneGizmo(stone);
		}
	}

	void DrawStoneGizmo(StonePlacement stone)
	{
		float radius = Mathf.Max(.05f, stone.strength * .5f);
		Vector3 localCenter = new(
			stone.position.x / State.Size - .5f,
			stone.position.y / State.Size - .5f,
			0
		);
		Vector3 localScale = new(
			LogicalToLocalScale(radius * 2),
			LogicalToLocalScale(radius * 2),
			LogicalToLocalScale(radius * 2) * GizmoHeightScale
		);

		Matrix4x4 previousMatrix = Gizmos.matrix;
		Gizmos.matrix = transform.localToWorldMatrix * Matrix4x4.TRS(localCenter, Quaternion.identity, localScale);
		Gizmos.DrawWireSphere(Vector3.zero, .5f);
		Gizmos.DrawSphere(Vector3.zero, .5f);
		Gizmos.matrix = previousMatrix;
	}
	#endregion
}
