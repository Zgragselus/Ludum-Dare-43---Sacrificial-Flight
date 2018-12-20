Shader "Unlit/ScatteringShader"
{
	Properties
	{
		_MainTex("Base (RGB)", 2D) = "white" {}
		_Color("Minimap Color", Color) = (1.0, 1.0, 1.0, 1.0)
	}

	SubShader
	{
		Tags
		{
			"Queue" = "Geometry"
			"RenderType" = "Opaque"
			//"RenderedByMinimap" = "True"
			//"IgnoreProjector" = "True"
			"ForceNoShadowCasting" = "True"
		}

		Lighting Off
		Blend Off
		Cull Back
		ZWrite On

		CGPROGRAM
#pragma surface SurfaceMain Lambert noforwardadd nofog

		sampler2D _MainTex;
		sampler2D _ScatterCausticsTex;
		float4 _ScatterPlane;
		fixed4 _Color;
		float _Phase;

		static float BlueWavelenght = 440.0f;
		static float BlueAbsorption = 0.005f;
		static float GreenWavelenght = 510.0f;
		static float GreenAbsorption = 0.03f;
		static float RedWavelenght = 650.0f;
		static float RedAbsorption = 0.35f;
		static float WaterDensity = 0.997f;

		// t - Distance travelled through media
		// d - Density of the media
		inline float Transmittance(in float t, in float d)
		{
			return exp(-t * d);
		}

		inline float Caustics(float phase, float2 projCoord)
		{
			int caust = (int)(phase) % 16;
			int caustX = caust % 4;
			int caustY = 3 - caust / 4;
			float2 coord = float2(frac(projCoord.x), frac(projCoord.y)) / 4.0f + float2((float)caustX / 4.0f, (float)caustY / 4.0f);
			return tex2D(_ScatterCausticsTex, coord).x;
		}

		struct Input
		{
			float3 worldPos;
			fixed2 uv_MainTex;
		};

		void SurfaceMain(Input input, inout SurfaceOutput output)
		{
			fixed4 mainColor = tex2D(_MainTex, input.uv_MainTex);
			
			float3 cameraPos = _WorldSpaceCameraPos.xyz;
			float3 worldPos = input.worldPos.xyz;
			float3 viewVec = worldPos - cameraPos;
			float d2 = length(viewVec);
			float d1 = 1000000.0f;

			float3 viewDir = normalize(viewVec);
			float denom = dot(-_ScatterPlane.xyz, viewDir);
			if (denom > 0.001f)
			{
				float3 p0l0 = -_ScatterPlane.xyz * _ScatterPlane.w - cameraPos;
				d1 = dot(p0l0, -_ScatterPlane.xyz) / denom;
			}

			float length = 0.0f;
			if (d2 > d1 && d1 > 0.0f)
			{
				length = d2 - d1;
			}

			float transmittance = Transmittance(length, WaterDensity);
			float3 scattering = transmittance / float3(RedAbsorption, GreenAbsorption, BlueAbsorption);
			scattering.x = clamp(scattering.x, 0.0f, 1.0f);
			scattering.y = clamp(scattering.y, 0.0f, 1.0f);
			scattering.z = clamp(scattering.z, 0.0f, 1.0f);

			/*int caust = (int)(16.0f * _Phase);
			int caustX = caust % 4;
			int caustY = 3 - caust / 4;
			float2 coord = float2(frac(worldPos.x * 0.1f), frac(worldPos.z * 0.1f)) / 4.0f + float2((float)caustX / 4.0f, (float)caustY / 4.0f);
			float caustics = tex2D(_ScatterCausticsTex, coord).x;*/

			float2 caustics = float2(Caustics(16.0f * _Phase, worldPos.xz * 0.4f), Caustics(16.0f * _Phase + 1.0f, worldPos.xz * 0.4f));
			float caustLerp = lerp(caustics.x, caustics.y, frac(_Phase * 16.0f));

			output.Albedo = fixed4(0.0f, 0.0f, 0.0f, 0.0f);
			//output.Emission = fixed3(input.worldPos.xyz);
			//output.Emission = float3(scattering.xyz);
			output.Emission = float3(caustLerp, transmittance, 0.0f);
			output.Alpha = 1.0f;
		}
		ENDCG
	}
}
