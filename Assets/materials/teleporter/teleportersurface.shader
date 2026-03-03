
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
	#define S_TRANSLUCENT 0
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
		
	float4 g_vTeleporterBackgroundColor < Attribute( "TeleporterBackgroundColor" ); Default4( 0.03, 0.03, 0.03, 1.00 ); >;
	float4 g_vTeleporterLightRingColor < Attribute( "TeleporterLightRingColor" ); Default4( 0.02, 0.24, 0.53, 1.00 ); >;
	float g_flspeed < Attribute( "speed" ); Default1( 0.5007757 ); >;
	float g_flsoftness < Attribute( "softness" ); Default1( 0.69403386 ); >;
	float g_flringsize < Attribute( "ringsize" ); Default1( 0.55920106 ); >;
	
	float4 MainPs( PixelInput i ) : SV_Target0
	{
		
		Material m = Material::Init( i );
		m.Albedo = float3( 1, 1, 1 );
		m.Normal = float3( 0, 0, 1 );
		m.Roughness = 1;
		m.Metalness = 0;
		m.AmbientOcclusion = 1;
		m.TintMask = 1;
		m.Opacity = 1;
		m.Emission = float3( 0, 0, 0 );
		m.Transmission = 0;
		
		float4 l_0 = g_vTeleporterBackgroundColor;
		float4 l_1 = g_vTeleporterLightRingColor;
		float l_2 = g_flspeed;
		float l_3 = g_flTime * l_2;
		float l_4 = frac( l_3 );
		float l_5 = g_flsoftness;
		float l_6 = l_4 - l_5;
		float2 l_7 = i.vTextureCoords.xy * float2( 1, 1 );
		float2 l_8 = l_7 - float2( 0.5, 0.5 );
		float l_9 = length( l_8 );
		float l_10 = smoothstep( l_4, l_6, l_9 );
		float l_11 = frac( l_3 );
		float l_12 = g_flringsize;
		float l_13 = l_11 - l_12;
		float l_14 = l_13 + l_5;
		float l_15 = smoothstep( l_13, l_14, l_9 );
		float l_16 = l_10 * l_15;
		float4 l_17 = l_1 * float4( l_16, l_16, l_16, l_16 );
		
		m.Albedo = l_0.xyz;
		m.Emission = l_17.xyz;
		m.Opacity = 1;
		m.Roughness = 1;
		m.Metalness = 0;
		m.AmbientOcclusion = 1;
		
		
		m.AmbientOcclusion = saturate( m.AmbientOcclusion );
		m.Roughness = saturate( m.Roughness );
		m.Metalness = saturate( m.Metalness );
		m.Opacity = saturate( m.Opacity );
		
		// Result node takes normal as tangent space, convert it to world space now
		m.Normal = TransformNormal( m.Normal, i.vNormalWs, i.vTangentUWs, i.vTangentVWs );
		
		// for some toolvis shit
		m.WorldTangentU = i.vTangentUWs;
		m.WorldTangentV = i.vTangentVWs;
		m.TextureCoords = i.vTextureCoords.xy;
				
		return ShadingModelStandard::Shade( m );
	}
}
