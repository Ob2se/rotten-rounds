
HEADER
{
	Description = "";
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	Forward();
	Depth();
	ToolsShadingComplexity( "tools_shading_complexity.shader" );
}

COMMON
{
	#ifndef S_ALPHA_TEST
	#define S_ALPHA_TEST 0
	#endif
	#ifndef S_TRANSLUCENT
	#define S_TRANSLUCENT 1
	#endif
	
	#include "common/shared.hlsl"
	#include "procedural.hlsl"

	#define S_UV2 1
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
	float4 vColor : COLOR0 < Semantic( Color ); >;
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
	float3 vPositionOs : TEXCOORD14;
	float3 vNormalOs : TEXCOORD15;
	float4 vTangentUOs_flTangentVSign : TANGENT	< Semantic( TangentU_SignV ); >;
	float4 vColor : COLOR0;
	float4 vTintColor : COLOR1;
	#if ( PROGRAM == VFX_PROGRAM_PS )
		bool vFrontFacing : SV_IsFrontFace;
	#endif
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput v )
	{
		
		PixelInput i = ProcessVertex( v );
		i.vPositionOs = v.vPositionOs.xyz;
		i.vColor = v.vColor;
		
		ExtraShaderData_t extraShaderData = GetExtraPerInstanceShaderData( v.nInstanceTransformID );
		i.vTintColor = extraShaderData.vTint;
		
		VS_DecodeObjectSpaceNormalAndTangent( v, i.vNormalOs, i.vTangentUOs_flTangentVSign );
		return FinalizeVertex( i );
		
	}
}

PS
{
	#include "common/pixel.hlsl"
	RenderState( CullMode, F_RENDER_BACKFACES ? NONE : DEFAULT );
		
	BoolAttribute( bWantsFBCopyTexture, true );
	Texture2D g_tFrameBufferCopyTexture < Attribute( "FrameBufferCopyTexture"); SrgbRead( false ); >;
	SamplerState g_sSampler0 < Filter( ANISO ); AddressU( WRAP ); AddressV( WRAP ); >;
	CreateInputTexture2D( raindripscomb, Linear, 8, "None", "_color", ",0/,0/0", DefaultFile( "shaders/rain_drips.jpg" ) );
	CreateInputTexture2D( raindripmask, Srgb, 8, "None", "_color", ",0/,0/0", DefaultFile( "shaders/rain_drip_mask.jpg" ) );
	CreateInputTexture2D( Texture_ps_2, Srgb, 8, "None", "_color", ",0/,0/0", DefaultFile( "shaders/rain_drops.jpg" ) );
	Texture2D g_traindripscomb < Channel( RGBA, Box( raindripscomb ), Linear ); OutputFormat( RGBA8888 ); SrgbRead( False ); >;
	Texture2D g_traindripmask < Channel( RGBA, Box( raindripmask ), Srgb ); OutputFormat( DXT5 ); SrgbRead( True ); >;
	Texture2D g_tTexture_ps_2 < Channel( RGBA, Box( Texture_ps_2 ), Srgb ); OutputFormat( DXT5 ); SrgbRead( True ); >;
		
	float2 MapSceneColorCoords( float2 vInput, float2 modes )
	{
		float2 result;
	
		// X
		if ( modes.x == 1 ) // Mirror
		{
			float xx = abs( vInput.x );
			result.x = (fmod( floor( xx ), 2.0 ) == 0.0) ? frac( xx ) : 1.0 - frac( xx );
		}
		else if ( modes.x == 2 ) // Clamp
		{
			result.x = clamp( vInput.x, 0.0, 1.0 );
		}
		else if ( modes.x == 3 ) // Border
		{
			result.x = (vInput.x < 0.0 || vInput.x > 1.0) ? 0.5 : vInput.x;
		}
		else if ( modes.x == 4 ) // MirrorOnce
		{
	        float xx = abs( vInput.x );
			float floorX = floor( xx );
			if ( floorX < 1.0 )
			{
				result.x = frac( xx );
			}
			else if ( floorX < 2.0 )
			{
				result.x = 1.0 - frac( xx );
			}
			else
			{
				result.x = vInput.x;
			}
		}
		else // Wrap by default
		{
			result.x = vInput.x;
		}
	
		// Y
		if ( modes.y == 1 ) // Mirror
		{
			float yy = abs( vInput.y );
			result.y = (fmod( floor( yy ), 2.0 ) == 0.0) ? frac( yy ) : 1.0 - frac( yy );
		}
		else if ( modes.y == 2 ) // Clamp
		{
			result.y = clamp( vInput.y, 0.0, 1.0 );
		}
		else if ( modes.y == 3 ) // Border
		{
			result.y = (vInput.y < 0.0 || vInput.y > 1.0) ? 0.5 : vInput.y;
		}
		else if ( modes.y == 4 ) // MirrorOnce
		{
			float yy = abs( vInput.y );
			float floorY = floor( yy );
			if ( floorY < 1.0 )
			{
				result.y = frac( yy );
			}
			else if ( floorY < 2.0 )
			{
				result.y = 1.0 - frac( yy );
			}
			else
			{
				result.y = vInput.y;
			}
		}
		else // Wrap by default
		{
			result.y = vInput.y;
		}
	
		return result;
	}
	
	float4 MainPs( PixelInput i ) : SV_Target0
	{

		
		float2 l_0 = i.vTextureCoords.xy * float2( 3, 3 );
		float4 l_1 = Tex2DS( g_traindripscomb, g_sSampler0, l_0 );
		float l_2 = l_1.x;
		float l_3 = l_1.y;
		float2 l_4 = float2( l_2, l_3);
		float2 l_5 = l_4 * float2( 6, 6 );
		float2 l_6 = l_5 - float2( 3, 3 );
		float l_7 = round( l_1.b );
		float l_8 = l_0.x;
		float l_9 = l_8 * 1;
		float l_10 = l_0.y;
		float l_11 = l_10 * -0.5;
		float l_12 = 0.0f;
		float l_13 = 0.0f;
		float4 l_14 = float4( l_9, l_11, l_12, l_13 );
		float l_15 = lerp( 0.15, -0.05, l_1.a );
		float l_16 = l_1.a + g_flTime;
		float l_17 = l_15 * l_16;
		float4 l_18 = l_14 + float4( l_17, l_17, l_17, l_17 );
		float4 l_19 = Tex2DS( g_traindripmask, g_sSampler0, l_18.xy );
		float l_20 = l_7 * l_19.r;
		float2 l_21 = l_6 * float2( l_20, l_20 );
		float3 l_22 = g_tFrameBufferCopyTexture.Sample( g_sAniso, MapSceneColorCoords( l_21, float2(0,0) )).rgb;
		float2 l_23 = i.vTextureCoords.xy * float2( 4, 4 );
		float l_24 = l_23.x;
		float l_25 = l_23.y;
		float2 l_26 = float2( l_24, l_25);
		float4 l_27 = Tex2DS( g_tTexture_ps_2, g_sSampler0, l_26 );
		float3 l_28 = float3( l_27.r, l_27.g, l_27.a );
		float3 l_29 = l_28 * float3( 2, 2, 2 );
		float3 l_30 = l_29 - float3( 1, 1, 1 );
		float l_31 = l_30.x;
		float l_32 = l_30.y;
		float2 l_33 = float2( l_31, l_32);
		float l_34 = l_30.z;
		float l_35 = saturate( l_34 );
		float l_36 = g_flTime * 2;
		float l_37 = l_27.b - l_36;
		float l_38 = l_37 % 1;
		float l_39 = frac( l_38 );
		float l_40 = l_35 * l_39;
		float2 l_41 = l_33 * float2( l_40, l_40 );
		float3 l_42 = g_tFrameBufferCopyTexture.Sample( g_sAniso, MapSceneColorCoords( l_41, float2(0,0) )).rgb;
		float3 l_43 = l_22 + l_42;
		float l_44 = l_20 + l_40;
		

		return float4( l_43, l_44 );
	}
}
