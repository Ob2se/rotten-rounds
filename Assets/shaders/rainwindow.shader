HEADER
{
    Description = "Animated rain on a glass window surface";
}

FEATURES
{
    #include "common/features.hlsl"
    Feature( F_TRANSLUCENT, 0..1, "Rendering" );
}

MODES
{
    Forward();
    Translucent();
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

    float g_flRainSpeed      < Attribute( "RainSpeed" );      Default1( 0.8 );  >;
    float g_flRainDensity    < Attribute( "RainDensity" );    Default1( 12.0 ); >;
    float g_flDropSize       < Attribute( "DropSize" );       Default1( 0.22 ); >;
    float g_flStreakLength    < Attribute( "StreakLength" );   Default1( 0.55 ); >;
    float g_flNormalStrength < Attribute( "NormalStrength" ); Default1( 0.5 );  >;
    float g_flGlassOpacity   < Attribute( "GlassOpacity" );   Default1( 0.15 ); >;
    float g_flDropOpacity    < Attribute( "DropOpacity" );    Default1( 0.9 );  >;
    float g_flGlassRoughness < Attribute( "GlassRoughness" ); Default1( 0.04 ); >;
    float4 g_vGlassTint      < Attribute( "GlassTint" );      Default4( 0.80, 0.88, 0.95, 1.0 ); >;

    float Hash1( float2 p )
    {
        float3 p3 = frac( float3( p.xyx ) * 0.1031 );
        p3 += dot( p3, p3.yzx + 33.33 );
        return frac( ( p3.x + p3.y ) * p3.z );
    }

    float RainCell( float2 cellUV, float2 cellID, float time )
    {
        float rndX    = Hash1( cellID );
        float rndSpd  = Hash1( cellID + 7.3 );
        float rndSz   = Hash1( cellID + 13.1 );
        float rndSkip = Hash1( cellID + 31.7 );

        if ( rndSkip > 0.7 ) return 0.0;

        float fallSpeed = ( 0.25 + rndSpd * 0.65 ) * g_flRainSpeed;
        float phase     = frac( time * fallSpeed + rndX );

        // UV Y increases going DOWN on screen (standard UV).
        // Drop falls downward = dc.y increases with phase.
        float2 dc;
        dc.x = ( rndX - 0.5 ) * 0.55;
        dc.y = -0.45 + phase * 0.9;

        // Drop body
        float2 d = cellUV - dc;
        d.x /= 0.3;
        d.y /= g_flDropSize * 0.5;
        float drop = smoothstep( 1.0, 0.0, length( d ) );

        // Trail sits ABOVE drop (lower Y = higher on screen = where drop came from)
        float streakLen = g_flStreakLength * ( 0.4 + rndSz * 0.45 );
        float streakTop = dc.y - streakLen;
        float streak    = 0.0;
        if ( cellUV.y < dc.y && cellUV.y > streakTop )
        {
            float t     = ( cellUV.y - streakTop ) / streakLen;
            float halfW = g_flDropSize * 0.4 * t;
            streak      = smoothstep( halfW, 0.0, abs( cellUV.x - dc.x ) ) * t;
        }

        return saturate( drop + streak * 0.7 );
    }

    float SampleRain( float2 uv, float time )
    {
        float2 scaled = uv * g_flRainDensity;
        float2 cellID = floor( scaled );
        float2 cellUV = frac( scaled ) - 0.5;
        return RainCell( cellUV, cellID, time );
    }

    float3 RainNormal( float2 uv, float time )
    {
        float eps = 0.8 / ( g_flRainDensity * 256.0 );
        float c   = SampleRain( uv, time );
        float cx  = SampleRain( uv + float2( eps, 0.0 ), time );
        float cy  = SampleRain( uv + float2( 0.0, eps ), time );
        return normalize( float3( ( c - cx ) * g_flNormalStrength,
                                  ( c - cy ) * g_flNormalStrength,
                                  0.25 ) );
    }

    float4 MainPs( PixelInput i ) : SV_Target0
    {
        float2 uv   = i.vTextureCoords.xy;
        float  time = g_flTime;

        float rain1 = SampleRain( uv,                               time       );
        float rain2 = SampleRain( uv * 1.7 + float2( 0.41, 0.67 ), time * 0.5 );
        float rain3 = SampleRain( uv * 2.9 + float2( 0.83, 0.22 ), time * 1.3 );

        float totalRain = saturate( rain1 * 0.55 + rain2 * 0.3 + rain3 * 0.2 );

        float3 n1 = RainNormal( uv,                               time       );
        float3 n2 = RainNormal( uv * 1.7 + float2( 0.41, 0.67 ), time * 0.5 );
        float3 n3 = RainNormal( uv * 2.9 + float2( 0.83, 0.22 ), time * 1.3 );

        float3 flat   = float3( 0.0, 0.0, 1.0 );
        float3 blendN = normalize( n1 * rain1 * 0.55
                                 + n2 * rain2 * 0.3
                                 + n3 * rain3 * 0.2
                                 + flat * ( 1.0 - totalRain ) );

        // Use the geometric surface normal for Fresnel, not the bump normal
        // (bump normal points sideways near drops, making Fresnel = 1.0 = white everywhere)
        float3 geoNormal = normalize( i.vNormalWs );
        float3 viewDir   = normalize( g_vCameraPositionWs.xyz - i.vPositionWithOffsetWs.xyz );
        float  NdotV     = saturate( dot( geoNormal, viewDir ) );

        // Subtle glass rim shimmer, capped so it never goes fully white
        float  fresnel    = pow( saturate( 1.0 - NdotV ), 5.0 ) * 0.25;
        float3 glassColor = g_vGlassTint.rgb * fresnel;

        // Water drops are grey-blue, not bright white
        float3 dropColor  = float3( 0.55, 0.65, 0.75 );
        float3 finalColor = lerp( glassColor, dropColor, totalRain * 0.6 );

        // Dry glass = nearly invisible; drops/streaks = opaque water blobs
        float  alpha = lerp( g_flGlassOpacity, g_flDropOpacity, totalRain );

        return float4( finalColor, alpha );
    }
}

