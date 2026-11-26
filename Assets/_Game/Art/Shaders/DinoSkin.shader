// Dino skin shader for URP (Unity 6). Recolors a realistic dino texture by zone while keeping its detail.
//
// Inputs per species (art):
//   _BaseMap    the model's realistic albedo
//   _BumpMap    normal map (optional)
//   _ZoneMask   RGBA mask painted on the same UVs: R = belly, G = accent (crest, frill, plates, claws),
//               B = eyes, A = where patterns may appear (1 = body, 0 = no pattern e.g. claws, mouth)
// Shared by all species:
//   _PatternMasks  Texture2DArray of grayscale patterns (slice 0 = Stripes ... 7 = Starry), tiled over UV0
//
// Per dino (set by DinoSkin through a MaterialPropertyBlock): the five tints, _PatternIndex and _TintStrength.
// Color = lerp(original albedo, tint x albedo detail, _TintStrength). "Detail" is the albedo's luminance
// normalized around its average, so scales, wrinkles and painted shading survive any recolor.
Shader "Dino/DinoSkin"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo (realistic)", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Float) = 1
        _ZoneMask ("Zone Mask (R belly, G accent, B eyes, A pattern area)", 2D) = "black" {}
        _PatternMasks ("Pattern Masks (array)", 2DArray) = "" {}
        _PatternTiling ("Pattern Tiling", Float) = 4
        _DetailAverage ("Albedo Average Luminance", Range(0.05, 1)) = 0.35
        _DetailContrast ("Detail Contrast", Range(0, 2)) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.25
        _EyeSmoothness ("Eye Smoothness", Range(0, 1)) = 0.9

        [HideInInspector] _BaseTint ("Base Tint", Color) = (0.36, 0.73, 0.29, 1)
        [HideInInspector] _BellyTint ("Belly Tint", Color) = (0.96, 0.91, 0.79, 1)
        [HideInInspector] _PatternTint ("Pattern Tint", Color) = (0.24, 0.49, 0.23, 1)
        [HideInInspector] _AccentTint ("Accent Tint", Color) = (0.94, 0.54, 0.24, 1)
        [HideInInspector] _EyeTint ("Eye Tint", Color) = (0.94, 0.63, 0.13, 1)
        [HideInInspector] _PatternIndex ("Pattern Index", Float) = 0
        _TintStrength ("Tint Strength (0 = original texture)", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float _BumpScale, _PatternTiling, _DetailAverage, _DetailContrast, _Smoothness, _EyeSmoothness;
            half4 _BaseTint, _BellyTint, _PatternTint, _AccentTint, _EyeTint;
            float _PatternIndex, _TintStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);  SAMPLER(sampler_BumpMap);
            TEXTURE2D(_ZoneMask); SAMPLER(sampler_ZoneMask);
            TEXTURE2D_ARRAY(_PatternMasks); SAMPLER(sampler_PatternMasks);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                half4  tangentWS  : TEXCOORD3;
                half   fog        : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = n.normalWS;
                o.tangentWS = half4(n.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half3 SkinAlbedo(float2 uv, out half eyes)
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                half4 zones = SAMPLE_TEXTURE2D(_ZoneMask, sampler_ZoneMask, uv);
                eyes = zones.b;

                // Keep the realistic detail: luminance relative to the texture's average.
                half lum = dot(albedo, half3(0.2126, 0.7152, 0.0722));
                half detail = clamp(lum / max(_DetailAverage, 0.05), 0, 2) * _DetailContrast + (1 - _DetailContrast);

                half3 tint = _BaseTint.rgb;
                if (_PatternIndex > 0.5)
                {
                    half pattern = SAMPLE_TEXTURE2D_ARRAY(_PatternMasks, sampler_PatternMasks, uv * _PatternTiling, _PatternIndex - 1).r;
                    tint = lerp(tint, _PatternTint.rgb, pattern * zones.a);
                }
                tint = lerp(tint, _BellyTint.rgb, zones.r);
                tint = lerp(tint, _AccentTint.rgb, zones.g);
                tint = lerp(tint, _EyeTint.rgb, zones.b);

                half3 recolored = saturate(tint * detail);
                return lerp(albedo, recolored, _TintStrength);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half eyes;
                half3 albedo = SkinAlbedo(i.uv, eyes);

                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                half3 bitangent = i.tangentWS.w * cross(i.normalWS, i.tangentWS.xyz);
                half3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(nTS, half3x3(i.tangentWS.xyz, bitangent, i.normalWS)));

                InputData input = (InputData)0;
                input.positionWS = i.positionWS;
                input.positionCS = i.positionCS;
                input.normalWS = normalWS;
                input.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                input.fogCoord = i.fog;
                input.bakedGI = SampleSH(normalWS);
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo;
                s.alpha = 1;
                s.metallic = 0;
                s.specular = half3(0, 0, 0);
                s.smoothness = lerp(_Smoothness, _EyeSmoothness, eyes);
                s.normalTS = nTS;
                s.occlusion = 1;

                half4 color = UniversalFragmentPBR(input, s);
                color.rgb = MixFog(color.rgb, i.fog);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; };

            V ShadowVert(A v)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - posWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, lightDir));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
            #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.positionCS = cs;
                return o;
            }

            half4 ShadowFrag(V i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; };

            V DepthVert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half DepthFrag(V i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
