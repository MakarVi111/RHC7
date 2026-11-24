Shader "RHC7/Natural Bone Dirt"
{
    Properties
    {
        _BoneAlbedo ("Bone Albedo", 2D) = "white" {}
        _BoneNormal ("Bone Normal", 2D) = "bump" {}
        _BoneAO ("Bone AO", 2D) = "white" {}
        _DirtVolume ("Dirt Volume (R dust, G clay, B crust, A wear)", 3D) = "white" {}
        _VolumeMin ("Volume Min", Vector) = (-0.2, 0, -0.2, 0)
        _VolumeSize ("Volume Size", Vector) = (0.4, 0.35, 0.4, 0)
        _DustColor ("Dust Color", Color) = (0.62, 0.54, 0.40, 1)
        _ClayColor ("Clay Color", Color) = (0.42, 0.24, 0.13, 1)
        _CrustColor ("Crust Color", Color) = (0.50, 0.47, 0.40, 1)
        _WearColor ("Wear Color", Color) = (0.33, 0.20, 0.11, 1)
        _BoneSmoothness ("Bone Smoothness", Range(0, 1)) = 0.34
        _DirtSmoothness ("Dirt Smoothness", Range(0, 1)) = 0.05
        _DirtNormalStrength ("Dirt Normal Strength", Range(0, 4)) = 2.0
        _DetailScale ("Dirt Detail Scale", Float) = 190
        _DustOpacity ("Dust Film Opacity", Range(0, 1)) = 0.62
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
            #pragma target 3.5
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

            TEXTURE2D(_BoneAlbedo); SAMPLER(sampler_BoneAlbedo);
            TEXTURE2D(_BoneNormal); SAMPLER(sampler_BoneNormal);
            TEXTURE2D(_BoneAO); SAMPLER(sampler_BoneAO);
            TEXTURE3D(_DirtVolume); SAMPLER(sampler_DirtVolume);

            CBUFFER_START(UnityPerMaterial)
                float4 _BoneAlbedo_ST;
                float4 _BoneNormal_ST;
                float4 _BoneAO_ST;
                float4 _VolumeMin;
                float4 _VolumeSize;
                half4 _DustColor;
                half4 _ClayColor;
                half4 _CrustColor;
                half4 _WearColor;
                half _BoneSmoothness;
                half _DirtSmoothness;
                half _DirtNormalStrength;
                half _DetailScale;
                half _DustOpacity;
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

            float HashCell(int x, int y, int z)
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)z * 1274126177u + (uint)1337 * 1442695041u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) / 16777215.0;
            }

            float ValueNoise3(float x, float y, float z)
            {
                int ix = (int)floor(x);
                int iy = (int)floor(y);
                int iz = (int)floor(z);
                float fx = x - ix;
                float fy = y - iy;
                float fz = z - iz;
                fx = fx * fx * (3.0 - 2.0 * fx);
                fy = fy * fy * (3.0 - 2.0 * fy);
                fz = fz * fz * (3.0 - 2.0 * fz);

                float n000 = HashCell(ix, iy, iz);
                float n100 = HashCell(ix + 1, iy, iz);
                float n010 = HashCell(ix, iy + 1, iz);
                float n110 = HashCell(ix + 1, iy + 1, iz);
                float n001 = HashCell(ix, iy, iz + 1);
                float n101 = HashCell(ix + 1, iy, iz + 1);
                float n011 = HashCell(ix, iy + 1, iz + 1);
                float n111 = HashCell(ix + 1, iy + 1, iz + 1);

                float nx00 = lerp(n000, n100, fx);
                float nx10 = lerp(n010, n110, fx);
                float nx01 = lerp(n001, n101, fx);
                float nx11 = lerp(n011, n111, fx);
                return lerp(lerp(nx00, nx10, fy), lerp(nx01, nx11, fy), fz);
            }

            float Fbm3(float3 p)
            {
                float sum = 0.0;
                float amp = 0.5;
                for (int k = 0; k < 3; k++)
                {
                    sum += ValueNoise3(p.x, p.y, p.z) * amp;
                    p *= 2.07;
                    amp *= 0.5;
                }
                return sum;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float3 positionOS : TEXCOORD5;
                float3 normalOS : TEXCOORD6;
                half fogCoord : TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = normalInputs.tangentWS;
                output.bitangentWS = normalInputs.bitangentWS;
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                output.uv = input.uv;
                output.fogCoord = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 uv = input.uv;
                float3 volumeUVW = (input.positionOS - _VolumeMin.xyz) / max(_VolumeSize.xyz, 1e-5);
                half4 mask = SAMPLE_TEXTURE3D(_DirtVolume, sampler_DirtVolume, saturate(volumeUVW));
                half wear = mask.a;

                float n1 = Fbm(uv * 11.0 + 3.7);
                float n2 = Fbm(uv * 37.0 + 11.2);
                float n3 = Fbm(uv * 96.0 + 23.1);
                float n4 = Fbm(uv * 230.0 + 5.5);
                float g1 = Fbm3(input.positionOS * 120.0 + 17.0);

                // The natural sediment pattern is evaluated in object space so
                // it stays continuous across the fragmented scan UV atlas.
                float3 pOS = input.positionOS;
                float3 nOS = normalize(input.normalOS);
                float up = saturate(nOS.y * 0.5 + 0.5);
                float underside = smoothstep(-0.55, -0.05, nOS.y);

                float broad = Fbm3(pOS * 5.2 + float3(13.7, 41.2, 7.9));
                float patch = Fbm3(pOS * 8.7 + float3(61.4, 10.5, 33.1));
                float mid = Fbm3(pOS * 17.0 + float3(27.9, 73.6, 51.2));
                float fine = Fbm3(pOS * 41.0 + float3(88.1, 19.4, 67.3));
                float micro = Fbm3(pOS * 105.0 + float3(5.2, 90.7, 12.8));
                float clayNoise = Fbm3(pOS * 12.5 + float3(44.6, 36.9, 81.4));
                float dustNoise = Fbm3(pOS * 6.8 + float3(70.3, 24.8, 58.6));

                float bare = smoothstep(0.595, 0.735, broad);
                float keep = 1.0 - bare * 0.94;

                float crustPatch = smoothstep(0.520, 0.680, patch);
                crustPatch *= 0.55 + 0.55 * up;
                float crustPat = saturate(crustPatch * (0.72 + 0.38 * mid));
                crustPat = saturate(crustPat * (0.82 + 0.25 * fine));

                float clayPatch = smoothstep(0.485, 0.660, clayNoise);
                float clayPat = saturate(clayPatch * (0.66 + 0.34 * mid));
                clayPat = saturate(clayPat + crustPat * 0.18);

                float dustPat = 0.45 + 0.55 * mid;
                dustPat *= 0.70 + 0.30 * fine;
                dustPat *= lerp(1.0, 0.42, crustPat);
                dustPat *= lerp(1.0, 0.72, clayPat);
                dustPat *= 0.82 + 0.24 * micro;
                float dustDrift = smoothstep(0.40, 0.60, dustNoise);
                dustPat *= 0.30 + 0.70 * dustDrift;

                dustPat *= keep * underside;
                clayPat *= keep * underside;
                crustPat *= keep * underside;

                if (bare > 0.86)
                {
                    dustPat *= 0.10;
                    clayPat *= 0.08;
                    crustPat *= 0.08;
                }

                float fleck = smoothstep(0.74, 0.86, fine) * 0.35;
                dustPat *= 1.0 - fleck;

                half3 boneAlbedo = SAMPLE_TEXTURE2D(_BoneAlbedo, sampler_BoneAlbedo, TRANSFORM_TEX(uv, _BoneAlbedo)).rgb;
                half ao = SAMPLE_TEXTURE2D(_BoneAO, sampler_BoneAO, TRANSFORM_TEX(uv, _BoneAO)).r;
                half3 boneNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(_BoneNormal, sampler_BoneNormal, TRANSFORM_TEX(uv, _BoneNormal)), 1.0);

                // Wear is damage to the bone itself; it stays under the dirt.
                half wearMask = smoothstep(0.04, 0.80, wear);
                half3 boneCol = lerp(boneAlbedo, _WearColor.rgb * (0.62 + 0.50 * n3), wearMask * 0.72);
                half boneSmoothness = lerp(_BoneSmoothness, 0.15, wearMask * 0.8);

                // Layer colours, broken up with multi-scale noise and AO grime.
                half3 dustCol = _DustColor.rgb * (0.78 + 0.42 * n2) * (0.88 + 0.24 * n4);
                half3 clayCol = _ClayColor.rgb * (0.72 + 0.52 * n1) * (0.86 + 0.28 * n3);
                half3 crustCol = _CrustColor.rgb * (0.80 + 0.36 * n2) * (0.88 + 0.24 * g1);
                half grime = lerp(0.58, 1.0, ao);
                clayCol *= grime;
                crustCol *= grime;

                half dustA = saturate(dustPat * mask.r) * _DustOpacity;
                half clayA = saturate(clayPat * mask.g);
                half crustA = saturate(crustPat * mask.b);

                // Sediment stack: bone -> clay -> crust -> loose dust film.
                half3 albedo = boneCol;
                albedo = lerp(albedo, clayCol, clayA);
                albedo = lerp(albedo, crustCol, crustA);
                albedo = lerp(albedo, dustCol, dustA);

                half coverage = 1.0 - (1.0 - clayA) * (1.0 - crustA) * (1.0 - dustA);
                half smoothness = lerp(boneSmoothness, _DirtSmoothness, coverage);

                // Grit / sediment speckles, only on dirt.
                half grit = smoothstep(0.78, 0.94, n4) * coverage;
                albedo = lerp(albedo, _CrustColor.rgb * 1.30, grit * 0.30);

                // Procedural dirt micro-relief blended into the scanned bone normal.
                float e = 0.0025;
                float h0 = Fbm(uv * _DetailScale);
                float hx = Fbm((uv + float2(e, 0.0)) * _DetailScale);
                float hy = Fbm((uv + float2(0.0, e)) * _DetailScale);
                half2 slope = half2((hx - h0) / e, (hy - h0) / e) * 0.010 * _DirtNormalStrength * coverage;
                half3 normalTS = normalize(half3(boneNormal.xy + slope, boneNormal.z));
                float3 normalWS = TransformTangentToWorld(normalTS, half3x3(normalize(input.tangentWS), normalize(input.bitangentWS), normalize(input.normalWS)));

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalize(normalWS);
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
                surfaceData.occlusion = ao;
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

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings depthVert(DepthAttributes input)
            {
                DepthVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 depthFrag(DepthVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
