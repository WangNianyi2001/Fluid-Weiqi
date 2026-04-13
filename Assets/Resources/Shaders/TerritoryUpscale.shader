Shader "FluidWeiqi/TerritoryUpscale"
{
	Properties
	{
		_MainTex ("Territory", 2D) = "black" {}
		_EdgeBlend ("Edge Blend", Range(0, 1)) = 0.35
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

			sampler2D _MainTex;
			float4 _MainTex_TexelSize;
			float _EdgeBlend;

			v2f vert(appdata input)
			{
				v2f output;
				output.vertex = UnityObjectToClipPos(input.vertex);
				output.uv = input.uv;
				return output;
			}

			float4 SampleNearest(float2 uv)
			{
				float2 texSize = 1.0 / _MainTex_TexelSize.xy;
				float2 pixel = floor(uv * texSize);
				float2 nearestUv = (pixel + 0.5) * _MainTex_TexelSize.xy;
				return tex2D(_MainTex, nearestUv);
			}

			fixed4 frag(v2f input) : SV_Target
			{
				float2 px = _MainTex_TexelSize.xy;
				float4 c = SampleNearest(input.uv);
				float4 n = SampleNearest(input.uv + float2(0, px.y));
				float4 s = SampleNearest(input.uv - float2(0, px.y));
				float4 e = SampleNearest(input.uv + float2(px.x, 0));
				float4 w = SampleNearest(input.uv - float2(px.x, 0));

				float alphaEdge = abs(c.a - n.a) + abs(c.a - s.a) + abs(c.a - e.a) + abs(c.a - w.a);
				float colorEdge = length(c.rgb - n.rgb) + length(c.rgb - s.rgb) + length(c.rgb - e.rgb) + length(c.rgb - w.rgb);
				float edge = saturate(alphaEdge + colorEdge * 0.5);

				float4 neighborhood = (c + n + s + e + w) * 0.2;
				return lerp(c, neighborhood, edge * _EdgeBlend);
			}
			ENDCG
		}
	}
}
