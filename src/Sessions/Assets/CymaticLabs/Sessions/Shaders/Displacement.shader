Shader "Cymatic Labs/Displacement" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
		_PointSize("Point Size", Float) = 4.0
		_Crossfader("Crossfader", Range(0, 1)) = 0.0
		_EffectIdA("Efx A ID", Int) = 0
		_EffectIdB("Efx B ID", Int) = 0
		_TimeA("Efx A Time", Float) = 1
		_ControlA1("Efx A Control '1'", Float) = 0
		_ControlA2("Efx A Control '2'", Float) = 0
		_ControlA3("Efx A Control '3'", Float) = 0
		_TimeB("Efx B Time", Float) = 1
		_ControlB1("Efx B Control '1'", Float) = 0
		_ControlB2("Efx B Control '2'", Float) = 0
		_ControlB3("Efx B Control '3'", Float) = 0
	}

	CGINCLUDE
	// EFFECTS
	#include "UnityCG.cginc"

	// Define shader effect IDs
	static const int EFX_NONE = 0;
	static const int EFX_CIRCULAR_DISTORTION = 1;
	static const int EFX_DISTORTION_X = 2;
	static const int EFX_DISTORTION_Y = 3;
	static const int EFX_DISTORTION_Z = 4;

	// Applies a vertex effect to a given vertex
	float4 applyVertexEffect(int effectId, float4 v, float time, float mix,
		float ctrl1, float ctrl2, float ctrl3)
	{
		float4 pos = float4(0, 0, 0, 0);

		// No effect, return immediately
		if (effectId == EFX_NONE) return pos;

		// CircularDistortion
		if (effectId == EFX_CIRCULAR_DISTORTION)
		{
			if (ctrl3 <= 1) ctrl3 = 1; // prevent divide by zero
			pos.x += (sin(time * 0.5 + v.z * (ctrl2 * 10)) * ctrl1 / (ctrl3 * 10)) * mix;
			pos.y += (cos(time * 0.5 + v.z * (ctrl2 * 10)) * ctrl1 / (ctrl3 * 10)) * mix;
		}
		// DistortionX
		else if (effectId == EFX_DISTORTION_X)
		{
			if (ctrl3 <= 1) ctrl3 = 1; // prevent divide by zero
			pos.x += (sin(time * 0.5 + v.y * (ctrl2 * 10)) * ctrl1 / (ctrl3 * 10)) * mix;
		}
		// DistortionY
		else if (effectId == EFX_DISTORTION_Y)
		{
			if (ctrl3 <= 1) ctrl3 = 1; // prevent divide by zero
			pos.y += (sin(time * 0.5 + v.x * (ctrl2 * 10)) * ctrl1 / (ctrl3 * 10)) * mix;
		}
		// DistortionZ
		else if (effectId == EFX_DISTORTION_Z)
		{
			if (ctrl3 <= 1) ctrl3 = 1; // prevent divide by zero
			pos.z += (sin(time * 0.5 + v.y * (ctrl2 * 10)) * ctrl1 / (ctrl3 * 10)) * mix;
		}

		// Unsupported, just 
		return pos;
	}

	ENDCG

	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200

		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows vertex:vert

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		sampler2D _MainTex;

		struct Input {
			float2 uv_MainTex;
		};

		/*struct appdata
		{
			float4 vertex : POSITION;
			float2 uv : TEXCOORD0;
		};*/

		struct v2f
		{
			float2 uv : TEXCOORD0;
			UNITY_FOG_COORDS(1)
			float4 vertex : SV_POSITION;
		};

		half _Glossiness;
		half _Metallic;
		fixed4 _Color;

		/*============================
		* Fields/Properties
		=============================*/
		float _PointSize;		// size of quads to render at each vertex
		float _Crossfader;		// crossfader 0.0 - 1.0 indicating % of A/B mixing
		int   _EffectIdA;		// ID of the special shader effect to enable (0 = none)
		int   _EffectIdB;		// ID of the special shader effect to enable (0 = none)
		float _ControlA1;		// effect A control paramter '1'
		float _ControlA2;		// effect A control paramter '2'
		float _ControlA3;		// effect A control paramter '3'
		float _ControlB1;		// effect B control paramter '1'
		float _ControlB2;		// effect B control paramter '2'
		float _ControlB3;		// effect B control paramter '3'
		float _TimeA = 1;
		float _TimeB = 1;

		void vert(inout appdata_full o)
		{
			float4 v = o.vertex;

			// Mix time
			float tA, tB, t;
			tA = (1 - _Crossfader) * _TimeA;
			tB = _Crossfader * _TimeB;
			t = tA + tB;

			// Prepare the effect control values
			float ctrl1, ctrl2, ctrl3, ctrlA1, ctrlA2, ctrlA3, ctrlB1, ctrlB2, ctrlB3;
			
			ctrlA1 = (1 - _Crossfader) * _ControlA1;
			ctrlB1 = _Crossfader * _ControlB1;
			ctrl1 = ctrlA1 + ctrlB1;

			ctrlA2 = (1 - _Crossfader) * _ControlA2;
			ctrlB2 = _Crossfader * _ControlB2;
			ctrl2 = ctrlA2 + ctrlB2;

			ctrlA3 = (1 - _Crossfader) * _ControlA3;
			ctrlB3 = _Crossfader * _ControlB3;
			ctrl3 = ctrlA3 + ctrlB3;

			// Apply vertex effect
			// If the crossfader position full A, just apply A
			if (_Crossfader == 0)
			{
				v += applyVertexEffect(_EffectIdA, v, _Time.y * t, 1, ctrl1, ctrl2, ctrl3);
			}
			// If the crossfader position full B, just apply B
			else if (_Crossfader == 1)
			{
				v += applyVertexEffect(_EffectIdB, v, _Time.y * t, 1, ctrl1, ctrl2, ctrl3);
			}
			// Otherwise produce a mix of each effect
			else
			{
				v += applyVertexEffect(_EffectIdA, v, _Time.y * t, 1 - _Crossfader, ctrl1, ctrl2, ctrl3);
				v += applyVertexEffect(_EffectIdB, v, _Time.y * t, _Crossfader, ctrl1, ctrl2, ctrl3);
			}

			o.vertex = v;
		}

		// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
		// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
		// #pragma instancing_options assumeuniformscaling
		UNITY_INSTANCING_BUFFER_START(Props)
			// put more per-instance properties here
		UNITY_INSTANCING_BUFFER_END(Props)

		void surf (Input IN, inout SurfaceOutputStandard o) {
			// Albedo comes from a texture tinted by color
			fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
			o.Albedo = c.rgb;
			// Metallic and smoothness come from slider variables
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
			o.Alpha = c.a;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
