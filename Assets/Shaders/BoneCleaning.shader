Shader "RHC7/Bone Cleaning"
{
    Properties
    {
        _BoneColor ("Bone Color", Color) = (0.84, 0.78, 0.64, 1)
        _LooseDirtColor ("Loose Dirt", Color) = (0.28, 0.21, 0.13, 1)
        _PackedDirtColor ("Packed Dirt", Color) = (0.16, 0.12, 0.07, 1)
        _ContactDirtColor ("Contact Dirt", Color) = (0.07, 0.055, 0.035, 1)
        _WearColor ("Wear Color", Color) = (0.38, 0.19, 0.09, 1)
        _CrackColor ("Crack Color", Color) = (0.06, 0.04, 0.03, 1)
        _DirtMap ("Dirt (R) / Wear (G) Mask", 2D) = "white" {}
        _Smoothness ("Smoothness", Range(0, 1)) = 0.22
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_DirtMap);
            SAMPLER(sampler_DirtMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BoneColor;
                half4 _LooseDirtColor;
                half4 _PackedDirtColor;
                half4 _ContactDirtColor;
                half4 _WearColor;
                half4 _CrackColor;
                half _Smoothness;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float sum = 0.0;
                float amp = 0.5;
                for (int k = 0; k < 4; k++)
                {
                    sum += ValueNoise(p) * amp;
                    p *= 2.03;
                    amp *= 0.5;
                }
                return sum;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fogCoord : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = input.uv;
                output.fogCoord = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half2 mask = SAMPLE_TEXTURE2D(_DirtMap, sampler_DirtMap, input.uv).rg;
                half dirt = mask.r;
                half wear = mask.g;

                float nFine = Fbm(input.uv * float2(150.0, 325.0));
                float nMid = Fbm(input.uv * float2(42.0, 91.0) + 7.3);
                float nWide = Fbm(input.uv * float2(11.0, 24.0) + 21.7);
                float grit = ValueNoise(input.uv * float2(340.0, 740.0));
                float grit2 = ValueNoise(input.uv * float2(120.0, 260.0) + 13.1);

                // --- bone ---
                half3 bone = _BoneColor.rgb;
                bone *= 0.90 + 0.16 * nMid;
                bone *= 0.95 + 0.08 * nFine;
                bone = lerp(bone, bone * half3(0.82, 0.78, 0.72), smoothstep(0.62, 0.92, nWide) * 0.55);
                bone += (nFine - 0.5) * 0.03;

                // --- rock crust ---
                half3 crust = _LooseDirtColor.rgb * (0.72 + 0.50 * nFine);
                crust *= 0.82 + 0.32 * grit;
                crust *= 1.0 - smoothstep(0.72, 0.96, grit2) * 0.22;
                crust = lerp(crust, _ContactDirtColor.rgb * (0.82 + 0.30 * nFine), smoothstep(0.56, 0.44, dirt));
                crust *= 0.86 + 0.24 * nWide;

                float stain = exp(-(pow((input.uv.x - 0.18) * 5.0, 2.0) + pow((input.uv.y - 0.50) * 2.6, 2.0)));
                crust *= 1.0 - stain * 0.35;

                half crustCoverage = smoothstep(0.02, 0.40, dirt);
                crustCoverage *= 0.86 + 0.14 * smoothstep(0.2, 0.8, nMid);
                half3 albedo = lerp(bone, crust, crustCoverage);

                // --- wear: scratched, exposed bone ---
                half wearMask = smoothstep(0.20, 0.55, wear);
                half3 worn = _WearColor.rgb * (0.62 + 0.55 * nFine);
                worn = lerp(worn, _BoneColor.rgb * 0.62, 0.22);
                albedo = lerp(albedo, worn, wearMask * 0.78);

                // fresh scratches
                half scratch = smoothstep(0.10, 0.45, wear) * smoothstep(0.55, 0.78, ValueNoise(input.uv * float2(320.0, 690.0)));
                albedo = lerp(albedo, _WearColor.rgb * 1.2, scratch * 0.35);

                // deep chip / crack
                half chip = smoothstep(0.48, 0.70, wear);
                albedo = lerp(albedo, _CrackColor.rgb * 2.2, chip * 0.62);

                half crack = smoothstep(0.74, 0.90, wear);
                albedo = lerp(albedo, _CrackColor.rgb, crack);

                half smoothness = lerp(_Smoothness, 0.06, crustCoverage);
                smoothness = lerp(smoothness, 0.12, wearMask);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalize(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogCoord;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1.0, 1.0, 1.0, 1.0);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = 0.0;
                surfaceData.specular = 0.0;
                surfaceData.smoothness = smoothness;
                surfaceData.normalTS = half3(0.0, 0.0, 1.0);
                surfaceData.emission = 0.0;
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = 1.0;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, input.fogCoord);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            ShadowVaryings shadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = positionCS;
                return output;
            }

            half4 shadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
