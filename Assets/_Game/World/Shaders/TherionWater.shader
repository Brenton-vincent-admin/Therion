// Lakes, rivers and the sea. Colour gets deeper with water depth, the bottom shows through near the
// shore (refracted), the sky reflects at shallow angles, the sun glints, and rivers flow downstream.
// Depth and refraction need the camera depth / opaque textures; WorldQualityTier switches them per
// graphics level through the two _TherionWater* globals, so the same material works on every level.
Shader "Therion/Water"
{
    Properties
    {
        _ShallowColor("Shallow Color", Color) = (0.20,0.42,0.36,1)
        _DeepColor("Deep Color (alpha = murkiness)", Color) = (0.02,0.11,0.13,0.92)
        _DepthFade("Depth Fade (m)", Float) = 3.5
        _ShoreFade("Shore Softness (m)", Float) = 0.25
        [NoScaleOffset][Normal] _BumpMap("Wave Normal Map", 2D) = "bump" {}
        _NormalTiling("Wave Tiling (1/m)", Float) = 0.11
        _NormalStrength("Wave Strength", Range(0,2)) = 0.45
        _FlowSpeed("Still Water Drift (x, z)", Vector) = (0.020,0.013,0,0)
        _Smoothness("Smoothness", Range(0,1)) = 0.94
        _Refraction("Refraction", Range(0,0.2)) = 0.035
        _FoamColor("Foam Color", Color) = (0.92,0.95,0.95,0.7)
        _FoamWidth("Shore Foam Width (m)", Float) = 0.35
        _SunGlint("Sun Glint", Range(0,20)) = 6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _ShallowColor;
            half4 _DeepColor;
            float _DepthFade;
            float _ShoreFade;
            float _NormalTiling;
            half _NormalStrength;
            float4 _FlowSpeed;
            half _Smoothness;
            half _Refraction;
            half4 _FoamColor;
            float _FoamWidth;
            half _SunGlint;
            CBUFFER_END

            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            // Set by WorldQualityTier. 0 = texture not available on this graphics level.
            float _TherionWaterDepth;
            float _TherionWaterRefraction;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 flow : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float3 flowAndFog : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.flowAndFog = float3(input.flow, ComputeFogFactor(o.positionCS.z));
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 0.0001);
                float surfaceEye = input.screenPos.w;
                float3 viewVec = GetCameraPositionWS() - input.positionWS;
                float viewDist = length(viewVec);
                half3 V = viewVec / max(viewDist, 0.0001);

                // Waves: one flowing layer (two phases cross-faded so it never stretches) plus a slow big one.
                float t = _Time.y;
                float2 baseUV = input.positionWS.xz * _NormalTiling;
                float2 drift = _FlowSpeed.xy * t;
                float2 flow = input.flowAndFog.xy * _NormalTiling * 2.5;
                float p0 = frac(t * 0.4);
                float p1 = frac(t * 0.4 + 0.5);
                half w = abs(1.0 - 2.0 * p0);
                half3 n0 = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, baseUV - flow * p0 + drift), _NormalStrength);
                half3 n1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, baseUV - flow * p1 + drift + 0.37), _NormalStrength);
                half3 nA = lerp(n0, n1, w);
                half3 nB = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, baseUV * 0.31 - drift * 0.7), _NormalStrength);
                half3 nT = half3(nA.xy + nB.xy, nA.z * nB.z);
                nT.xy *= saturate(120.0 / max(viewDist, 1.0));
                half3 N = normalize(half3(nT.x, max(nT.z, 0.05), nT.y));

                // How much water we are looking through.
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float thickness = max(0.0, sceneEye - surfaceEye);
                float2 refractUV = screenUV + nT.xy * (_Refraction * saturate(thickness));
                float refractEye = LinearEyeDepth(SampleSceneDepth(refractUV), _ZBufferParams);
                float useRefract = step(surfaceEye, refractEye);
                refractUV = lerp(screenUV, refractUV, useRefract);
                thickness = lerp(thickness, max(0.0, refractEye - surfaceEye), useRefract);
                thickness = lerp(_DepthFade * 0.8, thickness, _TherionWaterDepth);
                half shore = lerp(1.0, saturate(thickness / max(_ShoreFade, 0.001)), _TherionWaterDepth);

                half absorb = 1.0 - exp(-thickness / max(_DepthFade, 0.01));
                half3 body = lerp(_ShallowColor.rgb, _DeepColor.rgb, absorb);

                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                float4 shadowCoord = float4(0.0, 0.0, 0.0, 0.0);
                #endif
                Light mainLight = GetMainLight(shadowCoord);
                half sunUp = saturate(mainLight.direction.y);
                half3 ambient = SampleSH(half3(0.0, 1.0, 0.0));
                half3 lit = body * (mainLight.color * (sunUp * lerp(0.45, 1.0, mainLight.shadowAttenuation)) + ambient);

                half opacity = saturate(absorb * _DeepColor.a + 0.06);
                half3 below = SampleSceneColor(refractUV) * lerp(half3(1.0, 1.0, 1.0), _ShallowColor.rgb * 1.6, saturate(absorb * 1.5));
                half3 color = lerp(lit, lerp(below, lit, opacity), _TherionWaterRefraction);

                // Shore and rapids foam.
                half speed = length(input.flowAndFog.xy);
                half foam = (1.0 - saturate(thickness / max(_FoamWidth, 0.001))) * _TherionWaterDepth * 0.6;
                foam = max(foam, saturate((speed - 2.5) * 0.4) * (0.55 + 0.45 * nA.x * 4.0));
                foam = saturate(foam) * _FoamColor.a;
                half3 foamLit = _FoamColor.rgb * (mainLight.color * (sunUp * mainLight.shadowAttenuation) * 0.6 + ambient);
                color = lerp(color, foamLit, foam);

                // Sky reflection and sun glint.
                half fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(N, V)), 5.0);
                fresnel *= 1.0 - foam;
                half3 R = reflect(-V, N);
                R.y = abs(R.y);
                half3 sky = GlossyEnvironmentReflection(R, 1.0 - _Smoothness, 1.0);
                color = lerp(color, sky, fresnel);
                half3 H = normalize(mainLight.direction + V);
                half glint = pow(saturate(dot(N, H)), 500.0) * _SunGlint * mainLight.shadowAttenuation * (1.0 - foam);
                color += mainLight.color * glint;

                half alpha = lerp(saturate(opacity + fresnel + foam + glint), 1.0, _TherionWaterRefraction) * shore;
                color = MixFog(color, input.flowAndFog.z);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
