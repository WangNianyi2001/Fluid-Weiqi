Shader "FluidWeiqi/BoardGrid"
{
	Properties
	{
		_GridColor ("Grid Color", Color) = (0, 0, 0, 1)
		_BoardSize ("Board Size", Float) = 19
		_BorderThickness ("Border Thickness", Range(0.002, 0.2)) = 0.045
		_StarPointRadius ("Star Point Radius", Range(0.02, 0.5)) = 0.13
		_StarPointSoftness ("Star Point Softness", Range(0.002, 0.2)) = 0.03
		_AlphaCutout ("Alpha Cutout", Range(0, 1)) = 0.01
		_StarEdgeOffset ("Star Edge Offset", Float) = 3
		_Topology ("Topology", Float) = 0
		// 0 = StandardFlat, 1 = IrregularFlat (tengen only), 2 = Sphere (great circles)
		_GridDisplayMode ("Grid Display Mode", Float) = 0
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
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 localPos : TEXCOORD1;
			};

			float4 _GridColor;
			float _BoardSize;
			float _BorderThickness;
			float _StarPointRadius;
			float _StarPointSoftness;
			float _AlphaCutout;
			float _StarEdgeOffset;
			float _Topology;
			float _GridDisplayMode;

			v2f vert(appdata vertexInput)
			{
				v2f output;
				output.vertex = TransformObjectToHClip(vertexInput.vertex.xyz);
				output.uv = vertexInput.uv;
				output.localPos = vertexInput.vertex.xyz;
				return output;
			}

			// ---- Flat: border edges only (no interior grid lines) ----
			float GetBorderAlpha(float uv, float boardSize, float thickness)
			{
				float segmentCount = max(1.0, boardSize - 1.0);
				float thick = thickness / segmentCount;
				float d = min(uv, 1.0 - uv);
				float aa = max(fwidth(d), 1e-6);
				return 1.0 - smoothstep(thick, thick + aa, d);
			}

			// ---- Sphere: 3 axis-aligned great circles in local object space ----
			// Threshold derived so visual width matches flat border thickness:
			//   equatorial cell spacing = PI / boardSize radians
			//   => halfThreshold = 0.5 * thickness * PI / boardSize
			float GetGreatCircleAlpha(float3 localPos, float boardSize, float thickness)
			{
				float3 dir = normalize(localPos);
				float halfThreshold = 0.5 * thickness * PI / max(boardSize, 1.0);
				// distance to each of the 3 coordinate planes on the unit sphere
				float d = min(min(abs(dir.x), abs(dir.y)), abs(dir.z));
				float aa = max(fwidth(d), 1e-6);
				return 1.0 - smoothstep(halfThreshold, halfThreshold + aa, d);
			}

			float EvalStar(float2 uv, float2 starUv, float radius, float softness)
			{
				float dist = distance(uv, starUv);
				return 1.0 - smoothstep(radius, radius + softness, dist);
			}

			// ---- Star points (flat boards only) ----
			float GetTengenAlpha(float2 uv, float boardSize, float radius, float softness)
			{
				float segmentCount = max(1.0, boardSize - 1.0);
				float center = segmentCount * 0.5;
				float hasCenter = step(abs(frac(center)), 1e-5);
				float centerUv = center / segmentCount;
				float r = radius / segmentCount;
				float s = max(softness / segmentCount, 1e-6);
				return hasCenter * (1.0 - smoothstep(r, r + s, distance(uv, float2(centerUv, centerUv))));
			}

			float GetStarAlpha(float2 uv, float lineCount, float edgeOffset, float radiusRatio, float softnessRatio)
			{
				float segmentCount = max(1.0, lineCount - 1.0);
				float edge = clamp(edgeOffset, 0.0, segmentCount);
				float farEdge = segmentCount - edge;
				float center = segmentCount * 0.5;
				float hasCenter = step(abs(frac(center)), 1e-5);
				float onlyTengen = step(lineCount, 7.0);
				float hideEdgeSideStars = step(lineCount, 13.0);

				float radius = radiusRatio / segmentCount;
				float softness = max(softnessRatio / segmentCount, 1e-6);
				float alpha = 0.0;

				if(onlyTengen < 0.5)
				{
					alpha = max(alpha, EvalStar(uv, float2(edge / segmentCount, edge / segmentCount), radius, softness));
					alpha = max(alpha, EvalStar(uv, float2(edge / segmentCount, farEdge / segmentCount), radius, softness));
					alpha = max(alpha, EvalStar(uv, float2(farEdge / segmentCount, edge / segmentCount), radius, softness));
					alpha = max(alpha, EvalStar(uv, float2(farEdge / segmentCount, farEdge / segmentCount), radius, softness));
				}

				if(hasCenter > 0.5)
				{
					float centerUv = center / segmentCount;
					alpha = max(alpha, EvalStar(uv, float2(centerUv, centerUv), radius, softness));

					if(hideEdgeSideStars < 0.5)
					{
						float edgeUv = edge / segmentCount;
						float farUv = farEdge / segmentCount;
						alpha = max(alpha, EvalStar(uv, float2(edgeUv, centerUv), radius, softness));
						alpha = max(alpha, EvalStar(uv, float2(farUv, centerUv), radius, softness));
						alpha = max(alpha, EvalStar(uv, float2(centerUv, edgeUv), radius, softness));
						alpha = max(alpha, EvalStar(uv, float2(centerUv, farUv), radius, softness));
					}
				}

				return alpha;
			}

			float4 frag(v2f input) : SV_Target
			{
				float boardSize = max(2.0, floor(_BoardSize + 0.5));
				float alpha = 0.0;

				if(_GridDisplayMode >= 1.5) // Sphere: great circles from local coords
				{
					alpha = GetGreatCircleAlpha(input.localPos, boardSize, _BorderThickness);
				}
				else // Flat: border edges + optional star points
				{
					float2 uv = input.uv;
					float borderX = GetBorderAlpha(uv.x, boardSize, _BorderThickness);
					float borderY = GetBorderAlpha(uv.y, boardSize, _BorderThickness);
					alpha = max(borderX, borderY);

					float starAlpha;
					if(_GridDisplayMode >= 0.5) // IrregularFlat: tengen only
						starAlpha = GetTengenAlpha(uv, boardSize, _StarPointRadius, _StarPointSoftness);
					else // StandardFlat: full star pattern
						starAlpha = GetStarAlpha(uv, boardSize, _StarEdgeOffset, _StarPointRadius, _StarPointSoftness);
					alpha = max(alpha, starAlpha);
				}

				alpha *= _GridColor.a;
				clip(alpha - _AlphaCutout);
				return float4(_GridColor.rgb, alpha);
			}
			ENDHLSL
		}
	}
}
