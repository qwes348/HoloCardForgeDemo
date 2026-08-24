using UnityEngine;

namespace HoloCard
{
    /// <summary>
    /// 셰이더 프로퍼티 이름 캐시. 매 프레임 Shader.PropertyToID 를 부르지 않기 위해.
    /// </summary>
    public static class HoloCardIDs
    {
        public static readonly int ParallaxDepth    = Shader.PropertyToID("_ParallaxDepth");
        public static readonly int ParallaxSteps    = Shader.PropertyToID("_ParallaxSteps");
        public static readonly int ParallaxChroma   = Shader.PropertyToID("_ParallaxChroma");
        public static readonly int DepthShade       = Shader.PropertyToID("_DepthShade");

        public static readonly int HoloIntensity    = Shader.PropertyToID("_HoloIntensity");
        public static readonly int HoloScale        = Shader.PropertyToID("_HoloScale");
        public static readonly int HoloAngle        = Shader.PropertyToID("_HoloAngle");
        public static readonly int HoloSpread       = Shader.PropertyToID("_HoloSpread");
        public static readonly int HoloContrast     = Shader.PropertyToID("_HoloContrast");
        public static readonly int HoloBlend        = Shader.PropertyToID("_HoloBlend");
        public static readonly int HoloSpeed        = Shader.PropertyToID("_HoloSpeed");
        public static readonly int HoloGrazing      = Shader.PropertyToID("_HoloGrazing");

        public static readonly int GlareIntensity   = Shader.PropertyToID("_GlareIntensity");
        public static readonly int GlareSize        = Shader.PropertyToID("_GlareSize");
        public static readonly int GlarePower       = Shader.PropertyToID("_GlarePower");
        public static readonly int SheenIntensity   = Shader.PropertyToID("_SheenIntensity");

        public static readonly int SparkleIntensity = Shader.PropertyToID("_SparkleIntensity");
        public static readonly int SparkleDensity   = Shader.PropertyToID("_SparkleDensity");
        public static readonly int SparklePower     = Shader.PropertyToID("_SparklePower");
        public static readonly int SparkleDepth     = Shader.PropertyToID("_SparkleDepth");

        public static readonly int RimIntensity     = Shader.PropertyToID("_RimIntensity");
        public static readonly int RimPower         = Shader.PropertyToID("_RimPower");
        public static readonly int Bevel            = Shader.PropertyToID("_Bevel");
        public static readonly int BevelWidth       = Shader.PropertyToID("_BevelWidth");

        // 컨트롤러가 매 프레임 써넣는 값
        public static readonly int PointerUV        = Shader.PropertyToID("_PointerUV");
        public static readonly int Tilt             = Shader.PropertyToID("_Tilt");
        public static readonly int VirtualView      = Shader.PropertyToID("_VirtualView");
        public static readonly int ViewBlend        = Shader.PropertyToID("_ViewBlend");

        // 확대 보기가 다른 카드를 어둡게 할 때 쓴다
        public static readonly int BaseColor        = Shader.PropertyToID("_BaseColor");

        // 텍스처
        public static readonly int BaseMap          = Shader.PropertyToID("_BaseMap");
        public static readonly int DepthMap         = Shader.PropertyToID("_DepthMap");
        public static readonly int FoilMask         = Shader.PropertyToID("_FoilMask");
    }

    /// <summary>
    /// 홀로 카드 룩 프리셋. 아티팩트 프리뷰의 슬라이더 값과 1:1 대응한다.
    /// 머티리얼에 그대로 부어넣거나, 머티리얼에서 값을 되읽을 수 있다.
    /// </summary>
    [CreateAssetMenu(fileName = "HoloCardPreset", menuName = "Holo Card/Preset", order = 0)]
    public class HoloCardPreset : ScriptableObject
    {
        [Header("01 Parallax")]
        [Range(0f, 0.3f)] public float parallaxDepth  = 0.075f;
        [Range(4f, 64f)]  public float parallaxSteps  = 32f;
        [Range(0f, 0.4f)] public float parallaxChroma = 0.09f;
        [Range(0f, 1f)]   public float depthShade     = 0.38f;

        [Header("03 Holographic Foil")]
        [Range(0f, 2.5f)]   public float holoIntensity = 0.62f;
        [Range(1f, 40f)]    public float holoScale     = 9f;
        [Range(0f, 180f)]   public float holoAngle     = 28f;
        [Range(0f, 8f)]     public float holoSpread    = 2.6f;
        [Range(0.3f, 3.5f)] public float holoContrast  = 1.7f;
        [Range(0f, 1f)]     public float holoBlend     = 0.42f;
        [Range(0f, 1f)]     public float holoSpeed     = 0.05f;
        [Range(0f, 1f)]     public float holoGrazing   = 0.8f;

        [Header("04 Sparkle")]
        [Range(0f, 2.5f)]   public float sparkleIntensity = 0.6f;
        [Range(20f, 320f)]  public float sparkleDensity   = 130f;
        [Range(4f, 120f)]   public float sparklePower     = 45f;
        [Range(0f, 1f)]     public float sparkleDepth     = 0.35f;

        [Header("05 Glare and Sheen")]
        [Range(0f, 1.5f)]  public float glareIntensity = 0.32f;
        [Range(0.1f, 1.6f)] public float glareSize     = 0.7f;
        [Range(0.5f, 6f)]  public float glarePower     = 2.2f;
        [Range(0f, 0.8f)]  public float sheenIntensity = 0.08f;

        [Header("06 Bevel and Rim")]
        [Range(0f, 1.2f)]    public float rimIntensity = 0.25f;
        [Range(0.5f, 8f)]    public float rimPower     = 2.5f;
        [Range(0f, 1f)]      public float bevel        = 0.18f;
        [Range(0.001f, 0.2f)] public float bevelWidth  = 0.035f;

        public void ApplyTo(Material m)
        {
            if (m == null) return;

            m.SetFloat(HoloCardIDs.ParallaxDepth,  parallaxDepth);
            m.SetFloat(HoloCardIDs.ParallaxSteps,  parallaxSteps);
            m.SetFloat(HoloCardIDs.ParallaxChroma, parallaxChroma);
            m.SetFloat(HoloCardIDs.DepthShade,     depthShade);

            m.SetFloat(HoloCardIDs.HoloIntensity, holoIntensity);
            m.SetFloat(HoloCardIDs.HoloScale,     holoScale);
            m.SetFloat(HoloCardIDs.HoloAngle,     holoAngle);
            m.SetFloat(HoloCardIDs.HoloSpread,    holoSpread);
            m.SetFloat(HoloCardIDs.HoloContrast,  holoContrast);
            m.SetFloat(HoloCardIDs.HoloBlend,     holoBlend);
            m.SetFloat(HoloCardIDs.HoloSpeed,     holoSpeed);
            m.SetFloat(HoloCardIDs.HoloGrazing,   holoGrazing);

            m.SetFloat(HoloCardIDs.SparkleIntensity, sparkleIntensity);
            m.SetFloat(HoloCardIDs.SparkleDensity,   sparkleDensity);
            m.SetFloat(HoloCardIDs.SparklePower,     sparklePower);
            m.SetFloat(HoloCardIDs.SparkleDepth,     sparkleDepth);

            m.SetFloat(HoloCardIDs.GlareIntensity, glareIntensity);
            m.SetFloat(HoloCardIDs.GlareSize,      glareSize);
            m.SetFloat(HoloCardIDs.GlarePower,     glarePower);
            m.SetFloat(HoloCardIDs.SheenIntensity, sheenIntensity);

            m.SetFloat(HoloCardIDs.RimIntensity, rimIntensity);
            m.SetFloat(HoloCardIDs.RimPower,     rimPower);
            m.SetFloat(HoloCardIDs.Bevel,        bevel);
            m.SetFloat(HoloCardIDs.BevelWidth,   bevelWidth);
        }

        public void CaptureFrom(Material m)
        {
            if (m == null) return;

            parallaxDepth  = m.GetFloat(HoloCardIDs.ParallaxDepth);
            parallaxSteps  = m.GetFloat(HoloCardIDs.ParallaxSteps);
            parallaxChroma = m.GetFloat(HoloCardIDs.ParallaxChroma);
            depthShade     = m.GetFloat(HoloCardIDs.DepthShade);

            holoIntensity = m.GetFloat(HoloCardIDs.HoloIntensity);
            holoScale     = m.GetFloat(HoloCardIDs.HoloScale);
            holoAngle     = m.GetFloat(HoloCardIDs.HoloAngle);
            holoSpread    = m.GetFloat(HoloCardIDs.HoloSpread);
            holoContrast  = m.GetFloat(HoloCardIDs.HoloContrast);
            holoBlend     = m.GetFloat(HoloCardIDs.HoloBlend);
            holoSpeed     = m.GetFloat(HoloCardIDs.HoloSpeed);
            holoGrazing   = m.GetFloat(HoloCardIDs.HoloGrazing);

            sparkleIntensity = m.GetFloat(HoloCardIDs.SparkleIntensity);
            sparkleDensity   = m.GetFloat(HoloCardIDs.SparkleDensity);
            sparklePower     = m.GetFloat(HoloCardIDs.SparklePower);
            sparkleDepth     = m.GetFloat(HoloCardIDs.SparkleDepth);

            glareIntensity = m.GetFloat(HoloCardIDs.GlareIntensity);
            glareSize      = m.GetFloat(HoloCardIDs.GlareSize);
            glarePower     = m.GetFloat(HoloCardIDs.GlarePower);
            sheenIntensity = m.GetFloat(HoloCardIDs.SheenIntensity);

            rimIntensity = m.GetFloat(HoloCardIDs.RimIntensity);
            rimPower     = m.GetFloat(HoloCardIDs.RimPower);
            bevel        = m.GetFloat(HoloCardIDs.Bevel);
            bevelWidth   = m.GetFloat(HoloCardIDs.BevelWidth);
        }

        /// <summary>
        /// 아티팩트에 있던 다섯 프리셋 + VintagePrint. 에디터 메뉴가 이걸로 에셋을 찍어낸다.
        ///
        /// VintagePrint 는 아티팩트에 없던 추가분이다. 앞의 다섯은 어두운 배경의 카드를
        /// 전제로 튜닝돼 있어서, 실제 카드 스캔처럼 밝고 불투명한 아트에 그대로 쓰면
        /// 글레어가 우윳빛 베일이 되어 인쇄면이 다 날아간다.
        /// </summary>
        public enum Builtin
        {
            StandardHolo, RainbowRare, GalaxyFoil, DeepDiorama, MobileLite,
            VintagePrint, FullArtFoil
        }

        public void LoadBuiltin(Builtin kind)
        {
            // 먼저 Standard Holo (= 기본값) 로 되돌린다.
            parallaxDepth = 0.075f; parallaxSteps = 32f; parallaxChroma = 0.09f; depthShade = 0.38f;
            holoIntensity = 0.62f; holoScale = 9f; holoAngle = 28f; holoSpread = 2.6f;
            holoContrast = 1.7f; holoBlend = 0.42f; holoSpeed = 0.05f; holoGrazing = 0.8f;
            sparkleIntensity = 0.6f; sparkleDensity = 130f; sparklePower = 45f; sparkleDepth = 0.35f;
            glareIntensity = 0.32f; glareSize = 0.7f; glarePower = 2.2f; sheenIntensity = 0.08f;
            rimIntensity = 0.25f; rimPower = 2.5f; bevel = 0.18f; bevelWidth = 0.035f;

            switch (kind)
            {
                case Builtin.RainbowRare:
                    holoIntensity = 1.05f; holoScale = 14f; holoSpread = 4.2f;
                    holoContrast = 1.9f; holoBlend = 0.62f;
                    sparkleIntensity = 1.1f; sparkleDensity = 190f;
                    parallaxDepth = 0.075f;
                    break;

                case Builtin.GalaxyFoil:
                    holoIntensity = 0.55f; holoScale = 4.5f; holoAngle = 96f;
                    holoSpread = 1.4f; holoContrast = 2.4f; holoBlend = 0.35f;
                    sparkleIntensity = 1.8f; sparkleDensity = 260f;
                    glareIntensity = 0.5f; rimIntensity = 0.5f;
                    break;

                case Builtin.DeepDiorama:
                    parallaxDepth = 0.20f; parallaxSteps = 56f; parallaxChroma = 0.20f;
                    holoIntensity = 0.32f; sparkleIntensity = 0.4f;
                    glareIntensity = 0.45f; sheenIntensity = 0.2f;
                    break;

                case Builtin.MobileLite:
                    parallaxDepth = 0.065f; parallaxSteps = 16f; parallaxChroma = 0f;
                    sparkleDensity = 80f; sparkleIntensity = 0.45f; holoIntensity = 0.6f;
                    break;

                case Builtin.VintagePrint:
                    // 구형 카드 스캔용. 밝고 불투명한 인쇄면이 살아남도록 가산 레이어를 낮추고,
                    // 대신 포일 마스크가 덮는 아트 창 안에서는 무지개를 더 세게 올린다.
                    //
                    // 이쪽은 액자 안에 그림이 들어 있는 구조라 진짜 디오라마가 성립한다.
                    // 베이커가 프레임·텍스트를 높이 1.0 으로 못 박아 두므로 깊이를 줘도
                    // 인쇄면은 흔들리지 않는다.
                    // 깊이를 올리는 대신 스텝을 올린다. 스텝이 모자라면 교차점을 놓쳐
                    // 계단 경계에서 픽셀이 길게 늘어난다.
                    parallaxDepth = 0.045f; parallaxSteps = 48f; parallaxChroma = 0.035f;
                    holoIntensity = 0.60f; holoScale = 11f; holoContrast = 1.8f; holoBlend = 0.55f;
                    sparkleIntensity = 0.5f; sparkleDensity = 160f;
                    glareIntensity = 0.12f; glareSize = 0.5f; glarePower = 3.0f;
                    sheenIntensity = 0.05f;
                    bevel = 0.10f; rimIntensity = 0.15f;
                    break;

                case Builtin.FullArtFoil:
                    // V / VMAX / VSTAR / 레인보우 같은 현행 풀아트 카드용.
                    //
                    // 포일이 전면에 깔리는데 아트 자체가 이미 밝고 화려해서, 무지개를
                    // 세게 올리면 가산 합성이 포화돼 카드가 통째로 파스텔로 날아간다.
                    // 무지개는 인쇄면을 덮는 게 아니라 그 위를 스치는 정도로만 두고,
                    // '반짝임'은 글리터로 낸다. 각도에 따라 입자가 터지는 쪽이
                    // 실제 풀아트 카드를 기울일 때의 느낌에 훨씬 가깝다.
                    // 그림 자체는 거의 움직이지 않는다. 풀아트에는 배경/피사체 경계가
                    // 없어서 깊이를 주면 누끼가 덜 딴 것처럼 보이기 때문이다.
                    // 입체감은 인쇄면 위에 뜬 포일이 만든다 — _SparkleDepth 가 글리터를
                    // 아트보다 더 밀어내서 유리 아래 인쇄 / 그 위 포일로 갈라진다.
                    parallaxDepth = 0.015f; parallaxChroma = 0.015f;
                    sparkleDepth = 0.9f;
                    holoIntensity = 0.30f; holoScale = 13f; holoContrast = 2.0f; holoBlend = 0.30f;
                    sparkleIntensity = 1.35f; sparkleDensity = 210f; sparklePower = 55f;
                    glareIntensity = 0.10f; glareSize = 0.45f; glarePower = 3.2f;
                    sheenIntensity = 0.06f;
                    bevel = 0.08f; rimIntensity = 0.18f;
                    break;
            }
        }
    }
}
