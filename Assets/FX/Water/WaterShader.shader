// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Unlit/WaterShader"
{
	Properties
	{
		_NormalTex("Normal Map", 2D) = "bump" {}				// Normal map
		_DudvTex("DUDV Map", 2D) = "white" {}					// DUDV map (partial derivative of normal map)
		_ReflectionTex("Reflection Map", 2D) = "white" {}		// Unused
		_Tiling("Tiling", Range(1,100)) = 1						// Tiling
		_CausticsTex("Caustics Atlas", 2D) = "white" {}			// Caustics texture atlas
	}
	SubShader
	{
		Tags { "RenderType" = "Transparent" "LightMode" = "ForwardBase" }
		LOD 100

		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				UNITY_FOG_COORDS(1)
				float4 vertex : SV_POSITION;
				float4 projCoord : TEXCOORD2;
				float3 tangent : TEXCOORD3;
				float3 bitangent : TEXCOORD4;
				float3 normal : TEXCOORD5;
				float3 vp : TEXCOORD6;
				float3 ld : TEXCOORD7;
				float4 worldPos : TEXCOORD8;
				float4 tmp : TEXCOORD9;
				float4 proj0 : TEXCOORD10;
			};

			sampler2D _NormalTex;
			sampler2D _DudvTex;
			sampler2D _ReflectionTex;
			sampler2D _RefractionTex;
			sampler2D _HeightMap;
			sampler2D _ScatterTex;
			sampler2D _CausticsTex;
			float4 _NormalTex_ST;
			float _Tiling;
			float _Phase;
			float4 _SunDirection;
			float4x4 _SunViewMatrix;
			float4 _TerrainSize;
			float4 _TerrainPosition;
			float _TerrainFactor;

			// Scattering parameters
			static float BlueWavelenght = 440.0f;
			static float BlueAbsorption = 0.005f;
			static float GreenWavelenght = 510.0f;
			static float GreenAbsorption = 0.03f;
			static float RedWavelenght = 650.0f;
			static float RedAbsorption = 0.35f;
			static float WaterDensity = 0.997f;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.worldPos = mul(UNITY_MATRIX_M, v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _NormalTex) * _Tiling;
				o.projCoord = UnityObjectToClipPos(v.vertex);

				o.proj0 = ComputeScreenPos(v.vertex);
				COMPUTE_EYEDEPTH(o.proj0.z);

				o.tmp = mul(_SunViewMatrix, mul(UNITY_MATRIX_M, v.vertex));

				UNITY_TRANSFER_FOG(o,o.vertex);

				float3 wNormal = UnityObjectToWorldNormal(v.normal);
				float3 wTangent = UnityObjectToWorldDir(v.tangent.xyz);
				float tangentSign = v.tangent.w * unity_WorldTransformParams.w;
				float3 wBitangent = cross(wNormal, wTangent) * tangentSign;
				o.tangent = mul(UNITY_MATRIX_MV, wTangent);
				o.bitangent = mul(UNITY_MATRIX_MV, wBitangent);
				o.normal = mul(UNITY_MATRIX_MV, wNormal);
				o.vp = mul(UNITY_MATRIX_MV, v.vertex).xyz;
				o.ld = mul(UNITY_MATRIX_MV, _SunDirection).xyz;

				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				// Projection coordinates (for reflection, refraction)
				float2 reflCoord = (i.projCoord.xy / i.projCoord.w) * 0.5f + 0.5f;
				reflCoord.y = 1.0f - reflCoord.y;

				// DuDv - offset map, use for simulating water ripples
				float3 dudv = tex2D(_DudvTex, i.uv * 100.0f + float2(_Phase, _Phase)).xyz;

				// Normal map for lighting
				float3 nm = normalize(UnpackNormal(tex2D(_NormalTex, i.uv * 100.0f + float2(_Phase, _Phase) + dudv.xy * 0.04f)));

				// Reflection coordinates
				float3 refl = reflect(normalize(-i.projCoord.xyz), nm) - reflect(normalize(-i.projCoord.xyz), float3(0.0f, 1.0f, 0.0f));

				float2 offset = nm.xy;

				// Clamp reflection coordinates
				float2 reflCoordClamp = reflCoord - offset * 0.1f;
				reflCoordClamp.x = clamp(reflCoordClamp.x, 0.0f, 1.0f);
				reflCoordClamp.y = clamp(reflCoordClamp.y, 0.0f, 1.0f);

				// Read reflection and refraction textures
				float4 reflection = tex2D(_ReflectionTex, reflCoordClamp);
				float4 scattering = tex2D(_ScatterTex, reflCoordClamp);

				// Don't use scattering at all (computed in scatter/refraction phase)
				float3 transmittance = float3(1.0f, 1.0f, 1.0f);
				transmittance.x = clamp(transmittance.x, 0.0f, 1.0f);
				transmittance.y = clamp(transmittance.y, 0.0f, 1.0f);
				transmittance.z = clamp(transmittance.z, 0.0f, 1.0f);

				// Caustics texture
				float caustics = scattering.x;

				// Height blending for water - transparency around shallow depth
				float3 terrainCoord = (i.worldPos.xyz - _TerrainPosition.xyz) / _TerrainSize.xyz;
				float heightFactor = abs(tex2D(_HeightMap, terrainCoord.xz).x - terrainCoord.y) * _TerrainSize.y * _TerrainFactor;
				if (terrainCoord.x < 0.0f || terrainCoord.x > 1.0f || terrainCoord.z < 0.0f || terrainCoord.z > 1.0f)
				{
					heightFactor = 1.0f;
				}
				float height = clamp(heightFactor, 0.0f, 1.0f);

				// Mix refraction - from refractino image, caustics and scattering, use height factor for blending
				float4 refraction = tex2D(_RefractionTex, reflCoordClamp) * float4(transmittance, 1.0f) + float4(caustics, caustics, caustics, 0.0f) * dot(float3(0.2126f, 0.7152f, 0.0722f), scattering.xyz) * 4.0f * clamp(heightFactor * 0.1f, 0.0f, 1.0f);

				// Calculate specular light and fresnel for mixing reflection and refraction
				float3x3 tangentFrame = float3x3(normalize(i.tangent), -normalize(i.bitangent), normalize(i.normal));

				float3 normal = mul(nm.xyz, tangentFrame);

				float kr = pow(1.0f - dot(normal, normalize(-i.vp)), 2.0f);
				float3 reflDir = normalize(reflect(normalize(i.vp), normal));
				float spec = 3.0f * pow(max(dot(normalize(-i.ld), reflDir), 0.0f), 32.0f);
								
				// Final color
				float3 surface = float3(1.0f, 1.0f, 1.0f) * spec + lerp(refraction, reflection, kr).xyz * (1.0f - spec);

				// Caustics projection coordinates
				/*float2 projCoord = (i.tmp.xy / i.tmp.w) * 0.5f + 0.5f;

				// Read caustics textures from atlas (blend based on time)
				int caust = (int)(16.0f * _Phase);
				int caustX = caust % 4;
				int caustY = 3 - caust / 4;
				float2 coord = float2(frac(projCoord.x * 2.0f), frac(projCoord.y * 2.0f)) / 4.0f + float2((float)caustX / 4.0f, (float)caustY / 4.0f) + float2(_Phase, _Phase) + dudv.xy * 0.4f;
				float foam = tex2D(_CausticsTex, coord).x * (1.0f - clamp(heightFactor * 2.0f, 0.0f, 1.0f)) * 10.0f;

				// Mix reflection, refraction
				float4 col = fixed4(lerp(surface, refraction.xyz, 1.0f - height), height);*/

				// Output surface
				return fixed4(surface, 1.0f);
			}
			ENDCG
		}
	}
}
