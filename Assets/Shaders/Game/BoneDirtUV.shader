Shader "RHC7/Bone Dirt UV"
{
    Properties
    {
        _BoneAlbedo ("Bone Albedo", 2D) = "white" {}
        _BoneNormal ("Bone Normal", 2D) = "bump" {}
        _BoneAO ("Bone AO", 2D) = "white" {}
        _DirtMask ("Dirt Mask (R dust, G clay, B crust, A wear)", 2D) = "white" {}
        _DirtNormal ("Dirt Normal (object space, UV1)", 2D) = "bump" {}
        _CleanBoneColor ("Clean Bone Color", Color) = (0.86, 0.80, 0.68, 1)
        _CleanBoneBlend ("Clean Bone Blend", Range(0, 1)) = 0.90
        _DustColor ("Dust Color", Color) = (0.35, 0.34, 0.30, 1)
        _ClayColor ("Clay Color", Color) = (0.22, 0.13, 0.07, 1)
        _CrustColor ("Crust Color", Color) = (0.11, 0.105, 0.09, 1)
        _WearColor ("Wear Color", Color) = (0.28, 0.17, 0.09, 1)
        _CrackColor ("Crack Color", Color) = (0.03, 0.018, 0.01, 1)
        _FleckColor ("Mineral Fleck Color", Color) = (0.55, 0.50, 0.40, 1)
        _BoneSmoothness ("Bone Smoothness", Range(0, 1)) = 0.34
        _DustSmoothness ("Dust Smoothness", Range(0, 1)) = 0.04
        _ClaySmoothness ("Clay Smoothness", Range(0, 1)) = 0.15
        _CrustSmoothness ("Crust Smoothness", Range(0, 1)) = 0.08
        _DirtNormalStrength ("Dirt Normal Strength", Range(0, 5)) = 1.6
        _ProceduralDirtBump ("Procedural Dirt Bump", Range(0, 1)) = 0
        _DetailScale ("Dirt Detail Scale", Float) = 220
        _DustOpacity ("Dust Film Opacity", Range(0, 1)) = 0.55
        _DebugMode ("Debug Mode", Float) = 0
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

            TEXTURE2D(_BoneAlbedo); SAMPLER(sampler_BoneAlbedo);
            TEXTURE2D(_BoneNormal); SAMPLER(sampler_BoneNormal);
            TEXTURE2D(_BoneAO); SAMPLER(sampler_BoneAO);
            TEXTURE2D(_DirtMask); SAMPLER(sampler_DirtMask);
            TEXTURE2D(_DirtNormal); SAMPLER(sampler_DirtNormal);

            CBUFFER_START(UnityPerMaterial)
                float4 _BoneAlbedo_ST;
                float4 _BoneNormal_ST;
                float4 _BoneAO_ST;
                float4 _DirtMask_ST;
                float4 _DirtNormal_ST;
                half4 _CleanBoneColor;
                half _CleanBoneBlend;
                half4 _DustColor;
                half4 _ClayColor;
                half4 _CrustColor;
                half4 _WearColor;
                half4 _CrackColor;
                half4 _FleckColor;
                half _BoneSmoothness;
                half _DustSmoothness;
                half _ClaySmoothness;
                half _CrustSmoothness;
                half _DirtNormalStrength;
                half _ProceduralDirtBump;
                half _DetailScale;
                half _DustOpacity;
                half _DebugMode;
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

            float2 Hash22(float2 p)
            {
                return float2(Hash21(p), Hash21(p + 19.19));
            }

            float VoronoiEdge(float2 p)
            {
                float2 ip = floor(p);
                float2 fp = frac(p);
                float f1 = 8.0;
                float f2 = 8.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 g = float2(x, y);
                        float2 o = Hash22(ip + g);
                        float2 r = g + o - fp;
                        float d = dot(r, r);
                        if (d < f1)
                        {
                            f2 = f1;
                            f1 = d;
                        }
                        else if (d < f2)
                        {
                            f2 = d;
                        }
                    }
                }
                return 1.0 - smoothstep(0.004, 0.05, sqrt(f2) - sqrt(f1));
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float3 tangentWS : TEXCOORD4;
                float3 bitangentWS : TEXCOORD5;
                float3 positionOS : TEXCOORD6;
                float3 normalOS : TEXCOORD7;
                half4 color : TEXCOORD8;
                half fogCoord : TEXCOORD9;
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
                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                output.color = input.color;
                output.fogCoord = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 mask = input.color;
                if (_DebugMode > 1.5 && _DebugMode < 2.5) return half4(input.uv1.x, input.uv1.y, 0.0, 1.0);
                if (_DebugMode > 0.5 && _DebugMode < 1.5) return half4(mask.rgb, 1.0);
                // Wear channel is not used in the vertex-color workflow: all three
                // channels are dirt layers, alpha stays 0.
                half wear = 0.0;

                // This project uses untextured scanned meshes. Keep the palette
                // and vertex-color dirt layers, but do not depend on optional
                // albedo/normal/AO textures.
                half3 boneAlbedo = half3(1.0, 1.0, 1.0);
                half ao = 1.0;
                half3 boneNormal = half3(0.0, 0.0, 1.0);

                float n1 = Fbm(input.uv0 * 11.0 + 3.7);
                float n2 = Fbm(input.uv0 * 37.0 + 11.2);
                float n3 = Fbm(input.uv0 * 96.0 + 23.1);
                float n4 = Fbm(input.uv0 * 230.0 + 5.5);

                float3 pOS = input.positionOS;
                float3 nOS = normalize(input.normalOS);
                float up = saturate(nOS.y * 0.5 + 0.5);
                float underside = smoothstep(-0.55, -0.05, nOS.y);
                float cavity = 1.0 - ao;

                float3 an = abs(nOS);
                float2 crackUV = (an.y >= an.x && an.y >= an.z)
                    ? pOS.xz
                    : (an.x >= an.z ? pOS.yz : pOS.xy);
                float crackClay = VoronoiEdge(crackUV * 12000.0 + float2(3.1, 7.4));
                float crackCrust = VoronoiEdge(crackUV * 19000.0 + float2(11.7, 2.3));

                float lump = smoothstep(0.54, 0.76, Fbm3(pOS * 6000.0 + float3(5.1, 91.2, 33.4)));
                float fine = Fbm3(pOS * 20000.0 + float3(88.1, 19.4, 67.3));
                float g1 = Fbm3(pOS * 45000.0 + float3(17.0, 41.0, 9.0));

                // Light prepared bone under the dirt. Keep it nearly uniform:
                // the dark fossil texture must not read as dirt speckles.
                half boneLum = dot(boneAlbedo, half3(0.299, 0.587, 0.114));
                half3 cleanBone = _CleanBoneColor.rgb * (0.86 + 0.18 * saturate(boneLum * 1.4));
                cleanBone = lerp(cleanBone, boneAlbedo * 1.25, 0.06);
                half3 boneBase = lerp(boneAlbedo, cleanBone, _CleanBoneBlend);

                half wearMask = smoothstep(0.04, 0.80, wear);
                half3 boneCol = lerp(boneBase, _WearColor.rgb * (0.62 + 0.50 * n3), wearMask * 0.72);
                half boneSmoothness = lerp(_BoneSmoothness, 0.15, wearMask * 0.8);

                // Vertex colors carry physical layer weights: R dust (top),
                // G clay (middle), B hard matrix (bottom). Do not amplify the
                // mask: a faint weight must stay a faint film, not full paint.
                half dustA = smoothstep(0.03, 0.30, mask.r) * _DustOpacity;
                half clayA = smoothstep(0.04, 0.35, mask.g);
                half crustA = smoothstep(0.04, 0.40, mask.b);

                half3 dustCol = _DustColor.rgb * (0.80 + 0.30 * n2) * (0.90 + 0.16 * n4);
                half3 clayCol = _ClayColor.rgb * (0.68 + 0.52 * n1) * (0.82 + 0.30 * n3);
                half3 crustCol = _CrustColor.rgb * (0.74 + 0.42 * n2) * (0.86 + 0.24 * g1);

                // Coarse granular relief in the earth itself.
                half grain = Fbm3(pOS * 12000.0 + float3(3.7, 8.1, 5.3));
                dustCol *= 0.86 + 0.24 * grain;
                clayCol *= 0.82 + 0.32 * grain;
                crustCol *= 0.82 + 0.32 * grain;

                // Mild mottling from the scan; the dark fossil albedo must not
                // tint the dirt into a uniform brown coat.
                half dirtDetail = 0.82 + 0.24 * saturate(boneLum * 2.2);
                dustCol *= dirtDetail;
                clayCol *= dirtDetail;
                crustCol *= dirtDetail;
                dustCol *= 0.92 + 0.10 * up;

                half grime = lerp(0.70, 1.0, ao);
                dustCol *= lerp(0.88, 1.0, ao);
                clayCol *= grime;
                crustCol *= grime;

                clayCol = lerp(clayCol, _CrackColor.rgb, saturate(crackClay * clayA) * 0.55);
                crustCol = lerp(crustCol, _CrackColor.rgb, saturate(crackCrust * crustA) * 0.45);
                clayCol = lerp(clayCol, _ClayColor.rgb * 1.18, lump * 0.22);
                crustCol = lerp(crustCol, _CrustColor.rgb * 1.18, lump * 0.25);
                half fleckA = smoothstep(0.80, 0.95, g1) * crustA;

                half3 albedo = boneCol;
                albedo = lerp(albedo, crustCol, crustA);
                albedo = lerp(albedo, clayCol, clayA);
                albedo = lerp(albedo, dustCol, dustA);
                albedo = lerp(albedo, _FleckColor.rgb, fleckA * 0.40);

                half coverage = 1.0 - (1.0 - clayA) * (1.0 - crustA) * (1.0 - dustA);

                // Darken the rim of each earth patch: fakes the thickness lip.
                half edge = saturate(length(fwidth(mask.rgb)) * 3.0);
                albedo *= 1.0 - 0.16 * edge * coverage;

                if (_DebugMode > 3.5) return half4(albedo, 1.0);
                if (_DebugMode > 2.5 && _DebugMode < 3.5) return half4(coverage, coverage, coverage, 1.0);
                half dirtSmoothness = (dustA * _DustSmoothness + clayA * _ClaySmoothness + crustA * _CrustSmoothness)
                                      / max(0.0001, dustA + clayA + crustA);
                half smoothness = lerp(boneSmoothness, dirtSmoothness, coverage);

                // Baked object-space normal of the dirt crust; on clean bone use
                // the scanned 4K tangent-space normal map.
                half3 dirtNWS = normalize(input.normalWS);
                half3 boneNWS = normalize(input.normalWS);
                float3 normalWS = normalize(lerp(boneNWS, dirtNWS, saturate(coverage * _DirtNormalStrength)));

                // Meshes without a usable dirt-normal UV can still show real
                // granular relief: derive a bump normal from the same 3D noise
                // that drives the earth colour. World-space derivatives keep
                // it independent of UVs and tangents.
                float3 nWS = normalize(normalWS);
                float3 dpx = ddx(input.positionWS);
                float3 dpy = ddy(input.positionWS);
                float3 r1 = cross(dpy, nWS);
                float3 r2 = cross(nWS, dpx);
                float det = dot(dpx, r1);
                float dirtHeight = grain * 0.65 + lump * 0.35;
                float3 bumpGrad = sign(det) * (ddx(dirtHeight) * r1 + ddy(dirtHeight) * r2);
                float3 bumpNWS = normalize(abs(det) * nWS - bumpGrad * 0.08 * _ProceduralDirtBump);
                normalWS = normalize(lerp(nWS, bumpNWS, saturate(coverage * _ProceduralDirtBump)));

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
                surfaceData.specular = half3(0.015, 0.015, 0.015);
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
