using UnityEngine;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class BoardUtility
{
	#region Types

	[StructLayout(LayoutKind.Sequential)]
	public struct ChainStat
	{
		public int rootLabel;
		public int owner;
		public int pixelCount;
		public int hasLiberty;
	}

	[StructLayout(LayoutKind.Sequential)]
	struct GpuStonePlacement
	{
		public Vector2 position;
		public float strength;
	}

	public sealed class BoardCaches
	{
		public RenderTexture distributionMap;
		public RenderTexture territoryMap;
		public ComputeShader distributionShader;
		public ComputeBuffer ownerBuffer;
		public ComputeBuffer areaPixelCountBuffer;
		public ComputeBuffer labelBufferA;
		public ComputeBuffer labelBufferB;
		public ComputeBuffer activeLabelBuffer;
		public ComputeBuffer cclChangedBuffer;
		public ComputeBuffer chainOwnerBuffer;
		public ComputeBuffer chainPixelCountBuffer;
		public ComputeBuffer chainLibertyBuffer;
		public ComputeBuffer compactChainStatBuffer;
		public ComputeBuffer compactChainStatCountBuffer;

		public int distributionKernel;
		public int territoryKernel;
		public int clearAreaPixelCountsKernel;
		public int accumulateAreaPixelCountsKernel;
		public int cclInitKernel;
		public int cclPropagateKernel;
		public int clearChainStatsKernel;
		public int accumulateChainStatsKernel;
		public int compactChainStatsKernel;

		public int[] ownerDataCache;
		public int[] labelDataCache;
		public bool ownerDataCacheValid;
		public bool labelDataCacheValid;

		public bool isInitialized;
	}

	#endregion

	#region Constants

	public const int MaxPlayers = 4;
	public const int ComputeTextureSize = 128;

	const string DistributionShaderResourcePath = "Shaders/BoardDistribution";
	const int MaxCclIterations = ComputeTextureSize * ComputeTextureSize;
	const int ThreadGroupSize = 8;
	const float AreaEpsilon = 1e-6f;

	#endregion

	#region Init / Dispose

	public static void Initialize(BoardCaches c)
	{
		if(c.isInitialized)
			return;

		c.distributionShader = Resources.Load<ComputeShader>(DistributionShaderResourcePath);
		c.distributionKernel = c.distributionShader.FindKernel("CSDistribution");
		c.territoryKernel = c.distributionShader.FindKernel("CSTerritory");
		c.clearAreaPixelCountsKernel = c.distributionShader.FindKernel("CSClearAreaPixelCounts");
		c.accumulateAreaPixelCountsKernel = c.distributionShader.FindKernel("CSAccumulateAreaPixelCounts");
		c.cclInitKernel = c.distributionShader.FindKernel("CSInitLabels");
		c.cclPropagateKernel = c.distributionShader.FindKernel("CSPropagateLabels");
		c.clearChainStatsKernel = c.distributionShader.FindKernel("CSClearChainStats");
		c.accumulateChainStatsKernel = c.distributionShader.FindKernel("CSAccumulateChainStats");
		c.compactChainStatsKernel = c.distributionShader.FindKernel("CSCompactChainStats");

		c.distributionMap = CreateRenderTexture(CreateDistributionMapDescriptor());
		c.territoryMap = CreateRenderTexture(CreateTerritoryMapDescriptor());
		AllocateConnectivityBuffers(c);
		c.isInitialized = true;
	}

	public static void Dispose(BoardCaches c)
	{
		if(!c.isInitialized)
			return;

		ReleaseRenderTexture(ref c.distributionMap);
		ReleaseRenderTexture(ref c.territoryMap);
		ReleaseBuffer(ref c.ownerBuffer);
		ReleaseBuffer(ref c.areaPixelCountBuffer);
		ReleaseBuffer(ref c.labelBufferA);
		ReleaseBuffer(ref c.labelBufferB);
		ReleaseBuffer(ref c.cclChangedBuffer);
		ReleaseBuffer(ref c.chainOwnerBuffer);
		ReleaseBuffer(ref c.chainPixelCountBuffer);
		ReleaseBuffer(ref c.chainLibertyBuffer);
		ReleaseBuffer(ref c.compactChainStatBuffer);
		ReleaseBuffer(ref c.compactChainStatCountBuffer);
		c.activeLabelBuffer = null;
		c.ownerDataCache = null;
		c.labelDataCache = null;
		c.ownerDataCacheValid = false;
		c.labelDataCacheValid = false;
		c.isInitialized = false;
	}

	#endregion

	#region Analysis

	public static void RenderAnalysis(BoardCaches c, BoardState state, IReadOnlyList<Color> playerColors)
	{
		if(!c.isInitialized || state == null || c.distributionMap == null || c.territoryMap == null)
			return;

		RenderDistributionMap(c, state);
		RenderTerritoryMap(c, state, playerColors);
		RunDominantAreaStats(c, state);
		RunConnectedComponents(c);
		RunChainStats(c);
	}

	public static float[] GetPlayerAreasByDominance(Board b, int playerCount)
	{
		var c = b.Caches;
		float scale = Mathf.Pow(b.State.Size, 2) / Mathf.Pow(ComputeTextureSize, 2);

		float[] areaByPlayer = new float[playerCount];
		if(!c.isInitialized || c.areaPixelCountBuffer == null)
			return areaByPlayer;

		int[] raw = new int[MaxPlayers];
		c.areaPixelCountBuffer.GetData(raw);
		for(int i = 0; i < areaByPlayer.Length; ++i)
			areaByPlayer[i] = raw[i] * scale;
		return areaByPlayer;
	}

	public static List<ChainStat> GetChainStats(BoardCaches c)
	{
		List<ChainStat> chainStats = new();
		if(!c.isInitialized || c.activeLabelBuffer == null)
			return chainStats;

		c.compactChainStatBuffer.SetCounterValue(0);
		c.distributionShader.SetInt("_TextureWidth", ComputeTextureSize);
		c.distributionShader.SetInt("_TextureHeight", ComputeTextureSize);
		c.distributionShader.SetBuffer(c.compactChainStatsKernel, "_OwnerBuffer", c.ownerBuffer);
		c.distributionShader.SetBuffer(c.compactChainStatsKernel, "_LabelBufferRead", c.activeLabelBuffer);
		c.distributionShader.SetBuffer(c.compactChainStatsKernel, "_ChainOwnerBuffer", c.chainOwnerBuffer);
		c.distributionShader.SetBuffer(c.compactChainStatsKernel, "_ChainPixelCountBuffer", c.chainPixelCountBuffer);
		c.distributionShader.SetBuffer(c.compactChainStatsKernel, "_ChainHasLibertyBuffer", c.chainLibertyBuffer);
		c.distributionShader.SetBuffer(c.compactChainStatsKernel, "_CompactChainStatBuffer", c.compactChainStatBuffer);

		int groupsX = Mathf.CeilToInt(ComputeTextureSize / (float)ThreadGroupSize);
		int groupsY = Mathf.CeilToInt(ComputeTextureSize / (float)ThreadGroupSize);
		c.distributionShader.Dispatch(c.compactChainStatsKernel, groupsX, groupsY, 1);

		ComputeBuffer.CopyCount(c.compactChainStatBuffer, c.compactChainStatCountBuffer, 0);
		int[] countArray = new int[1];
		c.compactChainStatCountBuffer.GetData(countArray);
		int chainCount = countArray[0];
		if(chainCount <= 0)
			return chainStats;

		ChainStat[] stats = new ChainStat[chainCount];
		c.compactChainStatBuffer.GetData(stats, 0, 0, chainCount);
		chainStats.AddRange(stats);
		return chainStats;
	}

	public static int GetChainLabelAtAbsolutePosition(BoardCaches c, BoardState renderState, Vector2 absolutePosition)
	{
		if(!c.isInitialized || c.activeLabelBuffer == null || renderState == null)
			return -1;
		if(!TryEnsureLabelDataCache(c))
			return -1;

		int pixelIndex = AbsolutePositionToPixelIndex(renderState, absolutePosition);
		if(pixelIndex < 0)
			return -1;

		return c.labelDataCache[pixelIndex];
	}

	public static bool IsOccupiedAtAbsolutePosition(BoardCaches c, BoardState renderState, Vector2 absolutePosition)
	{
		if(!c.isInitialized || c.ownerBuffer == null || renderState == null)
			return false;
		if(!TryEnsureOwnerDataCache(c))
			return false;

		int pixelIndex = AbsolutePositionToPixelIndex(renderState, absolutePosition);
		if(pixelIndex < 0)
			return false;

		return c.ownerDataCache[pixelIndex] >= 0;
	}

	public static int GetChainLabelAtLogicalPosition(BoardCaches c, BoardState renderState, Vector2 logicalPosition)
	{
		return GetChainLabelAtAbsolutePosition(c, renderState, logicalPosition);
	}

	public static bool IsOccupiedAtLogicalPosition(BoardCaches c, BoardState renderState, Vector2 logicalPosition)
	{
		return IsOccupiedAtAbsolutePosition(c, renderState, logicalPosition);
	}

	public static List<List<int>> GetStoneChainLabels(BoardCaches c, BoardState renderState)
	{
		List<List<int>> labelsByPlayer = new();
		if(!c.isInitialized || c.activeLabelBuffer == null || renderState == null)
			return labelsByPlayer;

		for(int player = 0; player < renderState.PlayerCount; ++player)
		{
			IReadOnlyList<StonePlacement> stones = renderState.GetStones(player);
			List<int> playerLabels = new(stones.Count);
			for(int i = 0; i < stones.Count; ++i)
				playerLabels.Add(GetChainLabelAtAbsolutePosition(c, renderState, stones[i].position));
			labelsByPlayer.Add(playerLabels);
		}

		return labelsByPlayer;
	}

	/// <summary>
	/// Standard Go placement rule: occupancy check, suicide check, capture.
	/// Caches must be current for <paramref name="state"/> before calling.
	/// On success, caches are left reflecting <paramref name="newState"/>.
	/// </summary>
	public static bool TryPlaceStoneStandard(
		BoardCaches c,
		BoardState state,
		int player,
		Vector2 position,
		out BoardState newState,
		float strength = 1)
	{
		newState = null;
		if(player < 0 || player >= state.PlayerCount) return false;
		if(strength <= 0) return false;
		if(position.x < 0 || position.x >= state.Size || position.y < 0 || position.y >= state.Size) return false;
		if(IsOccupiedAtAbsolutePosition(c, state, position)) return false;

		BoardState previewState = new(state);
		previewState.AddStone(player, position, strength);

		RunGameplayAnalysis(c, previewState);

		List<ChainStat> chainStats = GetChainStats(c);
		Dictionary<int, ChainStat> chainStatsByRoot = new(chainStats.Count);
		HashSet<int> capturedRoots = new();

		for(int i = 0; i < chainStats.Count; ++i)
		{
			ChainStat cs = chainStats[i];
			chainStatsByRoot[cs.rootLabel] = cs;
			if(cs.owner != player && cs.hasLiberty == 0)
				capturedRoots.Add(cs.rootLabel);
		}

		int placedRoot = GetChainLabelAtAbsolutePosition(c, previewState, position);
		bool hasLiberty = chainStatsByRoot.TryGetValue(placedRoot, out ChainStat placedStat) && placedStat.hasLiberty != 0;
		if(capturedRoots.Count == 0 && !hasLiberty)
			return false;

		if(capturedRoots.Count > 0)
			RemoveCapturedStonesStandard(c, previewState, capturedRoots, player);

		newState = previewState;
		return true;
	}

	#endregion

	#region Board parameters

	public static int GetStarEdgeOffset(int boardSize)
	{
		if(boardSize >= 11)
			return 3;
		if(boardSize >= 5)
			return 2;
		return 1;
	}

	#endregion

	#region Private computation

	static void RunGameplayAnalysis(BoardCaches c, BoardState state)
	{
		RenderDistributionMap(c, state);
		RenderTerritoryMap(c, state, System.Array.Empty<Color>());
		RunDominantAreaStats(c, state);
		RunConnectedComponents(c);
		RunChainStats(c);
	}

	static void RemoveCapturedStonesStandard(BoardCaches c, BoardState state, HashSet<int> capturedRoots, int currentPlayer)
	{
		List<List<int>> stoneChainLabels = GetStoneChainLabels(c, state);
		for(int player = 0; player < state.PlayerCount; ++player)
		{
			if(player == currentPlayer)
				continue;

			List<int> playerLabels = stoneChainLabels[player];
			for(int stoneIndex = playerLabels.Count - 1; stoneIndex >= 0; --stoneIndex)
			{
				if(capturedRoots.Contains(playerLabels[stoneIndex]))
					state.RemoveStoneAt(player, stoneIndex);
			}
		}
	}

	static void RenderDistributionMap(BoardCaches c, BoardState state)
	{
		ComputeBuffer[] stoneBuffers = new ComputeBuffer[MaxPlayers];
		try
		{
			c.distributionShader.SetTexture(c.distributionKernel, "_DistributionOutput", c.distributionMap);
			c.distributionShader.SetFloat("_BoardSize", state.Size - 1);
			c.distributionShader.SetFloat("_StoneHardness", Mathf.Clamp(state.StoneHardness, 0f, 0.9999f));
			c.distributionShader.SetInt("_TextureWidth", c.distributionMap.width);
			c.distributionShader.SetInt("_TextureHeight", c.distributionMap.height);

			for(int player = 0; player < MaxPlayers; ++player)
			{
				int stoneCount = player < state.PlayerCount ? state.GetStones(player).Count : 0;
				stoneBuffers[player] = new ComputeBuffer(Mathf.Max(1, stoneCount), 3 * sizeof(float));
				c.distributionShader.SetInt($"_Player{player}StoneCount", stoneCount);
				c.distributionShader.SetBuffer(c.distributionKernel, $"_Player{player}Stones", stoneBuffers[player]);

				if(stoneCount == 0)
					continue;

				IReadOnlyList<StonePlacement> source = state.GetStones(player);
				GpuStonePlacement[] gpuStones = new GpuStonePlacement[stoneCount];
				for(int i = 0; i < stoneCount; ++i)
				{
					gpuStones[i] = new GpuStonePlacement
					{
						position = source[i].position,
						strength = source[i].strength,
					};
				}
				stoneBuffers[player].SetData(gpuStones);
			}

			int groupsX = Mathf.CeilToInt(c.distributionMap.width / (float)ThreadGroupSize);
			int groupsY = Mathf.CeilToInt(c.distributionMap.height / (float)ThreadGroupSize);
			c.distributionShader.Dispatch(c.distributionKernel, groupsX, groupsY, 1);
		}
		finally
		{
			for(int i = 0; i < stoneBuffers.Length; ++i)
				stoneBuffers[i]?.Release();
		}
	}

	static void RenderTerritoryMap(BoardCaches c, BoardState state, IReadOnlyList<Color> playerColors)
	{
		c.ownerDataCacheValid = false;

		c.distributionShader.SetTexture(c.territoryKernel, "_DistributionInput", c.distributionMap);
		c.distributionShader.SetTexture(c.territoryKernel, "_TerritoryOutput", c.territoryMap);
		c.distributionShader.SetBuffer(c.territoryKernel, "_OwnerBuffer", c.ownerBuffer);
		c.distributionShader.SetInt("_TextureWidth", c.territoryMap.width);
		c.distributionShader.SetInt("_TextureHeight", c.territoryMap.height);
		c.distributionShader.SetInt("_PlayerCount", state.PlayerCount);

		for(int player = 0; player < MaxPlayers; ++player)
		{
			Color playerColor = player < playerColors.Count ? playerColors[player] : Color.magenta;
			c.distributionShader.SetVector($"_PlayerColor{player}", playerColor);
		}

		int groupsX = Mathf.CeilToInt(c.territoryMap.width / (float)ThreadGroupSize);
		int groupsY = Mathf.CeilToInt(c.territoryMap.height / (float)ThreadGroupSize);
		c.distributionShader.Dispatch(c.territoryKernel, groupsX, groupsY, 1);
	}

	static void RunDominantAreaStats(BoardCaches c, BoardState state)
	{
		c.distributionShader.SetInt("_TextureWidth", ComputeTextureSize);
		c.distributionShader.SetInt("_TextureHeight", ComputeTextureSize);
		c.distributionShader.SetInt("_PlayerCount", state.PlayerCount);
		c.distributionShader.SetFloat("_AreaEpsilon", AreaEpsilon);
		c.distributionShader.SetBuffer(c.clearAreaPixelCountsKernel, "_AreaPixelCountBuffer", c.areaPixelCountBuffer);
		c.distributionShader.Dispatch(c.clearAreaPixelCountsKernel, 1, 1, 1);

		c.distributionShader.SetTexture(c.accumulateAreaPixelCountsKernel, "_DistributionInput", c.distributionMap);
		c.distributionShader.SetBuffer(c.accumulateAreaPixelCountsKernel, "_AreaPixelCountBuffer", c.areaPixelCountBuffer);
		int groupsX = Mathf.CeilToInt(ComputeTextureSize / (float)ThreadGroupSize);
		int groupsY = Mathf.CeilToInt(ComputeTextureSize / (float)ThreadGroupSize);
		c.distributionShader.Dispatch(c.accumulateAreaPixelCountsKernel, groupsX, groupsY, 1);
	}

	static void RunConnectedComponents(BoardCaches c)
	{
		c.labelDataCacheValid = false;

		c.distributionShader.SetInt("_TextureWidth", ComputeTextureSize);
		c.distributionShader.SetInt("_TextureHeight", ComputeTextureSize);
		c.distributionShader.SetBuffer(c.cclInitKernel, "_OwnerBuffer", c.ownerBuffer);
		c.distributionShader.SetBuffer(c.cclInitKernel, "_LabelBufferWrite", c.labelBufferA);

		int groupsX = Mathf.CeilToInt(ComputeTextureSize / (float)ThreadGroupSize);
		int groupsY = Mathf.CeilToInt(ComputeTextureSize / (float)ThreadGroupSize);
		c.distributionShader.Dispatch(c.cclInitKernel, groupsX, groupsY, 1);

		ComputeBuffer readBuffer = c.labelBufferA;
		ComputeBuffer writeBuffer = c.labelBufferB;
		int[] changed = new int[1];
		for(int i = 0; i < MaxCclIterations; ++i)
		{
			changed[0] = 0;
			c.cclChangedBuffer.SetData(changed);

			c.distributionShader.SetBuffer(c.cclPropagateKernel, "_OwnerBuffer", c.ownerBuffer);
			c.distributionShader.SetBuffer(c.cclPropagateKernel, "_LabelBufferRead", readBuffer);
			c.distributionShader.SetBuffer(c.cclPropagateKernel, "_LabelBufferWrite", writeBuffer);
			c.distributionShader.SetBuffer(c.cclPropagateKernel, "_CclChangedBuffer", c.cclChangedBuffer);
			c.distributionShader.Dispatch(c.cclPropagateKernel, groupsX, groupsY, 1);

			(readBuffer, writeBuffer) = (writeBuffer, readBuffer);

			c.cclChangedBuffer.GetData(changed);
			if(changed[0] == 0)
				break;
		}

		c.activeLabelBuffer = readBuffer;
	}

	static void RunChainStats(BoardCaches c)
	{
		if(c.activeLabelBuffer == null)
			return;

		c.distributionShader.SetInt("_TextureWidth", ComputeTextureSize);
		c.distributionShader.SetInt("_TextureHeight", ComputeTextureSize);

		int groupsX = Mathf.CeilToInt(ComputeTextureSize / (float)ThreadGroupSize);
		int groupsY = Mathf.CeilToInt(ComputeTextureSize / (float)ThreadGroupSize);

		c.distributionShader.SetBuffer(c.clearChainStatsKernel, "_ChainOwnerBuffer", c.chainOwnerBuffer);
		c.distributionShader.SetBuffer(c.clearChainStatsKernel, "_ChainPixelCountBuffer", c.chainPixelCountBuffer);
		c.distributionShader.SetBuffer(c.clearChainStatsKernel, "_ChainHasLibertyBuffer", c.chainLibertyBuffer);
		c.distributionShader.Dispatch(c.clearChainStatsKernel, groupsX, groupsY, 1);

		c.distributionShader.SetBuffer(c.accumulateChainStatsKernel, "_OwnerBuffer", c.ownerBuffer);
		c.distributionShader.SetBuffer(c.accumulateChainStatsKernel, "_LabelBufferRead", c.activeLabelBuffer);
		c.distributionShader.SetBuffer(c.accumulateChainStatsKernel, "_ChainOwnerBuffer", c.chainOwnerBuffer);
		c.distributionShader.SetBuffer(c.accumulateChainStatsKernel, "_ChainPixelCountBuffer", c.chainPixelCountBuffer);
		c.distributionShader.SetBuffer(c.accumulateChainStatsKernel, "_ChainHasLibertyBuffer", c.chainLibertyBuffer);
		c.distributionShader.Dispatch(c.accumulateChainStatsKernel, groupsX, groupsY, 1);
	}

	static int AbsolutePositionToPixelIndex(BoardState renderState, Vector2 absolutePosition)
	{
		float span = renderState.BoardStateExtent;
		if(absolutePosition.x < 0 || absolutePosition.x > span)
			return -1;
		if(absolutePosition.y < 0 || absolutePosition.y > span)
			return -1;

		float normalizedX = Mathf.Clamp01(absolutePosition.x / span);
		float normalizedY = Mathf.Clamp01(absolutePosition.y / span);
		int pixelX = Mathf.Clamp((int)(normalizedX * ComputeTextureSize), 0, ComputeTextureSize - 1);
		int pixelY = Mathf.Clamp((int)(normalizedY * ComputeTextureSize), 0, ComputeTextureSize - 1);
		return pixelY * ComputeTextureSize + pixelX;
	}

	static bool TryEnsureOwnerDataCache(BoardCaches c)
	{
		if(!c.isInitialized || c.ownerBuffer == null)
			return false;

		int pixelCount = ComputeTextureSize * ComputeTextureSize;
		if(c.ownerDataCache == null || c.ownerDataCache.Length != pixelCount)
			c.ownerDataCache = new int[pixelCount];

		if(!c.ownerDataCacheValid)
		{
			c.ownerBuffer.GetData(c.ownerDataCache);
			c.ownerDataCacheValid = true;
		}

		return true;
	}

	static bool TryEnsureLabelDataCache(BoardCaches c)
	{
		if(!c.isInitialized || c.activeLabelBuffer == null)
			return false;

		int pixelCount = ComputeTextureSize * ComputeTextureSize;
		if(c.labelDataCache == null || c.labelDataCache.Length != pixelCount)
			c.labelDataCache = new int[pixelCount];

		if(!c.labelDataCacheValid)
		{
			c.activeLabelBuffer.GetData(c.labelDataCache);
			c.labelDataCacheValid = true;
		}

		return true;
	}

	static void AllocateConnectivityBuffers(BoardCaches c)
	{
		int pixelCount = ComputeTextureSize * ComputeTextureSize;
		c.ownerBuffer = new ComputeBuffer(pixelCount, sizeof(int));
		c.areaPixelCountBuffer = new ComputeBuffer(MaxPlayers, sizeof(int));
		c.labelBufferA = new ComputeBuffer(pixelCount, sizeof(int));
		c.labelBufferB = new ComputeBuffer(pixelCount, sizeof(int));
		c.cclChangedBuffer = new ComputeBuffer(1, sizeof(int));
		c.chainOwnerBuffer = new ComputeBuffer(pixelCount, sizeof(int));
		c.chainPixelCountBuffer = new ComputeBuffer(pixelCount, sizeof(int));
		c.chainLibertyBuffer = new ComputeBuffer(pixelCount, sizeof(int));
		c.compactChainStatBuffer = new ComputeBuffer(pixelCount, 4 * sizeof(int), ComputeBufferType.Append);
		c.compactChainStatCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
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

	static void ReleaseBuffer(ref ComputeBuffer buffer)
	{
		buffer?.Release();
		buffer = null;
	}

	#endregion
}
