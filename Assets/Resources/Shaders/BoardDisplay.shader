Shader "FluidWeiqi/BoardDisplay"
{
	Properties
	{
		[Header(Inspector)]
		[Space]
		_AlphaCurve ("Alpha Curve", Range(0, 1)) = 1
		_PlayerColor0 ("Player Color 0", Color) = (0, 0, 0, 1)
		_PlayerColor1 ("Player Color 1", Color) = (1, 1, 1, 1)
		_PlayerColor2 ("Player Color 2", Color) = (0.8, 0.2, 0.2, 1)
		_PlayerColor3 ("Player Color 3", Color) = (0.2, 0.4, 0.9, 1)

		[Header(Runtime)]
		[Space]
		_DistributionMap ("Distribution Map", 2D) = "black" {}
		_Threshold ("Threshold", Float) = 0.5
	}

	SubShader
	{
		Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
		Cull Off
		ZWrite Off
		ZTest LEqual
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			TEXTURE2D(_DistributionMap);
			SAMPLER(sampler_DistributionMap);
			float4 _DistributionMap_TexelSize;
			float _Threshold;
			float _AlphaCurve;
			float4 _PlayerColor0;
			float4 _PlayerColor1;
			float4 _PlayerColor2;
			float4 _PlayerColor3;

			v2f vert(appdata vertexInput)
			{
				v2f output;
				output.vertex = TransformObjectToHClip(vertexInput.vertex.xyz);
				output.uv = vertexInput.uv;
				return output;
			}

			float4 GetPlayerColor(int index)
			{
				if(index == 0)
					return _PlayerColor0;
				if(index == 1)
					return _PlayerColor1;
				if(index == 2)
					return _PlayerColor2;
				return _PlayerColor3;
			}

			float4 SampleDensity(float2 uv)
			{
				return SAMPLE_TEXTURE2D(_DistributionMap, sampler_DistributionMap, uv);
			}

			int FindBestPlayer(float4 density)
			{
				int best = 0;
				float maxVal = density.x;
				if(density.y > maxVal) { maxVal = density.y; best = 1; }
				if(density.z > maxVal) { maxVal = density.z; best = 2; }
				if(density.w > maxVal) { best = 3; }
				return best;
			}

			float AlphaFromDensity(float totalDensity)
			{
				float a = totalDensity < _Threshold ? 0 : totalDensity;
				float exponent = max(_AlphaCurve, 1e-4);
				return pow(a, exponent);
			}

			float4 frag(v2f input) : SV_Target
			{
				float4 density = SampleDensity(input.uv);
				float totalDensity = density.x + density.y + density.z + density.w;
				int bestPlayer = FindBestPlayer(density);
				float alpha = AlphaFromDensity(totalDensity);

				float4 playerColor = GetPlayerColor(bestPlayer);
				return float4(playerColor.rgb, alpha);
			}
			ENDHLSL
		}
	}
}