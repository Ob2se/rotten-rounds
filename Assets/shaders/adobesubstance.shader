HEADER
{
    Description = "Substance Painter PBR — 1:1 Mapping with Parallax Height";
    Version = 1;
}

FEATURES
{
    #include "common/features.hlsl"

    Feature( F_SSS,              0..1, "Rendering" );
    Feature( F_HEIGHT_MAP,       0..1, "Rendering" );
    Feature( F_EMISSIVE,         0..1, "Rendering" );
    Feature( F_DETAIL_NORMAL,    0..1, "Rendering" );
    Feature( F_ALPHA_TEST,       0..1, "Rendering" );
}

MODES
{
    VrForward();
    Depth();
    ToolsVis( "tools_vis.shader" );
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

    // ─── Static feature combos ──────────────────────────────────────────────
    StaticCombo( S_SSS,           F_SSS,          Sys( ALL ) );
    StaticCombo( S_HEIGHT_MAP,    F_HEIGHT_MAP,   Sys( ALL ) );
    StaticCombo( S_EMISSIVE,      F_EMISSIVE,     Sys( ALL ) );
    StaticCombo( S_DETAIL_NORMAL, F_DETAIL_NORMAL,Sys( ALL ) );

    // ─── Samplers ────────────────────────────────────────────────────────────
    SamplerState g_sSampler < Filter( ANISO ); AddressU( WRAP ); AddressV( WRAP ); >;

    // ─── Input textures (Substance Painter channel layout) ───────────────────
    CreateInputTexture2D( BaseColor,        Srgb,   8, "",                 "_color",          "Base Color,0/10",        Default3( 1.0, 1.0, 1.0 ) );
    CreateInputTexture2D( Normal,           Linear, 8, "NormalizeNormals", "_normal",          "Normal,0/20",            Default3( 0.5, 0.5, 1.0 ) );
    CreateInputTexture2D( Roughness,        Linear, 8, "",                 "_rough",           "Roughness,0/30",         Default1( 0.5 ) );
    CreateInputTexture2D( Metallic,         Linear, 8, "",                 "_metal",           "Metallic,0/40",          Default1( 0.0 ) );
    CreateInputTexture2D( AmbientOcclusion, Linear, 8, "",                 "_ao",              "Ambient Occlusion,0/50", Default1( 1.0 ) );

#if ( S_HEIGHT_MAP )
    CreateInputTexture2D( Height,           Linear, 8, "",                 "_height",          "Height Map,1/10",        Default1( 0.5 ) );
#endif

#if ( S_EMISSIVE )
    CreateInputTexture2D( Emissive,         Srgb,   8, "",                 "_emissive",        "Emissive,2/10",          Default3( 0.0, 0.0, 0.0 ) );
#endif

#if ( S_DETAIL_NORMAL )
    CreateInputTexture2D( DetailNormal,     Linear, 8, "NormalizeNormals", "_detailnormal",    "Detail Normal,3/10",     Default3( 0.5, 0.5, 1.0 ) );
#endif

#if ( S_SSS )
    CreateInputTexture2D( SSSMask,          Linear, 8, "",                 "_sss",             "SSS Mask,4/10",          Default1( 1.0 ) );
#endif

    // ─── Compiled GPU textures ───────────────────────────────────────────────
    Texture2D g_tBaseColor        < Channel( RGBA, Box( BaseColor ),        Srgb   ); OutputFormat( BC7 );  SrgbRead( true );  >;
    Texture2D g_tNormal           < Channel( RGBA, Box( Normal ),           Linear ); OutputFormat( BC7 );  SrgbRead( false ); >;
    Texture2D g_tRoughness        < Channel( R,    Box( Roughness ),        Linear ); OutputFormat( BC7 );  SrgbRead( false ); >;
    Texture2D g_tMetallic         < Channel( R,    Box( Metallic ),         Linear ); OutputFormat( BC7 );  SrgbRead( false ); >;
    Texture2D g_tAmbientOcclusion < Channel( R,    Box( AmbientOcclusion ), Linear ); OutputFormat( BC7 );  SrgbRead( false ); >;

#if ( S_HEIGHT_MAP )
    Texture2D g_tHeight           < Channel( R,    Box( Height ),           Linear ); OutputFormat( BC7 );  SrgbRead( false ); >;
#endif

#if ( S_EMISSIVE )
    Texture2D g_tEmissive         < Channel( RGB,  Box( Emissive ),         Srgb   ); OutputFormat( BC7 );  SrgbRead( true );  >;
#endif

#if ( S_DETAIL_NORMAL )
    Texture2D g_tDetailNormal     < Channel( RGBA, Box( DetailNormal ),     Linear ); OutputFormat( BC7 );  SrgbRead( false ); >;
#endif

#if ( S_SSS )
    Texture2D g_tSSSMask          < Channel( R,    Box( SSSMask ),          Linear ); OutputFormat( BC7 );  SrgbRead( false ); >;
#endif

    // ─── Material scalar parameters ──────────────────────────────────────────

    // Parallax / Height
#if ( S_HEIGHT_MAP )
    float  g_flHeightScale      < UiType( Slider ); UiGroup( "Height Map,1/20" ); Default1( 0.05 );  Range( 0.001, 0.15 ); >;
    int    g_nPOMStepsMin       < UiType( Slider ); UiGroup( "Height Map,1/30" ); Default1( 8 );     Range( 4, 32 );       >;
    int    g_nPOMStepsMax       < UiType( Slider ); UiGroup( "Height Map,1/40" ); Default1( 32 );    Range( 8, 64 );       >;
#endif

    // Emissive
#if ( S_EMISSIVE )
    float  g_flEmissiveScale    < UiType( Slider ); UiGroup( "Emissive,2/20" );   Default1( 1.0 );   Range( 0.0, 10.0 );   >;
    float3 g_vEmissiveTint      < UiType( Color );  UiGroup( "Emissive,2/30" );   Default3( 1, 1, 1 );                      >;
#endif

    // Detail normal
#if ( S_DETAIL_NORMAL )
    float  g_flDetailNormalScale    < UiType( Slider ); UiGroup( "Detail Normal,3/20" ); Default1( 1.0 ); Range( 0.0, 3.0 ); >;
    float  g_flDetailNormalTiling   < UiType( Slider ); UiGroup( "Detail Normal,3/30" ); Default1( 4.0 ); Range( 1.0, 32.0 ); >;
    float  g_flDetailNormalBlend    < UiType( Slider ); UiGroup( "Detail Normal,3/40" ); Default1( 0.5 ); Range( 0.0, 1.0 );  >;
#endif

    // SSS
#if ( S_SSS )
    float3 g_vSSSColor          < UiType( Color );  UiGroup( "Subsurface Scattering,4/20" ); Default3( 1.0, 0.4, 0.3 ); >;
    float  g_flSSSScale         < UiType( Slider ); UiGroup( "Subsurface Scattering,4/30" ); Default1( 1.0 ); Range( 0.0, 2.0 ); >;
#endif

    // ─── Texture coordinate tiling & offset ─────────────────────────────────
    float4 g_vTextureTiling     < UiType( VectorText ); UiGroup( "UV,5/10" ); Default4( 1, 1, 0, 0 ); >;

    // ─── Helper: Reoriented Normal Blending ─────────────────────────────────
    // Produces correct results when blending two tangent-space normal maps.
    float3 RNBlend( float3 base, float3 detail )
    {
        base   = base   * float3(  2,  2, 2 ) + float3( -1, -1,  0 );
        detail = detail * float3( -2, -2, 2 ) + float3( 1,  1, -1 );
        return normalize( base * dot( base, detail ) - detail * base.z );
    }

#if ( S_HEIGHT_MAP )
    // ─── Parallax Occlusion Mapping ─────────────────────────────────────────
    // Adaptive step count based on view angle for best performance/quality tradeoff.
    float2 ParallaxOcclusionMap( float2 uv, float3 viewDirTs )
    {
        // Clamp z to prevent extreme UV stretching at grazing angles.
        float zSafe   = max( viewDirTs.z, 0.1 );
        // Total UV shift = (view projected onto surface plane) * height scale.
        // Shift is *with* viewDirTs.xy — UV walks toward where the viewer is.
        float2 dir = ( viewDirTs.xy / zSafe ) * g_flHeightScale;

        // Adaptive step count: more steps at grazing angles.
        float cosAngle = saturate( viewDirTs.z );
        int   steps    = (int)lerp( (float)g_nPOMStepsMax, (float)g_nPOMStepsMin, cosAngle );
        float stepSize = 1.0 / (float)steps;
        float2 uvStep  = dir * stepSize;

        float  layerH  = 1.0;      // current layer height (top → 0)
        float  surfH   = 0.0;
        float2 curUV   = uv;
        float2 prevUV  = uv;
        float  prevH   = 0.0;

        [loop]
        for ( int s = 0; s < steps; s++ )
        {
            layerH  -= stepSize;
            prevUV   = curUV;
            curUV   += uvStep;
            prevH    = surfH;
            surfH    = g_tHeight.SampleLevel( g_sSampler, curUV, 0 ).r;

            if ( surfH >= layerH )
                break;
        }

        // Linear interpolation between the last two steps for sub-step precision.
        float w = ( surfH - layerH ) / max( ( surfH - prevH ) + stepSize, 1e-5 );
        return lerp( curUV, prevUV, w );
    }
#endif

    // ─── Main pixel shader ──────────────────────────────────────────────────
    float4 MainPs( PixelInput i ) : SV_Target0
    {
        Material m = Material::From( i );

        // UV tiling
        float2 uv = i.vTextureCoords.xy * g_vTextureTiling.xy + g_vTextureTiling.zw;

#if ( S_HEIGHT_MAP )
        // Build tangent-space view direction for POM.
        float3 viewWs  = normalize( g_vCameraPositionWs.xyz - i.vPositionWithOffsetWs.xyz );
        float3 tanU    = normalize( i.vTangentUWs.xyz );
        float3 tanV    = normalize( i.vTangentVWs.xyz );
        float3 nrmWs   = normalize( i.vNormalWs );
        float3 viewTs  = float3( dot( viewWs, tanU ),
                                 dot( viewWs, tanV ),
                                 dot( viewWs, nrmWs ) );
        uv = ParallaxOcclusionMap( uv, viewTs );
#endif

        // Base textures
        float4 baseColor = g_tBaseColor.Sample(        g_sSampler, uv );
        float3 nrmSample = g_tNormal.Sample(           g_sSampler, uv ).xyz;
        float  roughness = g_tRoughness.Sample(        g_sSampler, uv ).r;
        float  metallic  = g_tMetallic.Sample(         g_sSampler, uv ).r;
        float  ao        = g_tAmbientOcclusion.Sample( g_sSampler, uv ).r;

        // Tangent-space normal from the map
        float3 localNormal = normalize( nrmSample * 2.0 - 1.0 );

#if ( S_DETAIL_NORMAL )
        float2 uvDetail    = uv * g_flDetailNormalTiling;
        float3 detailSample = g_tDetailNormal.Sample( g_sSampler, uvDetail ).xyz;
        float3 detailNormal = normalize( detailSample * 2.0 - 1.0 );
        // Blend using RNB, then scale detail influence
        float3 blendedNorm = RNBlend( localNormal, detailNormal );
        localNormal        = normalize( lerp( localNormal, blendedNorm, g_flDetailNormalBlend * g_flDetailNormalScale ) );
#endif

        // Fill material
        m.Albedo           = baseColor.rgb;
        m.Opacity          = baseColor.a;
        m.Normal           = TransformNormal( localNormal, i.vNormalWs, i.vTangentUWs, i.vTangentVWs );
        m.Roughness        = roughness;
        m.Metalness        = metallic;
        m.AmbientOcclusion = ao;
        m.TintMask         = 1.0;
        m.WorldTangentU    = i.vTangentUWs;
        m.WorldTangentV    = i.vTangentVWs;
        m.TextureCoords    = uv;

#if ( S_EMISSIVE )
        float3 emissiveSample = g_tEmissive.Sample( g_sSampler, uv ).rgb;
        m.Emission = emissiveSample * g_vEmissiveTint * g_flEmissiveScale;
#else
        m.Emission = float3( 0.0, 0.0, 0.0 );
#endif

#if ( S_SSS )
        float sssMask  = g_tSSSMask.Sample( g_sSampler, uv ).r;
        m.Transmission = g_vSSSColor * sssMask * g_flSSSScale;
#else
        m.Transmission = 0.0;
#endif

        return ShadingModelStandard::Shade( m );
    }
}