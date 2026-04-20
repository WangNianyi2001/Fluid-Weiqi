Shader "FluidWeiqi/BoardDisplay"
{
	Properties
	{
		_DistributionMap ("Distribution Map", 2D) = "black" {}
		_Threshold ("Threshold", Float) = 0.35355339
		_ThresholdSoftness ("Threshold Softness", Range(0.0001, 0.2)) = 0.02
		_BlurStrength ("Blur Strength", Range(0, 1)) = 0.6
		_BaseAlpha ("Base Alpha", Range(0, 1)) = 0.28
		_DominanceAlphaBoost ("Dominance Alpha Boost", Range(0, 2)) = 0.62
		_MaxAlpha ("Max Alpha", Range(0, 1)) = 0.72
		_BorderColor ("Border Color", Color) = (0.12, 0.12, 0.12, 1)
		_BorderStrength ("Border Strength", Range(0, 2)) = 1
		_BorderWidth ("Border Width (Texel)", Range(0.5, 3)) = 1
		_BorderSoftness ("Border Softness", Range(0.0001, 1)) = 0.25
		_PlayerColor0 ("Player Color 0", Color) = (0, 0, 0, 1)
		_PlayerColor1 ("Player Color 1", Color) = (1, 1, 1, 1)
		_PlayerColor2 ("Player Color 2", Color) = (0.8, 0.2, 0.2, 1)
		_PlayerColor3 ("Player Color 3", Color) = (0.2, 0.4, 0.9, 1)
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
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

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

			sampler2D _DistributionMap;
			float4 _DistributionMap_TexelSize;
			float _Threshold;
			float _ThresholdSoftness;
			float _BlurStrength;
			float _BaseAlpha;
			float _DominanceAlphaBoost;
			float _MaxAlpha;
			float4 _BorderColor;
			float _BorderStrength;
			float _BorderWidth;
			float _BorderSoftness;
			float4 _PlayerColor0;
			float4 _PlayerColor1;
			float4 _PlayerColor2;
			float4 _PlayerColor3;

			v2f vert(appdata vertexInput)
			{
				v2f output;
				output.vertex = UnityObjectToClipPos(vertexInput.vertex);
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

			float4 SampleSmoothedDensity(float2 uv)
			{
				float2 px = _DistributionMap_TexelSize.xy;

				float4 c = tex2D(_DistributionMap, uv) * 0.25;
				float4 n = tex2D(_DistributionMap, uv + float2(0, px.y)) * 0.125;
				float4 s = tex2D(_DistributionMap, uv - float2(0, px.y)) * 0.125;
				float4 e = tex2D(_DistributionMap, uv + float2(px.x, 0)) * 0.125;
				float4 w = tex2D(_DistributionMap, uv - float2(px.x, 0)) * 0.125;
				float4 ne = tex2D(_DistributionMap, uv + float2(px.x, px.y)) * 0.0625;
				float4 nw = tex2D(_DistributionMap, uv + float2(-px.x, px.y)) * 0.0625;
				float4 se = tex2D(_DistributionMap, uv + float2(px.x, -px.y)) * 0.0625;
				float4 sw = tex2D(_DistributionMap, uv + float2(-px.x, -px.y)) * 0.0625;

				float4 blurred = c + n + s + e + w + ne + nw + se + sw;
				float4 raw = tex2D(_DistributionMap, uv);
				return lerp(raw, blurred, _BlurStrength);
			}

			void FindBestPlayer(float4 density, out int bestPlayer, out float maxValue, out float totalDensity)
			{
				totalDensity = density.x + density.y + density.z + density.w;
				maxValue = density.x;
				bestPlayer = 0;

				if(density.y > maxValue)
				{
					maxValue = density.y;
					bestPlayer = 1;
				}

				if(density.z > maxValue)
				{
					maxValue = density.z;
					bestPlayer = 2;
				}

				if(density.w > maxValue)
				{
					maxValue = density.w;
					bestPlayer = 3;
				}
			}

			float CoverageFromTotalDensity(float totalDensity)
			{
				float minT = _Threshold - _ThresholdSoftness;
				float maxT = _Threshold + _ThresholdSoftness;
				return smoothstep(minT, maxT, totalDensity);
			}

			fixed4 frag(v2f input) : SV_Target
			{
				float4 density = SampleSmoothedDensity(input.uv);
				int bestPlayer;
				float maxValue;
				float totalDensity;
				FindBestPlayer(density, bestPlayer, maxValue, totalDensity);

				float coverage = CoverageFromTotalDensity(totalDensity);
				float dominance = maxValue / max(totalDensity, 1e-5);
				float alpha = saturate(_BaseAlpha + dominance * _DominanceAlphaBoost);
				alpha = min(alpha, _MaxAlpha);
				alpha *= coverage;

				float2 px = _DistributionMap_TexelSize.xy * _BorderWidth;
				float neighborBorder = 0;
				float coverageBorder = 0;

				float2 offsets[4] =
				{
					float2(px.x, 0),
					float2(-px.x, 0),
					float2(0, px.y),
					float2(0, -px.y)
				};

				for(int i = 0; i < 4; ++i)
				{
					float4 nd = SampleSmoothedDensity(input.uv + offsets[i]);
					int nBest;
					float nMax;
					float nTotal;
					FindBestPlayer(nd, nBest, nMax, nTotal);
					float nCoverage = CoverageFromTotalDensity(nTotal);

					if(coverage > 0.001 && nCoverage > 0.001 && nBest != bestPlayer)
						neighborBorder = 1;

					coverageBorder = max(coverageBorder, abs(coverage - nCoverage));
				}

				float borderFromCoverage = smoothstep(_BorderSoftness * 0.25, _BorderSoftness, coverageBorder);
				float borderMix = saturate(max(neighborBorder, borderFromCoverage) * _BorderStrength);

				float4 playerColor = GetPlayerColor(bestPlayer);
				float3 rgb = lerp(playerColor.rgb, _BorderColor.rgb, borderMix);
				float outAlpha = saturate(max(alpha, borderMix * _BorderColor.a * coverage));
				return float4(rgb, outAlpha);
			}
			ENDCG
		}
	}
}