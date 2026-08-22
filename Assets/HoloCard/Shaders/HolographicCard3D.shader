Shader "Holo/Holographic Card (3D)"
{
    Properties
    {
        [Header(Card Textures)][Space(4)]
        _BaseMap        ("Base Art", 2D) = "white" {}
        [NoScaleOffset] _DepthMap  ("Depth  (white = 앞으로 튀어나옴)", 2D) = "black" {}
        [NoScaleOffset] _FoilMask  ("Foil Mask (R)", 2D) = "white" {}
        _BaseColor      ("Tint", Color) = (1,1,1,1)

        [Header(01 Parallax)][Space(4)]
        _ParallaxDepth  ("Depth", Range(0, 0.3))    = 0.075
        _ParallaxSteps  ("Ray Steps", Range(4, 64)) = 32
        _ParallaxChroma ("Chromatic Aberration", Range(0, 0.4)) = 0.09
        _DepthShade     ("Depth Shading", Range(0, 1)) = 0.38

        [Header(03 Holographic Foil)][Space(4)]
        _HoloIntensity  ("Intensity", Range(0, 2.5))   = 0.62
        _HoloScale      ("Grating Density", Range(1, 40)) = 9
        _HoloAngle      ("Grating Angle", Range(0, 180)) = 28
        _HoloSpread     ("View Response", Range(0, 8))  = 2.6
        _HoloContrast   ("Contrast", Range(0.3, 3.5))   = 1.7
        _HoloBlend      ("Add to Dodge", Range(0, 1))   = 0.42
        _HoloSpeed      ("Drift Speed", Range(0, 1))    = 0.05
        _HoloGrazing    ("Grazing Boost", Range(0, 1))  = 0.8

        [Header(04 Sparkle)][Space(4)]
        _SparkleIntensity ("Glitter", Range(0, 2.5))      = 0.6
        _SparkleDensity   ("Glitter Density", Range(20, 320)) = 130
        _SparklePower     ("Glitter Tightness", Range(4, 120)) = 45
        _SparkleDepth     ("Glitter Parallax", Range(0, 1)) = 0.35

        [Header(05 Glare and Sheen)][Space(4)]
        _GlareIntensity ("Glare", Range(0, 1.5))    = 0.32
        _GlareSize      ("Glare Size", Range(0.1, 1.6)) = 0.7
        _GlarePower     ("Glare Falloff", Range(0.5, 6)) = 2.2
        _SheenIntensity ("Sheet Reflection", Range(0, 0.8)) = 0.08

        [Header(06 Bevel and Rim)][Space(4)]
        _RimIntensity   ("Rim Light", Range(0, 1.2)) = 0.25
        _RimPower       ("Rim Falloff", Range(0.5, 8)) = 2.5
        _RimColor       ("Rim Color", Color) = (0.6, 0.8, 1.0, 1)
        _Bevel          ("Bevel", Range(0, 1))      = 0.18
        _BevelWidth     ("Bevel Width", Range(0.001, 0.2)) = 0.035

        [Header(Environment)][Space(4)]
        _EnvIntensity   ("Reflection Probe", Range(0, 2)) = 0
        _EnvRoughness   ("Reflection Blur", Range(0, 1))  = 0.15
        [Toggle] _GammaBlend ("Composite in sRGB (poke-holo 동일)", Float) = 1

        [Header(View Source)][Space(4)]
        _ViewBlend      ("Camera 0 - 1 Controller", Range(0, 1)) = 0.35
        [HideInInspector] _VirtualView ("Virtual View", Vector) = (0,0,1,0)
        [HideInInspector] _PointerUV   ("Pointer UV", Vector)   = (0.5,0.5,0,0)
        [HideInInspector] _Tilt        ("Tilt", Float)          = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }
        LOD 300

        Pass
        {
            Name "HoloCardForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   HoloVert
            #pragma fragment HoloFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DepthMap);  SAMPLER(sampler_DepthMap);
            TEXTURE2D(_FoilMask);  SAMPLER(sampler_FoilMask);

            #include "HoloCardCore.hlsl"

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
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 tangentWS   : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float  fogCoord    : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings HoloVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   nrm = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS  = pos.positionCS;
                output.positionWS  = pos.positionWS;
                output.normalWS    = nrm.normalWS;
                output.tangentWS   = nrm.tangentWS;
                output.bitangentWS = nrm.bitangentWS;
                output.uv          = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogCoord    = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 HoloFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 normalWS    = normalize(input.normalWS);
                float3 tangentWS   = normalize(input.tangentWS);
                float3 bitangentWS = normalize(input.bitangentWS);

                float3 viewTS = HoloViewTS(input.positionWS, normalWS, tangentWS, bitangentWS);

                // 리플렉션 프로브. _EnvIntensity 0 이면 결과에 기여하지 않는다.
                float3 viewWS    = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 reflectWS = reflect(-viewWS, normalWS);
                float  mip       = PerceptualRoughnessToMipmapLevel(saturate(_EnvRoughness));
                float4 encoded   = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectWS, mip);
                float3 envCol    = DecodeHDREnvironment(encoded, unity_SpecCube0_HDR);

                float4 col = HoloCardShade(TEXTURE2D_ARGS(_BaseMap,  sampler_BaseMap),
                                           TEXTURE2D_ARGS(_DepthMap, sampler_DepthMap),
                                           TEXTURE2D_ARGS(_FoilMask, sampler_FoilMask),
                                           input.uv, viewTS, _Tilt, envCol);

                col.rgb *= _BaseColor.rgb;
                col.rgb  = MixFog(col.rgb, input.fogCoord);
                return half4(col.rgb, 1.0);
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
            #pragma target 3.5
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
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
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag
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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(DepthVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
    CustomEditor "HoloCard.Editor.HoloCardMaterialEditor"
}
