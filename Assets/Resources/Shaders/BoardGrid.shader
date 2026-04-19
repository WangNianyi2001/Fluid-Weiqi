Shader "FluidWeiqi/BoardGrid"
{
	Properties
	{
		_GridColor ("Grid Color", Color) = (0, 0, 0, 1)
		_BoardSize ("Board Size", Float) = 19
		_LineThickness ("Line Thickness", Range(0.002, 0.2)) = 0.045
		_EdgeLineMultiplier ("Edge Line Multiplier", Range(1, 4)) = 1.6
		_StarPointRadius ("Star Point Radius", Range(0.02, 0.5)) = 0.13
		_StarPointSoftness ("Star Point Softness", Range(0.002, 0.2)) = 0.03
		_AlphaCutout ("Alpha Cutout", Range(0, 1)) = 0.01
		_StarEdgeOffset ("Star Edge Offset", Float) = 3
	}

	SubShader
	{
		Tags { "RenderType" = "TransparentCutout" "Queue" = "Geometry" }
		Cull Off
		ZWrite Off
		ZTest LEqual
		Offset -1, -1

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
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			float4 _GridColor;
			float _BoardSize;
			float _LineThickness;
			float _EdgeLineMultiplier;
			float _StarPointRadius;
			float _StarPointSoftness;
			float _AlphaCutout;
			float _StarEdgeOffset;

			v2f vert(appdata vertexInput)
			{
				v2f output;
				output.vertex = UnityObjectToClipPos(vertexInput.vertex);
				output.uv = vertexInput.uv;
				return output;
			}

			float GetAxisLineAlpha(float uvCoord, float lineCount, float lineThickness, float edgeMultiplier)
			{
				float segmentCount = max(1.0, lineCount - 1.0);
				float lineCoord = uvCoord * segmentCount;
				float nearestLine = round(lineCoord);
				float edgeDistance = min(nearestLine, segmentCount - nearestLine);
				float edgeMask = step(edgeDistance, 0.5);
				float thickness = lineThickness * lerp(1.0, edgeMultiplier, edgeMask) / segmentCount;
				float distanceToLine = abs(lineCoord - nearestLine) / segmentCount;
				float aa = max(fwidth(distanceToLine), 1e-6);
				return 1.0 - smoothstep(thickness, thickness + aa, distanceToLine);
			}

			float EvalStar(float2 uv, float2 starUv, float radius, float softness)
			{
				float dist = distance(uv, starUv);
				return 1.0 - smoothstep(radius, radius + softness, dist);
			}

			float GetStarAlpha(float2 uv, float lineCount, float edgeOffset, float radiusRatio, float softnessRatio)
			{
				float segmentCount = max(1.0, lineCount - 1.0);
				float edge = clamp(edgeOffset, 0.0, segmentCount);
				float farEdge = segmentCount - edge;
				float center = segmentCount * 0.5;
				float hasCenter = step(abs(frac(center)), 1e-5) * step(9.0, lineCount);

				float radius = radiusRatio / segmentCount;
				float softness = max(softnessRatio / segmentCount, 1e-6);
				float alpha = 0.0;

				alpha = max(alpha, EvalStar(uv, float2(edge / segmentCount, edge / segmentCount), radius, softness));
				alpha = max(alpha, EvalStar(uv, float2(edge / segmentCount, farEdge / segmentCount), radius, softness));
				alpha = max(alpha, EvalStar(uv, float2(farEdge / segmentCount, edge / segmentCount), radius, softness));
				alpha = max(alpha, EvalStar(uv, float2(farEdge / segmentCount, farEdge / segmentCount), radius, softness));

				if(hasCenter > 0.5)
				{
					float centerUv = center / segmentCount;
					float edgeUv = edge / segmentCount;
					float farUv = farEdge / segmentCount;
					alpha = max(alpha, EvalStar(uv, float2(centerUv, centerUv), radius, softness));
					alpha = max(alpha, EvalStar(uv, float2(edgeUv, centerUv), radius, softness));
					alpha = max(alpha, EvalStar(uv, float2(farUv, centerUv), radius, softness));
					alpha = max(alpha, EvalStar(uv, float2(centerUv, edgeUv), radius, softness));
					alpha = max(alpha, EvalStar(uv, float2(centerUv, farUv), radius, softness));
				}

				return alpha;
			}

			fixed4 frag(v2f input) : SV_Target
			{
				float boardSize = max(2.0, floor(_BoardSize + 0.5));
				float lineX = GetAxisLineAlpha(input.uv.x, boardSize, _LineThickness, _EdgeLineMultiplier);
				float lineY = GetAxisLineAlpha(input.uv.y, boardSize, _LineThickness, _EdgeLineMultiplier);
				float gridAlpha = max(lineX, lineY);
				float starAlpha = GetStarAlpha(input.uv, boardSize, _StarEdgeOffset, _StarPointRadius, _StarPointSoftness);
				float alpha = max(gridAlpha, starAlpha) * _GridColor.a;

				clip(alpha - _AlphaCutout);
				return float4(_GridColor.rgb, alpha);
			}
			ENDCG
		}
	}
}
