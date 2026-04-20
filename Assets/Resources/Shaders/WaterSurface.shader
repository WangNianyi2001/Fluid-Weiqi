Shader "FluidWeiqi/WaterSurface"
{
	Properties
	{
		_ShallowColor ("Shallow Color", Color) = (0.18, 0.58, 0.67, 0.55)
		_DeepColor ("Deep Color", Color) = (0.02, 0.12, 0.22, 0.75)
		_FresnelColor ("Fresnel Color", Color) = (0.7, 0.9, 1.0, 1)
		_FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3
		_DepthMix ("Depth Mix", Range(0, 1)) = 0.55

		_NormalA ("Normal A", 2D) = "bump" {}
		_NormalB ("Normal B", 2D) = "bump" {}
		_NormalStrength ("Normal Strength", Range(0, 2)) = 0.85
		_TilingA ("Normal A Tiling", Float) = 0.18
		_TilingB ("Normal B Tiling", Float) = 0.31
		_SpeedA ("Normal A Speed XY", Vector) = (0.03, 0.015, 0, 0)
		_SpeedB ("Normal B Speed XY", Vector) = (-0.02, 0.024, 0, 0)

		_SpecularStrength ("Specular Strength", Range(0, 2)) = 0.9
		_Gloss ("Gloss", Range(8, 128)) = 48
		_DepthFadeDistance ("Depth Fade Distance", Float) = 3.5
		_DepthOpacityBoost ("Depth Opacity Boost", Range(0, 1)) = 0.35
	}

	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
		LOD 200
		Cull Back
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _NormalA;
			sampler2D _NormalB;
			float4 _NormalA_ST;
			float4 _NormalB_ST;

			float4 _ShallowColor;
			float4 _DeepColor;
			float4 _FresnelColor;
			float _FresnelPower;
			float _DepthMix;
			float _NormalStrength;
			float _TilingA;
			float _TilingB;
			float4 _SpeedA;
			float4 _SpeedB;
			float _SpecularStrength;
			float _Gloss;
			float _DepthFadeDistance;
			float _DepthOpacityBoost;

			sampler2D _CameraDepthTexture;

			struct appdata
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 worldPos : TEXCOORD1;
				float3 worldNormal : TEXCOORD2;
				float3 worldTangent : TEXCOORD3;
				float3 worldBitangent : TEXCOORD4;
				float4 screenPos : TEXCOORD5;
				float eyeDepth : TEXCOORD6;
			};

			v2f vert(appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				o.worldNormal = UnityObjectToWorldNormal(v.normal);
				o.worldTangent = normalize(mul((float3x3)unity_ObjectToWorld, v.tangent.xyz));
				o.worldBitangent = cross(o.worldNormal, o.worldTangent) * v.tangent.w;
				o.screenPos = ComputeScreenPos(o.pos);
				COMPUTE_EYEDEPTH(o.eyeDepth);
				return o;
			}

			float3 SampleWaterNormal(v2f i)
			{
				float2 uvA = i.uv * _TilingA + _Time.y * _SpeedA.xy;
				float2 uvB = i.uv * _TilingB + _Time.y * _SpeedB.xy;

				float3 nA = UnpackNormal(tex2D(_NormalA, uvA));
				float3 nB = UnpackNormal(tex2D(_NormalB, uvB));
				float3 nTS = normalize(float3((nA.xy + nB.xy) * _NormalStrength, nA.z * nB.z));

				float3x3 tbn = float3x3(normalize(i.worldTangent), normalize(i.worldBitangent), normalize(i.worldNormal));
				return normalize(mul(nTS, tbn));
			}

			fixed4 frag(v2f i) : SV_Target
			{
				float3 n = SampleWaterNormal(i);
				float3 vDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
				float3 lDir = normalize(_WorldSpaceLightPos0.xyz);
				float3 hDir = normalize(vDir + lDir);

				float ndv = saturate(dot(n, vDir));
				float fresnel = pow(1.0 - ndv, _FresnelPower);

				float depthBlend = saturate(_DepthMix + n.y * 0.25);
				float3 baseCol = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthBlend);

				float ndl = saturate(dot(n, lDir));
				float spec = pow(saturate(dot(n, hDir)), _Gloss) * _SpecularStrength;

				float3 litCol = baseCol * (0.35 + 0.65 * ndl);
				litCol += spec;
				litCol = lerp(litCol, _FresnelColor.rgb, fresnel * 0.65);

				float alpha = saturate(lerp(_ShallowColor.a, _DeepColor.a, depthBlend) + fresnel * 0.2);

				float sceneRawDepth = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos));
				float sceneEyeDepth = LinearEyeDepth(sceneRawDepth);
				float waterEyeDepth = i.eyeDepth;
				float submergedDepth = max(0, sceneEyeDepth - waterEyeDepth);
				float depthFade = saturate(submergedDepth / max(_DepthFadeDistance, 1e-4));
				alpha = saturate(alpha + depthFade * _DepthOpacityBoost);

				return float4(litCol, alpha);
			}
			ENDCG
		}
	}
}
