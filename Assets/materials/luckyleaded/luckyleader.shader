HEADER
{
	Description = "Abyss black glass with scrolling cloud mask and pulsing lightning strikes";
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
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput i )
	{
		PixelInput o = ProcessVertex( i );
		return FinalizeVertex( o );
	}
}

PS
{
    #include "common/pixel.hlsl"

	SamplerState g_sSampler0 < Filter( ANISO ); AddressU( WRAP ); AddressV( WRAP ); >;

	CreateInputTexture2D( LightningMaskA, Linear, 8, "", "_lightning_a", "Lightning Mask A", Default3( 0.0, 0.0, 0.0 ) );
	Texture2D g_tLightningMaskA < Channel( RGBA, Box( LightningMaskA ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	CreateInputTexture2D( LightningMaskB, Linear, 8, "", "_lightning_b", "Lightning Mask B", Default3( 0.0, 0.0, 0.0 ) );
	Texture2D g_tLightningMaskB < Channel( RGBA, Box( LightningMaskB ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;
	CreateInputTexture2D( LightningMaskC, Linear, 8, "", "_lightning_c", "Lightning Mask C", Default3( 0.0, 0.0, 0.0 ) );
	Texture2D g_tLightningMaskC < Channel( RGBA, Box( LightningMaskC ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;

	// Lightning tint color - assign a solid color texture to change the lightning color
	CreateInputTexture2D( LightningTintColor, Srgb, 8, "", "_lightning_tint", "Lightning Tint Color", Default3( 0.4, 0.65, 1.0 ) );
	Texture2D g_tLightningTintColor < Channel( RGBA, Box( LightningTintColor ), Srgb ); OutputFormat( DXT5 ); SrgbRead( true ); >;

	float4 g_vGlassNoiseScroll < Attribute( "GlassNoiseScroll" ); Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float g_flGlassNoiseScale < Attribute( "GlassNoiseScale" ); Default1( 2.0 ); >;
	float g_flRefractionStrength < Attribute( "RefractionStrength" ); Default1( 0.12 ); >;
	float g_flGlassOpacity < Attribute( "GlassOpacity" ); Default1( 0.92 ); >;
	float4 g_vGlassTint < Attribute( "GlassTint" ); Default4( 0.15, 0.18, 0.22, 1.0 ); >;

	float4 g_vAbyssColor < Attribute( "AbyssColor" ); Default4( 0.005, 0.005, 0.012, 1.0 ); >;
	float4 g_vLightningColor < Attribute( "LightningColor" ); Default4( 0.4, 0.65, 1.0, 1.0 ); >;
	float g_flLightningIntensity < Attribute( "LightningIntensity" ); Default1( 3.5 ); >;
	float g_flLightningFrequency < Attribute( "LightningFrequency" ); Default1( 0.9 ); >;
	float g_flStrikeWidth < Attribute( "StrikeWidth" ); Default1( 0.10 ); >;
	float g_flLightningThreshold < Attribute( "LightningThreshold" ); Default1( 0.08 ); >;
	float g_flLightningSoftness < Attribute( "LightningSoftness" ); Default1( 0.06 ); >;
	float g_flLightningAreaScale < Attribute( "LightningAreaScale" ); Default1( 6.5 ); >;
	float g_flLightningAreaThreshold < Attribute( "LightningAreaThreshold" ); Default1( 0.80 ); >;
	float g_flLightningAreaSoftness < Attribute( "LightningAreaSoftness" ); Default1( 0.18 ); >;
	float g_flLightningEmissionClamp < Attribute( "LightningEmissionClamp" ); Default1( 5.0 ); >;
	float g_flTransmission < Attribute( "GlassTransmission" ); Default1( 0.15 ); >;
	float g_flFresnelPower < Attribute( "FresnelPower" ); Default1( 4.0 ); >;
	float g_flFresnelScale < Attribute( "FresnelScale" ); Default1( 0.4 ); >;
	float g_flFresnelBias < Attribute( "FresnelBias" ); Default1( 0.02 ); >;
	float g_flGlassSpecular < Attribute( "GlassSpecular" ); Default1( 0.35 ); >;
	float g_flAbyssDepth < Attribute( "AbyssDepth" ); Default1( 0.6 ); >;
	float g_flEdgeTransparency < Attribute( "EdgeTransparency" ); Default1( 0.35 ); >;
	float g_flLightningGlowRadius < Attribute( "LightningGlowRadius" ); Default1( 0.35 ); >;
	float g_flLightningEmissionBoost < Attribute( "LightningEmissionBoost" ); Default1( 6.0 ); >;

	float Hash11( float n )
	{
		return frac( sin( n * 127.1 ) * 43758.5453123 );
	}

	float Hash21( float2 p )
	{
		return frac( sin( dot( p, float2( 127.1, 311.7 ) ) ) * 43758.5453123 );
	}

	float ValueNoise2D( float2 p )
	{
		float2 i = floor( p );
		float2 f = frac( p );
		float2 u = f * f * ( 3.0 - 2.0 * f );
		float a = Hash21( i + float2( 0.0, 0.0 ) );
		float b = Hash21( i + float2( 1.0, 0.0 ) );
		float c = Hash21( i + float2( 0.0, 1.0 ) );
		float d = Hash21( i + float2( 1.0, 1.0 ) );
		return lerp( lerp( a, b, u.x ), lerp( c, d, u.x ), u.y );
	}

	// FBM for richer glass distortion with multiple octaves
	float GlassNoise( float2 p )
	{
		float val = 0.0;
		float amp = 0.5;
		float freq = 1.0;
		for ( int octave = 0; octave < 3; octave++ )
		{
			val += ValueNoise2D( p * freq ) * amp;
			freq *= 2.17;
			amp *= 0.45;
		}
		return val;
	}

	float StrikePulse( float seed, float timeValue, float frequency, float width )
	{
		float localWidth = max( width, 0.005 );
		float x = timeValue * frequency + seed;
		float phase = frac( x );
		float core = smoothstep( 0.0, localWidth, phase ) * ( 1.0 - smoothstep( localWidth, localWidth * 2.4, phase ) );
		float amp = lerp( 0.45, 1.0, Hash11( floor( x ) + seed * 31.73 ) );
		return core * amp;
	}

	// Compute Fresnel reflectance (Schlick approximation)
	float FresnelSchlick( float cosTheta, float bias, float scale, float power )
	{
		return bias + scale * pow( saturate( 1.0 - cosTheta ), power );
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::Init( i );
		m.Albedo = float3( 0.0, 0.0, 0.0 );
		m.Normal = float3( 0.0, 0.0, 1.0 );
		m.Roughness = 0.02;
		m.Metalness = 0.0;
		m.AmbientOcclusion = 1.0;
		m.TintMask = 1.0;
		m.Opacity = 1.0;
		m.Emission = float3( 0.0, 0.0, 0.0 );
		m.Transmission = 0.0;

		float2 uv = i.vTextureCoords.xy;
		float timeValue = g_flTime;

		// ============================================================
		// GLASS LAYER - wavy distortion with animated scroll
		// ============================================================
		float2 scrolledUv = uv + g_vGlassNoiseScroll.xy * timeValue;
		float2 noiseUv = scrolledUv * max( g_flGlassNoiseScale, 0.02 );
		float noiseVal = GlassNoise( noiseUv );

		// Compute gradient for surface normal perturbation
		float eps = 0.008;
		float noiseRight = GlassNoise( noiseUv + float2( eps, 0.0 ) );
		float noiseUp    = GlassNoise( noiseUv + float2( 0.0, eps ) );

		float2 glassGradient = float2( noiseVal - noiseRight, noiseVal - noiseUp ) / eps;
		// Strong normal perturbation for very visible glass distortion
		float3 glassNormal = normalize( float3( glassGradient * 0.15, 1.0 ) );
		m.Normal = glassNormal;

		float2 glassDistort = glassGradient * g_flRefractionStrength * 0.01;

		// ============================================================
		// FRESNEL - glassy rim reflections
		// ============================================================
		float3 worldNormal = TransformNormal( m.Normal, i.vNormalWs, i.vTangentUWs, i.vTangentVWs );
		float3 viewDir = normalize( g_vCameraPositionWs.xyz - i.vPositionWithOffsetWs.xyz );
		float NdotV = saturate( dot( worldNormal, viewDir ) );
		float fresnel = FresnelSchlick( NdotV, g_flFresnelBias, g_flFresnelScale, g_flFresnelPower );

		// ============================================================
		// LIGHTNING LAYER - pulsing strikes deep inside the abyss
		// ============================================================
		float2 uvA = uv * 1.9 + float2( 0.31, -0.08 ) + glassDistort;
		float2 uvB = uv * 2.4 + float2( -0.21, 0.19 ) + glassDistort * 1.2;
		float2 uvC = uv * 1.5 + float2( 0.11, 0.43 ) + glassDistort * 0.8;

		float4 lightningSampleA = g_tLightningMaskA.Sample( g_sSampler0, uvA );
		float4 lightningSampleB = g_tLightningMaskB.Sample( g_sSampler0, uvB );
		float4 lightningSampleC = g_tLightningMaskC.Sample( g_sSampler0, uvC );

		float lightningRawA = max( lightningSampleA.r, max( lightningSampleA.g, lightningSampleA.b ) ) * lightningSampleA.a;
		float lightningRawB = max( lightningSampleB.r, max( lightningSampleB.g, lightningSampleB.b ) ) * lightningSampleB.a;
		float lightningRawC = max( lightningSampleC.r, max( lightningSampleC.g, lightningSampleC.b ) ) * lightningSampleC.a;

		float lightningShapeA = smoothstep( g_flLightningThreshold, g_flLightningThreshold + g_flLightningSoftness, lightningRawA );
		float lightningShapeB = smoothstep( g_flLightningThreshold, g_flLightningThreshold + g_flLightningSoftness, lightningRawB );
		float lightningShapeC = smoothstep( g_flLightningThreshold, g_flLightningThreshold + g_flLightningSoftness, lightningRawC );

		float strikeA = lightningShapeA * StrikePulse( 1.17, timeValue, g_flLightningFrequency * 1.15, g_flStrikeWidth );
		float strikeB = lightningShapeB * StrikePulse( 2.83, timeValue, g_flLightningFrequency * 0.92, g_flStrikeWidth * 0.85 );
		float strikeC = lightningShapeC * StrikePulse( 4.61, timeValue, g_flLightningFrequency * 1.36, g_flStrikeWidth * 1.1 );
		float lightningMask = max( strikeA, max( strikeB, strikeC ) );

		// Soft glow aura around the lightning bolts
		float glowA = smoothstep( g_flLightningGlowRadius, 0.0, 1.0 - lightningRawA ) * StrikePulse( 1.17, timeValue, g_flLightningFrequency * 1.15, g_flStrikeWidth * 3.0 );
		float glowB = smoothstep( g_flLightningGlowRadius, 0.0, 1.0 - lightningRawB ) * StrikePulse( 2.83, timeValue, g_flLightningFrequency * 0.92, g_flStrikeWidth * 2.5 );
		float glowC = smoothstep( g_flLightningGlowRadius, 0.0, 1.0 - lightningRawC ) * StrikePulse( 4.61, timeValue, g_flLightningFrequency * 1.36, g_flStrikeWidth * 3.3 );
		float glowMask = max( glowA, max( glowB, glowC ) );

		// ============================================================
		// COMPOSITION - abyss + lightning + glass
		// ============================================================

		// 1. Deep black abyss base
		float3 abyssColor = g_vAbyssColor.rgb;

		// 2. Lightning emission (bright bolts + wider glow)
		// Sample the tint color texture for lightning coloring
		float3 lightningTint = g_tLightningTintColor.Sample( g_sSampler0, uv ).rgb;
		float3 boltEmission = lightningTint * saturate( lightningMask ) * g_flLightningIntensity;
		float3 glowEmission = lightningTint * saturate( glowMask ) * g_flLightningIntensity * 0.25;
		float3 totalLightning = boltEmission + glowEmission;

		// 3. The interior: deep black with lightning
		float3 interiorColor = abyssColor + totalLightning;

		// 4. Glass surface: subtle colored Fresnel rim over the abyss interior
		//    Tinted with glass color so it doesn't appear white
		float3 glassReflection = g_vGlassTint.rgb * fresnel * 0.5;

		// The interior stays nearly untouched - just a very subtle glass overlay
		float3 finalColor = interiorColor + glassReflection;

		// Lightning illuminates the glass surface faintly from inside
		finalColor += totalLightning * 0.05 * fresnel;

		// 5. Apply to material
		m.Albedo = float3( 0.01, 0.01, 0.01 ); // Near-black base
		m.Roughness = 0.05; // Very smooth glass but not mirror-perfect
		m.Metalness = g_flGlassSpecular; // Moderate metalness for subtle env reflections
		m.Transmission = g_flTransmission;

		// Emission: the base interior color plus heavily boosted lightning
		// The emission boost makes lightning areas bright enough to contribute
		// self-illumination via bloom and indirect lighting in the scene
		float3 boostedLightning = totalLightning * g_flLightningEmissionBoost;
		m.Emission = finalColor + boostedLightning;

		// Fully opaque - the abyss and lightning are visible through emission, not transparency
		m.Opacity = 1.0;

		m.AmbientOcclusion = saturate( m.AmbientOcclusion );
		m.Roughness = saturate( m.Roughness );
		m.Metalness = saturate( m.Metalness );
		m.Opacity = saturate( m.Opacity );

		m.Normal = TransformNormal( m.Normal, i.vNormalWs, i.vTangentUWs, i.vTangentVWs );
		m.WorldTangentU = i.vTangentUWs;
		m.WorldTangentV = i.vTangentVWs;
		m.TextureCoords = i.vTextureCoords.xy;

		return ShadingModelStandard::Shade( m );
	}
}
