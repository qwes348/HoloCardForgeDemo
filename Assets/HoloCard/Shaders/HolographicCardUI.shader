Shader "Holo/Holographic Card (UI)"
{
    Properties
    {
        [Header(Card Textures)][Space(4)]
        [PerRendererData] _MainTex ("Sprite Texture (Image 가 채운다)", 2D) = "white" {}
        _BaseMap        ("Base Art (비우면 Sprite 사용)", 2D) = "white" {}
        [NoScaleOffset] _DepthMap ("Depth  (white = 앞으로 튀어나옴)", 2D) = "black" {}
        [NoScaleOffset] _FoilMask ("Foil Mask (R)", 2D) = "white" {}
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

        [Toggle] _GammaBlend ("Composite in sRGB (poke-holo 동일)", Float) = 1

        [Header(View Source)][Space(4)]
        // UI 는 캔버스 모드에 따라 카메라 시선이 의미 없을 수 있어 기본값이 1(컨트롤러).
        _ViewBlend      ("Camera 0 - 1 Controller", Range(0, 1)) = 1
        [HideInInspector] _VirtualView   ("Virtual View", Vector) = (0,0,1,0)
        [HideInInspector] _PointerUV     ("Pointer UV", Vector)   = (0.5,0.5,0,0)
        [HideInInspector] _Tilt          ("Tilt", Float)          = 0
        [HideInInspector] _EnvIntensity  ("Env", Float)           = 0
        [HideInInspector] _EnvRoughness  ("Env Blur", Float)      = 0.15

        [Header(uGUI)][Space(4)]
        [HideInInspector] _StencilComp      ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil          ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp        ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask  ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask        ("Color Mask", Float) = 15
        [HideInInspector] _UseUIAlphaClip   ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline"    = "UniversalPipeline"
        }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull      Off
        Lighting  Off
        ZWrite    Off
        ZTest     [unity_GUIZTestMode]
        Blend     SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "HoloCardUI"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   HoloUIVert
            #pragma fragment HoloUIFrag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DepthMap);  SAMPLER(sampler_DepthMap);
            TEXTURE2D(_FoilMask);  SAMPLER(sampler_FoilMask);

            #include "HoloCardCore.hlsl"

            // uGUI 가 채우는 값들. UI 는 SRP Batcher 경로를 타지 않으므로
            // UnityPerMaterial 밖에 두어도 문제없다.
            float4 _ClipRect;

            float HoloUIClip(float2 position, float4 clipRect)
            {
                float2 inside = step(clipRect.xy, position.xy) * step(position.xy, clipRect.zw);
                return inside.x * inside.y;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings HoloUIVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color      = input.color * _BaseColor;
                return output;
            }

            half4 HoloUIFrag(Varyings input) : SV_Target
            {
                // 캔버스 쿼드의 탄젠트 프레임. RectTransform 은 XY 평면이므로
                // 오브젝트 스페이스 축을 그대로 월드로 옮기면 된다.
                float3 tangentWS   = normalize(TransformObjectToWorldDir(float3(1, 0, 0)));
                float3 bitangentWS = normalize(TransformObjectToWorldDir(float3(0, 1, 0)));
                float3 normalWS    = normalize(cross(tangentWS, bitangentWS));

                // 뒤에서 봐도 앞면 취급 (UI 는 Cull Off)
                float3 vWS  = normalize(GetWorldSpaceViewDir(input.positionWS));
                float  face = dot(normalWS, vWS) < 0 ? -1.0 : 1.0;
                normalWS    *= face;
                bitangentWS *= face;

                float3 viewTS = HoloViewTS(input.positionWS, normalWS, tangentWS, bitangentWS);

                float4 col = HoloCardShade(TEXTURE2D_ARGS(_BaseMap,  sampler_BaseMap),
                                           TEXTURE2D_ARGS(_DepthMap, sampler_DepthMap),
                                           TEXTURE2D_ARGS(_FoilMask, sampler_FoilMask),
                                           input.uv, viewTS, _Tilt, float3(0, 0, 0));

                col *= input.color;

            #ifdef UNITY_UI_CLIP_RECT
                col.a *= HoloUIClip(input.positionWS.xy, _ClipRect);
            #endif

            #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
            #endif

                return half4(col);
            }
            ENDHLSL
        }
    }

    FallBack "UI/Default"
    CustomEditor "HoloCard.Editor.HoloCardMaterialEditor"
}
