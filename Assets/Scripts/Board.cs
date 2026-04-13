using UnityEngine;
using System.Collections.Generic;

public class Board : MonoBehaviour
{
	#region Constants
	const int MaxPlayers = 4;
	const int TextureSize = 1024;
	const string DistributionShaderResourcePath = "Shaders/BoardDistribution";
	const string DisplayShaderResourcePath = "Shaders/BoardDisplay";
	const float GizmoHeightScale = 0.1f;
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
	ComputeShader distributionShader;
	Shader displayShader;
	#endregion

	#region Unity life cycle
	protected void Awake()
	{
		distributionShader = Resources.Load<ComputeShader>(DistributionShaderResourcePath);
		displayShader = Resources.Load<Shader>(DisplayShaderResourcePath);

		mainTexture = RenderTexture.GetTemporary(CreateMainTextureDescriptor());
		mainTexture.wrapMode = TextureWrapMode.Clamp;
		mainTexture.filterMode = FilterMode.Bilinear;

		material = new(renderer.sharedMaterial);
		renderer.material = material;
		ConfigureMaterialForTransparency(material);
		material.mainTexture = mainTexture;

		displayMaterial = new(displayShader);
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

		if(mainTexture != null)
		{
			RenderTexture.ReleaseTemporary(mainTexture);
			mainTexture = null;
		}
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
		if(mainTexture == null || renderState == null)
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
		distributionMap = RenderTexture.GetTemporary(CreateDistributionMapDescriptor());
		distributionMap.wrapMode = TextureWrapMode.Clamp;
		distributionMap.filterMode = FilterMode.Bilinear;

		RenderDistributionMap(state, distributionMap);
		UpdateDisplayMaterial(state, distributionMap);
		Graphics.Blit(null, mainTexture, displayMaterial);

		RenderTexture.ReleaseTemporary(distributionMap);
		distributionMap = null;
	}

	void RenderDistributionMap(BoardState state, RenderTexture rt)
	{
		int kernel = distributionShader.FindKernel("CSMain");
		ComputeBuffer[] stoneBuffers = new ComputeBuffer[MaxPlayers];

		try
		{
			distributionShader.SetTexture(kernel, "_Output", rt);
			distributionShader.SetFloat("_BoardSize", state.Size);
			distributionShader.SetFloat("_StoneVariance", Mathf.Max(0.0001f, state.StoneVariance));

			for(int player = 0; player < MaxPlayers; ++player)
			{
				int stoneCount = player < state.PlayerCount ? state.GetStones(player).Count : 0;
				stoneBuffers[player] = new ComputeBuffer(Mathf.Max(1, stoneCount), 3 * sizeof(float));
				distributionShader.SetInt($"_Player{player}StoneCount", stoneCount);
				distributionShader.SetBuffer(kernel, $"_Player{player}Stones", stoneBuffers[player]);

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

			int groupsX = Mathf.CeilToInt(rt.width / 8f);
			int groupsY = Mathf.CeilToInt(rt.height / 8f);
			distributionShader.Dispatch(kernel, groupsX, groupsY, 1);
		}
		finally
		{
			for(int i = 0; i < stoneBuffers.Length; ++i)
				stoneBuffers[i]?.Release();
		}
	}

	RenderTextureDescriptor CreateMainTextureDescriptor()
	{
		return new RenderTextureDescriptor(TextureSize, TextureSize, RenderTextureFormat.ARGB32, 0)
		{
			sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear,
			msaaSamples = 1,
			useMipMap = false,
			autoGenerateMips = false,
		};
	}

	RenderTextureDescriptor CreateDistributionMapDescriptor()
	{
		return new RenderTextureDescriptor(TextureSize, TextureSize, RenderTextureFormat.ARGBFloat, 0)
		{
			enableRandomWrite = true,
			sRGB = false,
			msaaSamples = 1,
			useMipMap = false,
			autoGenerateMips = false,
		};
	}

	void UpdateDisplayMaterial(BoardState state, Texture distributionTexture)
	{
		displayMaterial.SetTexture("_DistributionMap", distributionTexture);
		displayMaterial.SetFloat("_Threshold", state.Threshold);

		for(int player = 0; player < MaxPlayers; ++player)
		{
			Color playerColor = player < playerColors.Length ? playerColors[player] : Color.magenta;
			displayMaterial.SetColor($"_PlayerColor{player}", playerColor);
		}
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
		return new Vector2((localPosition.x + .5f) * State.Size, (localPosition.y + .5f) * State.Size);
	}

	public Vector3 LogicalToWorldPosition(Vector2 logicalPosition)
	{
		Vector3 localPosition = new(
			logicalPosition.x / State.Size - .5f,
			logicalPosition.y / State.Size - .5f,
			0
		);
		return transform.TransformPoint(localPosition);
	}

	float LogicalToLocalScale(float logicalLength)
	{
		return logicalLength / State.Size;
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
