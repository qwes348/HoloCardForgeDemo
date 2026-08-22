#ifndef HOLO_CARD_CORE_INCLUDED
#define HOLO_CARD_CORE_INCLUDED

// ---------------------------------------------------------------------------
// Holo Card Forge - shared core
//
// poke-holo.simey.me 의 홀로 포일을 URP 로 옮기고, 그 위에 진짜 레이마칭
// 패럴랙스(POM)를 얹은 여섯 레이어. 3D 셰이더와 UI 셰이더가 이 파일을 공유한다.
//
//   01 Parallax Occlusion Mapping   높이맵 레이마칭
//   02 색수차 레이어 분리            R/G/B 를 서로 다른 깊이에서 샘플
//   03 회절 무지개                   각도가 다른 두 격자의 간섭
//   04 마이크로 패싯 글리터          해시 기반 미세 반사면
//   05 글레어 & 시트 반사            포인터 하이라이트 + 가우시안 밴드
//   06 베벨 & 프레넬 림              테두리 두께감
//
// 수식은 아티팩트(Holo Card Forge)의 GLSL 프리뷰와 1:1 로 맞춰져 있다.
// 프리뷰 슬라이더 값을 그대로 머티리얼에 넣으면 같은 그림이 나온다.
// ---------------------------------------------------------------------------

// SRGBToLinear / LinearToSRGB. URP Core.hlsl 만 include 한 셰이더에는 안 딸려온다.
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

#define HOLO_TAU        6.28318530718
#define HOLO_MAX_STEPS  64

// 두 셰이더가 공유하는 머티리얼 상수. CBUFFER 는 셰이더당 한 번만 열려야 하므로
// 여기서 통째로 선언하고, 각 .shader 는 텍스처만 자기 쪽에서 선언한 뒤 이 파일을
// include 한다. (SRP Batcher 호환)
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseColor;

    float  _ParallaxDepth;
    float  _ParallaxSteps;
    float  _ParallaxChroma;
    float  _DepthShade;

    float  _HoloIntensity;
    float  _HoloScale;
    float  _HoloAngle;
    float  _HoloSpread;
    float  _HoloContrast;
    float  _HoloBlend;
    float  _HoloSpeed;
    float  _HoloGrazing;

    float  _GlareIntensity;
    float  _GlareSize;
    float  _GlarePower;
    float  _SheenIntensity;

    float  _SparkleIntensity;
    float  _SparkleDensity;
    float  _SparklePower;
    float  _SparkleDepth;

    float  _RimIntensity;
    float  _RimPower;
    float4 _RimColor;
    float  _Bevel;
    float  _BevelWidth;

    float4 _PointerUV;
    float  _Tilt;
    float4 _VirtualView;
    float  _ViewBlend;

    float  _EnvIntensity;
    float  _EnvRoughness;
    float  _GammaBlend;
CBUFFER_END

// ---------------------------------------------------------------------------
// helpers
// ---------------------------------------------------------------------------

float2 HoloRot2(float2 p, float a)
{
    float s = sin(a);
    float c = cos(a);
    return float2(p.x * c - p.y * s, p.x * s + p.y * c);
}

float3 HoloHash23(float2 p)
{
    float3 q = float3(dot(p, float2(127.1, 311.7)),
                      dot(p, float2(269.5, 183.3)),
                      dot(p, float2(419.2, 371.9)));
    return frac(sin(q) * 43758.5453);
}

// IQ 코사인 팔레트. HSV 변환과 달리 밴딩이 없다.
float3 HoloSpectrum(float t)
{
    return 0.5 + 0.5 * cos(HOLO_TAU * (t + float3(0.0, 0.33, 0.67)));
}

// UV 가장자리까지의 거리를 0~1 로. 1 = 안쪽, 0 = 테두리.
float HoloEdgeDist(float2 uv, float w)
{
    float2 e = min(uv, 1.0 - uv);
    return saturate(min(e.x, e.y) / max(w, 1e-4));
}

// ---------------------------------------------------------------------------
// 01 Parallax Occlusion Mapping
// 시선 방향으로 높이맵을 레이마칭해 교차점을 찾는다. 단순 UV 오프셋과 달리
// 앞쪽 레이어가 뒤를 실제로 가려서 카드 안쪽이 파인 것처럼 보인다.
// 높이맵 규약: 흰색 = 앞으로 튀어나옴.
// ---------------------------------------------------------------------------
float2 HoloParallaxUV(TEXTURE2D_PARAM(depthTex, depthSampler),
                      float2 uv, float3 v, float depth, float stepsF)
{
    if (depth <= 1e-5)
        return uv;

    int    steps     = (int)stepsF;
    float  vz        = max(abs(v.z), 0.15);      // 그레이징 각에서 오프셋 폭발 방지
    float2 maxOffset = (v.xy / vz) * depth;
    float  layerH    = 1.0 / stepsF;
    float2 duv       = maxOffset * layerH;

    float  curH  = 1.0;
    float2 curUV = uv;
    float  sampH = SAMPLE_TEXTURE2D_LOD(depthTex, depthSampler, curUV, 0).r;

    [loop]
    for (int i = 0; i < HOLO_MAX_STEPS; i++)
    {
        if (i >= steps)    break;
        if (sampH >= curH) break;
        curUV -= duv;
        curH  -= layerH;
        sampH  = SAMPLE_TEXTURE2D_LOD(depthTex, depthSampler, curUV, 0).r;
    }

    // 교차 지점 선형 보간 (soft self-occlusion)
    float2 prevUV = curUV + duv;
    float  after  = sampH - curH;
    float  before = SAMPLE_TEXTURE2D_LOD(depthTex, depthSampler, prevUV, 0).r - curH - layerH;
    float  w      = after / max(after - before, 1e-4);
    return lerp(curUV, prevUV, saturate(w));
}

// ---------------------------------------------------------------------------
// 03 회절 무지개
// 각도가 다른 두 격자를 겹쳐 간섭 무늬를 만들고 시선 벡터로 위상을 민다.
// 무지개가 흐르는 이유가 이것.
// ---------------------------------------------------------------------------
float3 HoloRainbow(float2 uv, float3 v, float t)
{
    float3 n = normalize(v);

    float  rad = radians(_HoloAngle);
    float2 g1  = HoloRot2(uv, rad);
    float2 g2  = HoloRot2(uv, rad + 1.169);     // 두 격자의 각도 차 = 간섭 주기

    float phase = (n.x * 0.5 + n.y * 0.3) * _HoloSpread + t * _HoloSpeed;
    float band1 = g1.x * _HoloScale + phase;
    float band2 = (g2.x * 0.63 + g2.y * 0.31) * _HoloScale - phase * 1.27;

    float3 c1 = HoloSpectrum(band1);
    float3 c2 = HoloSpectrum(band2 + 0.15);

    float  inter = 0.5 + 0.5 * sin(band1 * HOLO_TAU) * sin(band2 * HOLO_TAU);
    float3 col   = (c1 * 0.65 + c2 * 0.55) * lerp(0.55, 1.35, inter);

    col = pow(saturate(col), max(_HoloContrast, 0.05));

    // 비스듬히 볼수록 강해지는 실제 회절 격자의 거동
    float grazing = lerp(1.0, saturate(1.0 - abs(n.z) * 0.85) * 1.6, _HoloGrazing);
    return col * grazing;
}

// ---------------------------------------------------------------------------
// 04 마이크로 패싯 글리터
// 셀마다 해시로 미세 반사면을 만들고 시선과의 내적을 높은 지수로 올린다.
// 특정 각도에서만 개별 입자가 터지는 실물 텍스처 포일의 반짝임.
// ---------------------------------------------------------------------------
float HoloSparkle(float2 uv, float3 v, float t)
{
    // 셀이 픽셀보다 작아지면 개별 반짝임이 아니라 균일한 흰 막으로 뭉개진다.
    // 카드가 화면에서 작아질 때(목록 화면, 멀리 있는 카드) 반드시 일어나는 일이라
    // 화면상 셀 밀도를 재서 한 픽셀에 한 셀을 넘어가기 전에 서서히 죽인다.
    // 이게 없으면 밀도를 올릴수록 카드가 뿌옇게 뜬다.
    float cellsPerPixel = max(fwidth(uv.x), fwidth(uv.y)) * _SparkleDensity;
    float aaFade = 1.0 - smoothstep(0.5, 1.4, cellsPerPixel);
    if (aaFade <= 0.0) return 0.0;

    float3 n  = normalize(v);
    float2 gv = uv * _SparkleDensity;
    float2 id = floor(gv);
    float2 f  = frac(gv) - 0.5;

    float3 h  = HoloHash23(id);
    float3 fn = normalize(float3((h.xy * 2.0 - 1.0) * 0.85, 0.6));

    float spec = pow(saturate(dot(n, fn)), _SparklePower * (0.4 + h.z * 1.2));

    float2 c     = (h.xy - 0.5) * 0.7;
    float  d     = length(f - c);
    float  shape = saturate(1.0 - d * 3.6);
    float  tw    = 0.55 + 0.45 * sin(t * (1.3 + h.z * 2.7) + h.x * 12.0);

    return spec * shape * shape * tw * aaFade;
}

// ---------------------------------------------------------------------------
// 전체 합성
//   uv      : 카드 UV (0..1)
//   viewTS  : 탄젠트 스페이스 시선 벡터. z 가 표면 바깥을 향한다.
//   tilt    : 0..1, 기울어진 정도. 포일 전체를 부스트한다.
//   envCol  : 환경 반사색 (없으면 0)
// ---------------------------------------------------------------------------
float4 HoloCardShade(TEXTURE2D_PARAM(artTex,   artSampler),
                     TEXTURE2D_PARAM(depthTex, depthSampler),
                     TEXTURE2D_PARAM(foilTex,  foilSampler),
                     float2 uv, float3 viewTS, float tilt, float3 envCol)
{
    float3 v = normalize(viewTS);
    float  t = _Time.y;

    // 01 - 패럴랙스
    float2 puv  = HoloParallaxUV(TEXTURE2D_ARGS(depthTex, depthSampler),
                                 uv, v, _ParallaxDepth, _ParallaxSteps);
    float2 pdir = puv - uv;

    // 02 - 색수차. R/G/B 를 서로 다른 깊이에서 뽑아 두꺼운 유리 굴절감을 만든다.
    float4 baseCol;
    if (_ParallaxChroma > 1e-4)
    {
        float  r = SAMPLE_TEXTURE2D(artTex, artSampler, uv + pdir * (1.0 + _ParallaxChroma)).r;
        float4 g = SAMPLE_TEXTURE2D(artTex, artSampler, puv);
        float  b = SAMPLE_TEXTURE2D(artTex, artSampler, uv + pdir * (1.0 - _ParallaxChroma)).b;
        baseCol  = float4(r, g.g, b, g.a);
    }
    else
    {
        baseCol = SAMPLE_TEXTURE2D(artTex, artSampler, puv);
    }

    // poke-holo 의 CSS color-dodge 는 sRGB 공간에서 일어난다. 프로젝트가 Linear
    // 컬러스페이스여도 같은 그림을 얻으려면 여기서부터 감마 공간으로 넘어가고
    // 마지막에 되돌린다. _GammaBlend 0 이면 리니어 그대로 합성한다.
    //
    // 깊이 셰이딩도 반드시 이 안쪽에서 곱해야 한다. 리니어에서 0.62 를 곱하면
    // 감마에서 곱한 것보다 훨씬 밝게 남아서 어두운 배경이 통째로 들린다.
    float3 work = lerp(baseCol.rgb, LinearToSRGB(baseCol.rgb), _GammaBlend);

    // 파인 곳은 어둡게 - 셀프 섀도우 대용
    float dHit     = SAMPLE_TEXTURE2D(depthTex, depthSampler, puv).r;
    float shadeAmt = saturate(_ParallaxDepth * 6.0);
    work *= lerp(1.0, (1.0 - _DepthShade) + _DepthShade * dHit, shadeAmt);

    float foil  = SAMPLE_TEXTURE2D(foilTex, foilSampler, uv).r;
    float boost = 1.0 + tilt * 0.6;

    // 03 - 무지개를 add 와 color-dodge 사이에서 섞는다.
    // poke-holo 의 CSS color-dodge 가 dodge 쪽 극단.
    float3 holo   = HoloRainbow(uv, v, t) * foil * _HoloIntensity * boost;
    float3 addB   = work + holo;
    float3 dodgeB = work / max(1.0 - saturate(holo * 0.6), 0.15);
    float3 col    = lerp(addB, dodgeB, _HoloBlend);

    // 04 - 글리터. 패럴랙스 오프셋을 일부만 먹여 포일이 아트보다 살짝 위에 뜨게.
    col += HoloSparkle(uv + pdir * _SparkleDepth, v, t) * foil * _SparkleIntensity * boost;

    // 05 - 포인터 글레어 + 시트 반사
    float gd    = length(uv - _PointerUV.xy) / max(_GlareSize, 1e-3);
    float glare = pow(saturate(1.0 - gd), max(_GlarePower, 0.1));

    float sheenAxis = dot(uv - 0.5, normalize(v.xy + float2(1e-4, 0.0)));
    float sheen     = exp(-pow((sheenAxis - v.z * 0.15) * 3.2, 2.0)) * _SheenIntensity;

    col += (glare * _GlareIntensity + sheen) * boost;

    // 환경 반사 (선택). 큐브맵을 물린 3D 카드에서만 0 이상이 된다.
    float3 envWork = lerp(envCol, LinearToSRGB(envCol), _GammaBlend);
    col += envWork * _EnvIntensity * lerp(0.35, 1.0, foil);

    // 06 - 베벨 & 프레넬 림
    float edge = HoloEdgeDist(uv, _BevelWidth);
    col += (1.0 - edge) * _Bevel * clamp(0.35 + length(v.xy), 0.0, 1.5);

    float rim = pow(saturate(1.0 - abs(v.z)), max(_RimPower, 0.1)) * _RimIntensity;
    col += _RimColor.rgb * rim * (1.0 - edge * 0.65);

    // 감마 공간에서 합성했다면 리니어로 되돌린다.
    col = lerp(col, SRGBToLinear(col), _GammaBlend);

    return float4(col, baseCol.a);
}

// ---------------------------------------------------------------------------
// 탄젠트 스페이스 시선 벡터.
// _ViewBlend 로 실제 카메라 시선과 컨트롤러가 넣어준 가상 시선을 섞는다.
//   0 = 카메라만 (물리적으로 정확)
//   1 = 컨트롤러만 (poke-holo 웹 프리뷰와 동일한 거동)
// ---------------------------------------------------------------------------
float3 HoloViewTS(float3 positionWS, float3 normalWS, float3 tangentWS, float3 bitangentWS)
{
    float3 vWS  = normalize(GetWorldSpaceViewDir(positionWS));
    float3 real = float3(dot(vWS, tangentWS), dot(vWS, bitangentWS), dot(vWS, normalWS));
    real.z = max(real.z, 0.05);

    float3 virt = _VirtualView.xyz;
    virt.z = max(virt.z, 0.05);

    return normalize(lerp(real, virt, saturate(_ViewBlend)));
}

#endif // HOLO_CARD_CORE_INCLUDED
