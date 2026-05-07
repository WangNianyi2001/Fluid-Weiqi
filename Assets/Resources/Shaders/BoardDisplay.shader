Shader "FluidWeiqi/BoardDisplay"
{
	Properties
	{
		[Header(Inspector)]
		[Space]
		_MinAlpha ("Min Alpha", Range(0, 1)) = 0.5
		_AlphaCurve ("Alpha Curve", Range(0, 1)) = 1
		_PlayerColor0 ("Player Color 0", Color) = (0, 0, 0, 1)
		_PlayerColor1 ("Player Color 1", Color) = (1, 1, 1, 1)
		_PlayerColor2 ("Player Color 2", Color) = (0.8, 0.2, 0.2, 1)
		_PlayerColor3 ("Player Color 3", Color) = (0.2, 0.4, 0.9, 1)

		[Header(Runtime)]
		[Space]
		_DistributionMap ("Distribution Map", 2D) = "black" {}
		_Topology ("Topology", Float) = 0
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
				float3 localPos : TEXCOORD1;
				float4 vertex : SV_POSITION;
			};

			TEXTURE2D(_DistributionMap);
			SAMPLER(sampler_DistributionMap);
			float4 _DistributionMap_TexelSize;
			float _MinAlpha;
			float _AlphaCurve;
			float _Topology;
			float4 _PlayerColor0;
			float4 _PlayerColor1;
			float4 _PlayerColor2;
			float4 _PlayerColor3;

			v2f vert(appdata vertexInput)
			{
				v2f output;
				output.vertex = TransformObjectToHClip(vertexInput.vertex.xyz);
				output.uv = vertexInput.uv;
				output.localPos = vertexInput.vertex.xyz;
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

			float2 ComputeSphereUv(float3 localPos)
			{
				float3 dir = normalize(localPos);
				float phi = atan2(dir.x, dir.z);     // (-PI, PI]
				float theta = asin(clamp(dir.y, -1, 1)); // [-PI/2, PI/2]
				float u = frac(phi / (2.0 * PI) + 0.5);
				float v = saturate(theta / PI + 0.5);
				return float2(u, v);
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
				float t = totalDensity - 1;
				if(t < 0)
					return 0;
				t = step(0, t) * t;
				t = exp(t);
				t = (t - 1) / (t + 1);
				return lerp(_MinAlpha, 1, pow(t, _AlphaCurve));
			}

			float4 frag(v2f input) : SV_Target
			{
				float2 sampleUv = _Topology >= 0.5 ? ComputeSphereUv(input.localPos) : input.uv;
				float4 density = SampleDensity(sampleUv);
				float totalDensity = density.x + density.y + density.z + density.w;
				int bestPlayer = FindBestPlayer(density);
				float alpha = AlphaFromDensity(totalDensity);

				float4 playerColor = GetPlayerColor(bestPlayer);
				float luminance = dot(playerColor.rgb, float3(0.299, 0.587, 0.114));
				alpha = pow(alpha, lerp(1, 8, luminance));
				return float4(playerColor.rgb, alpha);
			}
			ENDHLSL
		}
	}
}