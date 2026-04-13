Shader "FluidWeiqi/BoardDisplay"
{
	Properties
	{
		_DistributionMap ("Distribution Map", 2D) = "black" {}
		_Threshold ("Threshold", Float) = 0.35355339
		_BlurStrength ("Blur Strength", Range(0, 1)) = 0.6
		_PlayerColor0 ("Player Color 0", Color) = (0, 0, 0, 1)
		_PlayerColor1 ("Player Color 1", Color) = (1, 1, 1, 1)
		_PlayerColor2 ("Player Color 2", Color) = (0.8, 0.2, 0.2, 1)
		_PlayerColor3 ("Player Color 3", Color) = (0.2, 0.4, 0.9, 1)
	}

	SubShader
	{
		Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
		Cull Off
		ZWrite Off
		ZTest Always

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
			float _BlurStrength;
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

			fixed4 frag(v2f input) : SV_Target
			{
				float4 density = SampleSmoothedDensity(input.uv);
				float totalDensity = density.x + density.y + density.z + density.w;
				float maxValue = density.x;
				int bestPlayer = 0;

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

				float mask = step(_Threshold, totalDensity);
				float4 color = GetPlayerColor(bestPlayer);
				color *= mask;
				return color;
			}
			ENDCG
		}
	}
}