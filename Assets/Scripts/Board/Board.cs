using UnityEngine;
using System.Collections.Generic;

public class Board : MonoBehaviour
{
	Match Match => Match.Current;

	#region Constants
	const int RenderTextureSize = 1024;
	const string DisplayShaderResourcePath = "Shaders/BoardDisplay";
	#endregion

	#region Inspector
	[SerializeField] new Renderer renderer;
	[SerializeField] int playerCount = 2;
	[SerializeField] float size = 19;
	[SerializeField] float stoneVariance = 1f / Mathf.Sqrt(32);
	[SerializeField] float threshold = .5f;
	#endregion

	#region Properties
	public int ComputeResolution => BoardUtility.ComputeResolution;
	public int PlayerCount
	{
		get => playerCount;
		set => playerCount = Mathf.Clamp(value, 2, BoardUtility.MaxPlayers);
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
	public BoardState State
	{
		get
		{
			if(state == null)
			{
				state = new(playerCount);
				ApplyBoardSettings(state, applySize: true);
			}

			return state;
		}
	}
	#endregion

	#region Runtime state
	BoardState state;
	BoardState previewState;
	bool hasPreview;
	Material material;
	Material displayMaterial;
	RenderTexture mainTexture;
	Shader displayShader;
	#endregion

	#region Unity life cycle
	protected void Awake()
	{
		displayShader = Resources.Load<Shader>(DisplayShaderResourcePath);
		mainTexture = CreateRenderTexture(CreateMainTextureDescriptor());

		material = new(renderer.sharedMaterial);
		renderer.material = material;
		material.mainTexture = mainTexture;

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

		ReleaseRenderTexture(ref mainTexture);

		hasPreview = false;
		previewState = null;
	}

	protected void Start()
	{
		ApplyBoardSettings(State, applySize: true);
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

		state.PlayerCount = playerCount;
		state.StoneVariance = stoneVariance;
		state.Threshold = threshold;
		RefreshRendering();
	}
	#endregion

	#region State management
	void ApplyBoardSettings(BoardState boardState, bool applySize)
	{
		PlayerCount = playerCount;
		boardState.PlayerCount = playerCount;
		if(applySize)
			boardState.Size = size;
		boardState.StoneVariance = stoneVariance;
		boardState.Threshold = threshold;
	}

	public void SetState(BoardState newState)
	{
		state = newState;
		ApplyBoardSettings(state, applySize: true);
		RefreshRendering();
	}

	public void RefreshRendering()
	{
		RefreshRendering(State);
	}

	public void RefreshRendering(BoardState renderState)
	{
		if(mainTexture == null || renderState == null || !BoardUtility.IsInitialized)
			return;

		BoardUtility.RenderAnalysis(renderState, Match.PlayerColors);
		if(displayMaterial != null)
		{
			displayMaterial.SetTexture("_DistributionMap", BoardUtility.DistributionMap);
			displayMaterial.SetFloat("_Threshold", renderState.Threshold);
			for(int player = 0; player < BoardUtility.MaxPlayers; ++player)
			{
				Color playerColor = player < Match.PlayerCount ? Match.PlayerInfos[player].color : Color.magenta;
				displayMaterial.SetColor($"_PlayerColor{player}", playerColor);
			}
			Graphics.Blit(BoardUtility.DistributionMap, mainTexture, displayMaterial);
		}
		else
			Graphics.Blit(BoardUtility.DistributionMap, mainTexture);
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
			return new int[Match.PlayerCount];
		return BoardUtility.GetPlayerAreaPixelsByDominance(Match.PlayerCount);
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
