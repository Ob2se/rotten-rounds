//=========================================================================================================================
//
// Glass Abyss with Lightning Shader
//
// A shader that displays an abyss-like background with striking lightning,
// overlaid with a moving, glass-like cloud layer.
//
//=========================================================================================================================

Shader "Custom.GlassAbyssLightning"
{
	Properties
	{
		// Glass/Cloud Layer
		g_tCloudMask( "Cloud Mask", "materials/effects/cloud_mask.png" ) = "white";
		g_vCloudSpeed( "Cloud Scroll Speed", Vector ) = "(0.05, 0.02, 0, 0)";
		g_flRefractionStrength( "Refraction Strength", Float, 0.0, 1.0 ) = 0.03;
		g_flGlassOpacity( "Glass Opacity", Float, 0.0, 1.0 ) = 0.2;
		g_vGlassColor( "Glass Color", Color ) = "(0.8, 0.9, 1.0, 1.0)";
		g_flSpecularIntensity( "Glass Specular", Float, 0.0, 10.0 ) = 2.0;

		// Lightning Layer
		g_tLightningAtlas( "Lightning Atlas", "materials/effects/lightning_atlas.png" ) = "white";
		g_vLightningColor( "Lightning Color", Color ) = "(0.7, 0.8, 1.0, 1.0)";
		g_flLightningIntensity( "Lightning Intensity", Float, 0.0, 20.0 ) = 10.0;
		g_flLightningSpeed( "Lightning Speed", Float, 0.0, 50.0 ) = 24.0; // Frames per second
		g_vLightningAtlasGrid( "Lightning Atlas Grid", Vector ) = "(4, 4, 0, 0)"; // 4x4 grid
	}

	Pass
	{
		//=================================================================================================================
		// Configuration
		//=================================================================================================================
		ZWrite = false;
		Blend = "one one_minus_src_alpha"; // Standard alpha blending

		//=================================================================================================================
		// Vertex Shader
		//=================================================================================================================
		VS
		{
			#include "common/shared.hlsl"

			// Input
			struct VertexInput
			{
				float3 v_vPosition	: POSITION;
				float2 v_vTexCoord	: TEXCOORD0;
			};

			// Output
			struct VertexOutput
			{
				float4 v_vPosition		: SV_POSITION;
				float2 v_vTexCoord		: TEXCOORD0;
				float4 v_vWorldPosition : TEXCOORD1;
			};

			// Main
			VertexOutput main( VertexInput i )
			{
				VertexOutput o = (VertexOutput)0;

				o.v_vPosition = mul( g_matModelViewProjection, float4( i.v_vPosition, 1.0f ) );
				o.v_vTexCoord = i.v_vTexCoord;
				o.v_vWorldPosition = mul( g_matModel, float4( i.v_vPosition, 1.0f ) );

				return o;
			}
		}

		//=================================================================================================================
		// Fragment Shader
		//=================================================================================================================
		PS
		{
			#include "common/shared.hlsl"

			// Properties
			SamplerState g_sAniso
			{
				Filter = ANISOTROPIC;
				AddressU = WRAP;
				AddressV = WRAP;
			};

			CreateTexture2D( g_tCloudMask );
			CreateTexture2D( g_tLightningAtlas );

			float4 g_vCloudSpeed;
			float g_flRefractionStrength;
			float g_flGlassOpacity;
			float4 g_vGlassColor;
			float g_flSpecularIntensity;

			float4 g_vLightningColor;
			float g_flLightningIntensity;
			float g_flLightningSpeed;
			float4 g_vLightningAtlasGrid;

			// Helper to get normal from a heightmap (our cloud mask)
			float3 GetNormalFromMap( Texture2D tex, float2 uv, float strength )
			{
				float2 texelSize = 1.0f / g_vScreenResolution.xy;
				float s1 = tex.Sample( g_sAniso, uv + float2( -texelSize.x, 0 ) ).r;
				float s2 = tex.Sample( g_sAniso, uv + float2(  texelSize.x, 0 ) ).r;
				float s3 = tex.Sample( g_sAniso, uv + float2( 0, -texelSize.y ) ).r;
				float s4 = tex.Sample( g_sAniso, uv + float2( 0,  texelSize.y ) ).r;
				
				float3 normal = float3( (s1-s2) * strength, (s3-s4) * strength, 1.0 );
				return normalize( normal );
			}

			// Main
			float4 main( PS_INPUT i ) : SV_Target
			{
				// Animate lightning sprite sheet
				float flNumFrames = g_vLightningAtlasGrid.x * g_vLightningAtlasGrid.y;
				float flFrameId = floor( fmod( g_flTime * g_flLightningSpeed, flNumFrames ) );
				float flFrameX = fmod( flFrameId, g_vLightningAtlasGrid.x );
				float flFrameY = floor( flFrameId / g_vLightningAtlasGrid.x );
				float2 vLightningUvBase = ( i.v_vTexCoord.xy + float2( flFrameX, flFrameY ) ) / g_vLightningAtlasGrid.xy;

				// Scrolling clouds
				float2 vCloudUv = i.v_vTexCoord.xy + g_flTime * g_vCloudSpeed.xy;
				float flCloudMask = g_tCloudMask.Sample( g_sAniso, vCloudUv ).r;
				float3 vCloudNormal = GetNormalFromMap( g_tCloudMask, vCloudUv, 10.0 );

				// Refract the lightning UVs based on the cloud normal
				float2 vRefractionOffset = vCloudNormal.xy * g_flRefractionStrength * flCloudMask;
				float flLightningMask = g_tLightningAtlas.Sample( g_sAniso, vLightningUvBase + vRefractionOffset ).r;
				float3 vLightning = g_vLightningColor.rgb * flLightningMask * g_flLightningIntensity;

				// Composite the final color
				float3 vFinalColor = lerp( vLightning, g_vGlassColor.rgb, flCloudMask * g_flGlassOpacity );

				// Add specular highlight for the "glass" feel
				float3 vViewDir = normalize( g_vCameraPosition.xyz - i.v_vWorldPosition.xyz );
				float flFresnel = 1.0 - saturate( dot( vViewDir, vCloudNormal ) );
				float flSpecular = pow( flFresnel, 5.0 ) * g_flSpecularIntensity * flCloudMask;
				
				vFinalColor += flSpecular;

				return float4( vFinalColor, 1.0 );
			}
		}
	}
}