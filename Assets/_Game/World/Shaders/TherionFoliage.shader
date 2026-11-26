// Leaves, bark and grass for the island. Lit like URP/Lit, plus: alpha cutout, light shining through
// leaves, wind sway in the vertex shader, and an optional root-to-tip colour gradient for grass blades.
// Works with terrain trees and instanced terrain details (GPU instancing).
Shader "Therion/Foliage"
{
    Properties
    {
        _BaseMap("Albedo", 2D) = "white" {}
        _BaseColor("Color", Color) = (1,1,1,1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.4
        [Toggle(_NORMALMAP)] _UseNormalMap("Use Normal Map", Float) = 0
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Float) = 1
        _Smoothness("Smoothness", Range(0,1)) = 0.15
        _Translucency("Light Through Leaves", Range(0,2)) = 0
        _NormalUp("Blend Normals Up (grass)", Range(0,1)) = 0

        [Header(Grass gradient)]
        _Gradient("Use Root To Tip Gradient", Range(0,1)) = 0
        _RootColor("Root Color", Color) = (0.10,0.16,0.05,1)
        _TipColor("Tip Color", Color) = (0.36,0.48,0.14,1)
        _DryColor("Dry Patch Color", Color) = (0.55,0.50,0.22,1)
        _DryAmount("Dry Patch Amount", Range(0,1)) = 0.35

        [Header(Wind)]
        _WindDir("Wind Direction (x, z)", Vector) = (1,0,0.35,0)
        _WindStrength("Bend At Top (m)", Float) = 0
        _BendHeight("Plant Height (m)", Float) = 1
        _Flutter("Leaf Flutter (m)", Float) = 0

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        half _Cutoff;
        half _BumpScale;
        half _Smoothness;
        half _Translucency;
        half _NormalUp;
        half _Gradient;
        half4 _RootColor;
        half4 _TipColor;
        half4 _DryColor;
        half _DryAmount;
        float4 _WindDir;
        float _WindStrength;
        float _BendHeight;
        float _Flutter;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

        float TherionHash(float2 p)
        {
            return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
        }

        float TherionNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = TherionHash(i);
            float b = TherionHash(i + float2(1.0, 0.0));
            float c = TherionHash(i + float2(0.0, 1.0));
            float d = TherionHash(i + float2(1.0, 1.0));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        // Sways the plant around its pivot: nothing at the base, most at the top.
        float3 TherionWind(float3 positionWS)
        {
            float4x4 m = GetObjectToWorldMatrix();
            float3 pivot = float3(m._m03, m._m13, m._m23);
            float h = max(0.0, positionWS.y - pivot.y);
            float k = min(h / max(_BendHeight, 0.01), 1.5);
            float2 dir = _WindDir.xz;
            dir /= max(length(dir), 0.001);
            float t = _Time.y;
            float phase = dot(pivot.xz, dir) * 0.12 + dot(pivot.xz, float2(0.031, 0.047));
            float gust = 0.55 + 0.45 * sin(t * 0.9 - phase) * sin(t * 0.33 - phase * 0.6 + 1.3);
            positionWS.xz += dir * (gust * k * k * _WindStrength);
            float f = sin(t * 3.7 + dot(positionWS, float3(2.1, 3.3, 1.7)));
            positionWS += float3(f, 0.4 * f, -f) * (_Flutter * k);
            return positionWS;
        }

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct LiteVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        void TherionAlphaClip(float2 uv)
        {
            #if defined(_ALPHATEST_ON)
            half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a;
            clip(a - _Cutoff);
            #endif
        }

        LiteVaryings LiteVert(Attributes input)
        {
            LiteVaryings o = (LiteVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            float3 positionWS = TherionWind(TransformObjectToWorld(input.positionOS.xyz));
            o.positionCS = TransformWorldToHClip(positionWS);
            o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            o.normalWS = TransformObjectToWorldNormal(input.normalOS);
            return o;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _NORMALMAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half3 fogAndGrad : TEXCOORD4; // fog, root-to-tip, per-plant shade
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionWS = TherionWind(TransformObjectToWorld(input.positionOS.xyz));
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                half3 n = TransformObjectToWorldNormal(input.normalOS);
                o.normalWS = normalize(lerp(n, half3(0.0, 1.0, 0.0), _NormalUp));
                o.tangentWS = half4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w * GetOddNegativeScale());
                float4x4 m = GetObjectToWorldMatrix();
                half shade = TherionHash(float2(m._m03, m._m23) * 0.37);
                o.fogAndGrad = half3(ComputeFogFactor(o.positionCS.z), input.uv.y, shade);
                return o;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                #if defined(_ALPHATEST_ON)
                clip(tex.a * _BaseColor.a - _Cutoff);
                #endif
                half3 albedo = tex.rgb * _BaseColor.rgb;

                half grad = saturate(input.fogAndGrad.y);
                half occlusion = 1.0;
                if (_Gradient > 0.01)
                {
                    half3 g = lerp(_RootColor.rgb, _TipColor.rgb, grad);
                    half patch = TherionNoise(input.positionWS.xz * 0.07);
                    patch = saturate(patch * 1.6 - 0.3);
                    g = lerp(g, _DryColor.rgb, patch * _DryAmount * saturate(grad + 0.25));
                    g *= lerp(0.72, 1.18, input.fogAndGrad.z);
                    albedo = lerp(albedo, albedo * g, _Gradient);
                    occlusion = lerp(1.0, lerp(0.45, 1.0, grad), _Gradient);
                }

                half3 normalWS = normalize(input.normalWS);
                #if defined(_NORMALMAP)
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 bitangentWS = input.tangentWS.w * cross(normalWS, input.tangentWS.xyz);
                normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, normalWS)));
                #endif
                half face = IS_FRONT_VFACE(facing, 1.0, -1.0);
                if (_NormalUp < 0.5) normalWS *= face;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                inputData.shadowCoord = float4(0.0, 0.0, 0.0, 0.0);
                #endif
                inputData.fogCoord = input.fogAndGrad.x;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1.0, 1.0, 1.0, 1.0);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.alpha = 1.0;
                surface.metallic = 0.0;
                surface.specular = half3(0.0, 0.0, 0.0);
                surface.smoothness = _Smoothness;
                surface.occlusion = occlusion;
                surface.normalTS = half3(0.0, 0.0, 1.0);

                half4 color = UniversalFragmentPBR(inputData, surface);

                // Sunlight glowing through thin leaves when seen from behind.
                Light mainLight = GetMainLight(inputData.shadowCoord);
                half back = saturate(dot(-normalWS, mainLight.direction));
                color.rgb += albedo * mainLight.color * (back * _Translucency * 0.6 * mainLight.shadowAttenuation);

                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return half4(color.rgb, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            LiteVaryings ShadowVert(Attributes input)
            {
                LiteVaryings o = (LiteVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                float3 positionWS = TherionWind(TransformObjectToWorld(input.positionOS.xyz));
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = positionCS;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 ShadowFrag(LiteVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                TherionAlphaClip(input.uv);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull [_Cull]
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex LiteVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            half4 DepthFrag(LiteVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                TherionAlphaClip(input.uv);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma vertex LiteVert
            #pragma fragment DepthNormalsFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            half4 DepthNormalsFrag(LiteVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                TherionAlphaClip(input.uv);
                half3 n = normalize(lerp(input.normalWS, half3(0.0, 1.0, 0.0), _NormalUp));
                return half4(n, 0.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Simple Lit"
}
