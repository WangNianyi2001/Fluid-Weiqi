using UnityEngine;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class BoardUtility
{
	[StructLayout(LayoutKind.Sequential)]
	public struct ChainStat
	{
		public int rootLabel;
		public int owner;
		public int pixelCount;
		public int hasLiberty;
	}

	public const int MaxPlayers = 4;
	public const int ComputeTextureSize = 128;

	const string DistributionShaderResourcePath = "Shaders/BoardDistribution";
	const int MaxCclIterations = ComputeTextureSize * ComputeTextureSize;
	const int ThreadGroupSize = 8;
	const float AreaEpsilon = 1e-6f;

	static RenderTexture distributionMap;
	static RenderTexture territoryMap;
	static ComputeShader distributionShader;
	static ComputeBuffer ownerBuffer;
	static ComputeBuffer areaPixelCountBuffer;
	static ComputeBuffer labelBufferA;
	static ComputeBuffer labelBufferB;
	static ComputeBuffer activeLabelBuffer;
	static ComputeBuffer cclChangedBuffer;
	static ComputeBuffer chainOwnerBuffer;
	static ComputeBuffer chainPixelCountBuffer;
	static ComputeBuffer chainLibertyBuffer;
	static ComputeBuffer compactChainStatBuffer;
	static ComputeBuffer compactChainStatCountBuffer;

	static int distributionKernel;
	static int territoryKernel;
	static int clearAreaPixelCountsKernel;
	static int accumulateAreaPixelCountsKernel;
	static int cclInitKernel;
	static int cclPropagateKernel;
	static int clearChainStatsKernel;
	static int accumulateChainStatsKernel;
	static int compactChainStatsKernel;
	static bool isInitialized;

	public static int ComputeResolution => ComputeTextureSize;
	public static RenderTexture DistributionMap => distributionMap;
	public static bool IsInitialized => isInitialized;

	public static void Initialize()
	{
		if(isInitialized)
			return;

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

		distributionMap = CreateRenderTexture(CreateDistributionMapDescriptor());
		territoryMap = CreateRenderTexture(CreateTerritoryMapDescriptor());
		AllocateConnectivityBuffers();
		isInitialized = true;
	}

	public static void Dispose()
	{
		if(!isInitialized)
			return;

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
		isInitialized = false;
	}

	public static void RenderAnalysis(BoardState state, IReadOnlyList<Color> playerColors)
	{
		if(!isInitialized || state == null || distributionMap == null || territoryMap == null)
			return;

		RenderDistributionMap(state, distributionMap);
		RenderTerritoryMap(state, distributionMap, territoryMap, playerColors);
		RunDominantAreaStats(state, distributionMap);
		RunConnectedComponents();
		RunChainStats();
	}

	public static int[] GetPlayerAreaPixelsByDominance(int playerCount)
	{
		int[] areaByPlayer = new int[playerCount];
		if(!isInitialized || areaPixelCountBuffer == null)
			return areaByPlayer;

		int[] raw = new int[MaxPlayers];
		areaPixelCountBuffer.GetData(raw);
		for(int i = 0; i < areaByPlayer.Length; ++i)
			areaByPlayer[i] = raw[i];

		return areaByPlayer;
	}

	public static List<ChainStat> GetChainStats()
	{
		List<ChainStat> chainStats = new();
		if(!isInitialized || activeLabelBuffer == null)
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

		ChainStat[] stats = new ChainStat[chainCount];
		compactChainStatBuffer.GetData(stats, 0, 0, chainCount);
		chainStats.AddRange(stats);
		return chainStats;
	}

	public static int GetChainLabelAtLogicalPosition(BoardState renderState, Vector2 logicalPosition)
	{
		if(!isInitialized || activeLabelBuffer == null || renderState == null)
			return -1;

		int pixelIndex = LogicalPositionToPixelIndex(renderState, logicalPosition);
		if(pixelIndex < 0)
			return -1;

		int[] label = new int[1];
		activeLabelBuffer.GetData(label, 0, pixelIndex, 1);
		return label[0];
	}

	public static bool IsOccupiedAtLogicalPosition(BoardState renderState, Vector2 logicalPosition)
	{
		if(!isInitialized || ownerBuffer == null || renderState == null)
			return false;

		int pixelIndex = LogicalPositionToPixelIndex(renderState, logicalPosition);
		if(pixelIndex < 0)
			return false;

		int[] owner = new int[1];
		ownerBuffer.GetData(owner, 0, pixelIndex, 1);
		return owner[0] >= 0;
	}

	public static List<List<int>> GetStoneChainLabels(BoardState renderState)
	{
		List<List<int>> labelsByPlayer = new();
		if(!isInitialized || activeLabelBuffer == null || renderState == null)
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

	static void RenderDistributionMap(BoardState state, RenderTexture rt)
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

	static void RenderTerritoryMap(BoardState state, RenderTexture distributionTexture, RenderTexture targetTerritory, IReadOnlyList<Color> playerColors)
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
			Color playerColor = player < playerColors.Count ? playerColors[player] : Color.magenta;
			distributionShader.SetVector($"_PlayerColor{player}", playerColor);
		}

		int groupsX = Mathf.CeilToInt(targetTerritory.width / (float)ThreadGroupSize);
		int groupsY = Mathf.CeilToInt(targetTerritory.height / (float)ThreadGroupSize);
		distributionShader.Dispatch(territoryKernel, groupsX, groupsY, 1);
	}

	static void RunDominantAreaStats(BoardState state, Texture distributionTexture)
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

	static void RunConnectedComponents()
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

	static void RunChainStats()
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

	static RenderTextureDescriptor CreateDistributionMapDescriptor()
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

	static RenderTextureDescriptor CreateTerritoryMapDescriptor()
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

	static RenderTexture CreateRenderTexture(RenderTextureDescriptor descriptor)
	{
		RenderTexture rt = new(descriptor)
		{
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Bilinear,
		};
		rt.Create();
		return rt;
	}

	static void ReleaseRenderTexture(ref RenderTexture rt)
	{
		if(rt == null)
			return;

		if(rt.IsCreated())
			rt.Release();
		Object.Destroy(rt);
		rt = null;
	}

	static void AllocateConnectivityBuffers()
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

	static int LogicalPositionToPixelIndex(BoardState renderState, Vector2 logicalPosition)
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
}
