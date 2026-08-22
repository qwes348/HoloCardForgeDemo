using System.IO;
using UnityEditor;
using UnityEngine;

namespace HoloCard.Editor
{
    public enum DepthSource
    {
        Luminance,
        Saturation,
        LuminanceAndSaturation,
        /// <summary>
        /// 배경색에서 얼마나 먼가를 높이로 쓴다. 휘도와 달리 "어두운 캐릭터"와
        /// "밝은 배경"을 구분할 수 있어서 피사체가 배경에서 실제로 떨어져 나온다.
        /// 아트 창 테두리를 배경 표본으로 삼는다.
        /// </summary>
        SubjectFromBackground,
    }
    public enum FoilMode
    {
        FullCard,
        Saturation,
        Luminance,
        ArtWindow,
        /// <summary>
        /// 전면 포일이되 밝은 인쇄면에서는 세기를 줄인다.
        /// 현행 풀아트 카드는 아트 자체가 이미 밝고 화려해서, 전면에 균일하게
        /// 무지개를 더하면 가산 합성이 포화돼 카드가 통째로 파스텔로 날아간다.
        /// 실제 포일도 어두운 잉크 위에서 가장 잘 보인다.
        /// </summary>
        FullArtAdaptive,
    }

    /// <summary>
    /// Depth·Foil 생성 파라미터. 창에서도 쓰고 스크립트에서도 쓴다.
    /// </summary>
    [System.Serializable]
    public struct HoloBakeSettings
    {
        public DepthSource depthSource;
        public int   blurRadius;
        public int   blurIterations;
        public float contrast;
        public float blackPoint;
        public float whitePoint;
        public bool  invertDepth;
        public float frameLift;
        public float frameWidth;

        /// <summary>엣지 보존 스무딩 반경. 0 이면 기존 박스 블러만 쓴다.</summary>
        public int edgeRadius;
        /// <summary>작을수록 경계를 날카롭게 남긴다. 피사체 실루엣이 이 값에 달렸다.</summary>
        public float edgeStrength;
        /// <summary>0 = 연속 높이, 2~8 = 그 개수의 평면으로 계단화. 디오라마처럼 보이게 한다.</summary>
        public int depthLayers;

        /// <summary>아트 창 바깥(프레임·텍스트)을 통째로 최상단 평면으로 만든다.</summary>
        public bool flattenOutsideArtWindow;
        /// <summary>추가로 평면 처리할 영역. 풀아트 카드의 텍스트 블록 등.</summary>
        public Rect[] flatRects;
        /// <summary>평면 영역 경계를 부드럽게 하는 폭.</summary>
        public float flatFeather;

        public FoilMode foilMode;
        public float foilThreshold;
        public float foilSoftness;
        public Rect  artWindow;
        public float artFeather;

        /// <summary>FullArtAdaptive: 이 휘도부터 포일을 줄이기 시작한다.</summary>
        public float highlightStart;
        /// <summary>FullArtAdaptive: 가장 밝은 곳에서 포일을 얼마나 줄일지 (0~1).</summary>
        public float highlightRolloff;

        public static HoloBakeSettings Default => new HoloBakeSettings
        {
            depthSource = DepthSource.Luminance,
            blurRadius = 3,
            blurIterations = 2,
            contrast = 1.35f,
            blackPoint = 0.05f,
            whitePoint = 0.95f,
            invertDepth = false,
            frameLift = 0.35f,
            frameWidth = 0.06f,

            foilMode = FoilMode.Saturation,
            foilThreshold = 0.25f,
            foilSoftness = 0.35f,
            artWindow = new Rect(0.08f, 0.30f, 0.84f, 0.46f),
            artFeather = 0.04f,
            highlightStart = 0.55f,
            highlightRolloff = 0.75f,

            edgeRadius = 8,
            edgeStrength = 0.06f,
            depthLayers = 0,
            flattenOutsideArtWindow = false,
            flatRects = null,
            flatFeather = 0.02f,
        };

        /// <summary>
        /// 구형 포켓몬 카드(Base Set 계열) 레이아웃에 맞춘 값.
        /// 아트 창이 UV 로 x 0.103~0.897, y 0.479~0.881 에 있다.
        /// </summary>
        public static HoloBakeSettings ClassicPokemonCard
        {
            get
            {
                var s = Default;

                // 배경색 대비로 피사체를 뽑는다. 휘도로는 어두운 포켓몬이
                // 배경에 파묻혀서 실루엣이 안 선다.
                s.depthSource = DepthSource.SubjectFromBackground;
                s.blurRadius = 1;          // 스캔 망점만 제거. 형태는 엣지 필터가 다듬는다
                s.blurIterations = 1;
                s.edgeRadius = 10;
                s.edgeStrength = 0.05f;
                s.depthLayers = 4;         // 배경 / 중경 / 피사체 / 프레임
                s.contrast = 1.25f;
                s.blackPoint = 0.10f;
                s.whitePoint = 0.88f;

                // 프레임과 텍스트는 통째로 최상단 평면. 여기가 움직이면 안 된다.
                s.frameLift = 0f;
                s.flattenOutsideArtWindow = true;
                s.flatFeather = 0.012f;

                s.foilMode = FoilMode.ArtWindow;
                s.artWindow = new Rect(0.103f, 0.479f, 0.794f, 0.402f);
                s.artFeather = 0.02f;
                return s;
            }
        }

        /// <summary>
        /// 현행 풀아트 카드(V / VMAX / VSTAR / 레인보우 / 시크릿 등).
        /// 아트가 카드 전면을 덮고 포일도 전면에 깔리므로 아트 창을 따로 잡지 않는다.
        /// 테두리가 얇아서 Frame Lift 도 거의 주지 않는다.
        /// </summary>
        public static HoloBakeSettings ModernFullArt
        {
            get
            {
                var s = Default;

                // 풀아트는 카드 전체가 그림이라 "배경 / 피사체" 경계가 없다.
                // 여기에 디오라마를 억지로 넣으면 누끼가 덜 딴 것처럼 보이므로,
                // 깊이는 인쇄면의 은은한 요철 정도로만 쓰고(휘도 기반, 계단화 없음)
                // 입체감은 그 위에 뜬 포일 층이 만든다 (_SparkleDepth, 글레어, 시트 반사).
                s.depthSource = DepthSource.Luminance;
                s.blurRadius = 2;
                s.blurIterations = 1;
                s.edgeRadius = 12;
                s.edgeStrength = 0.06f;
                s.depthLayers = 0;
                s.contrast = 0.85f;
                s.blackPoint = 0.10f;
                s.whitePoint = 0.90f;
                s.frameLift = 0f;

                // 풀아트는 아트가 카드 전면을 덮고 그 위에 텍스트가 얹힌다.
                // 아트 창이 따로 없으니 텍스트 블록을 직접 평면으로 못 박는다.
                // (Sword & Shield 계열 V / VMAX / VSTAR 레이아웃 실측값)
                s.flatRects = new[]
                {
                    new Rect(0f, 0.86f, 1f, 0.14f),   // 이름 · HP 바
                    new Rect(0f, 0f,    1f, 0.44f),   // 기술 텍스트 ~ 하단 정보
                };
                s.flatFeather = 0.035f;

                // 포일은 전면이지만, artWindow 는 배경색을 어디서 표본으로 뽑을지에도
                // 쓰인다. 텍스트에 가리지 않은 실제 그림 영역을 가리켜야 한다.
                s.artWindow = new Rect(0.05f, 0.46f, 0.90f, 0.40f);

                s.foilMode = FoilMode.FullArtAdaptive;
                s.highlightStart = 0.50f;
                s.highlightRolloff = 0.80f;
                return s;
            }
        }
    }

    /// <summary>
    /// 카드 아트 한 장에서 Depth 맵과 Foil 마스크를 뽑는 순수 로직.
    /// EditorWindow 와 배치 메뉴가 공유한다.
    /// </summary>
    public static class HoloCardBaker
    {
        public static void Bake(Texture2D source, HoloBakeSettings s,
                                out Texture2D depth, out Texture2D foil)
        {
            depth = null;
            foil = null;
            if (source == null) return;

            Texture2D readable = MakeReadable(source);
            if (readable == null) return;

            try
            {
                int w = readable.width, h = readable.height;
                Color[] px = readable.GetPixels();
                depth = ToTexture(BuildDepth(px, w, h, s), w, h);
                foil  = ToTexture(BuildFoil(px, w, h, s), w, h);
            }
            finally
            {
                Object.DestroyImmediate(readable);
            }
        }

        /// <summary>
        /// 소스 옆에 &lt;이름&gt;_Depth.png / &lt;이름&gt;_Foil.png 를 쓰고 임포트 설정까지 맞춘다.
        /// 생성된 에셋 경로를 돌려준다.
        /// </summary>
        public static bool Generate(Texture2D source, HoloBakeSettings s,
                                    out string depthPath, out string foilPath)
        {
            depthPath = foilPath = null;

            string srcPath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(srcPath)) return false;

            Bake(source, s, out Texture2D depth, out Texture2D foil);
            if (depth == null || foil == null) return false;

            try
            {
                string dir  = Path.GetDirectoryName(srcPath).Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(srcPath);
                depthPath = $"{dir}/{name}_Depth.png";
                foilPath  = $"{dir}/{name}_Foil.png";

                File.WriteAllBytes(depthPath, depth.EncodeToPNG());
                File.WriteAllBytes(foilPath,  foil.EncodeToPNG());

                AssetDatabase.ImportAsset(depthPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(foilPath,  ImportAssetOptions.ForceUpdate);
                ConfigureAsDataMap(depthPath);
                ConfigureAsDataMap(foilPath);
                return true;
            }
            finally
            {
                Object.DestroyImmediate(depth);
                Object.DestroyImmediate(foil);
            }
        }

        [MenuItem("Tools/Holo Card/Bake Selected Textures", false, 21)]
        static void BakeSelection()
        {
            var textures = Selection.GetFiltered<Texture2D>(SelectionMode.Assets);
            if (textures.Length == 0)
            {
                EditorUtility.DisplayDialog("Holo Baker",
                    "프로젝트 창에서 카드 아트 텍스처를 선택한 뒤 실행하세요.", "확인");
                return;
            }

            int made = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < textures.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Holo Baker", textures[i].name,
                        (float)i / textures.Length);
                    if (Generate(textures[i], HoloBakeSettings.Default, out _, out _)) made++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[Holo Baker] {made}/{textures.Length} 장 처리 완료.");
        }

        // ── 생성 로직 ────────────────────────────────────────────────────

        public static float[] BuildDepth(Color[] src, int w, int h, HoloBakeSettings s)
        {
            var luminance = new float[src.Length];
            for (int i = 0; i < src.Length; i++)
                luminance[i] = src[i].r * 0.2126f + src[i].g * 0.7152f + src[i].b * 0.0722f;

            float[] value = BuildDepthSignal(src, luminance, w, h, s);

            // 1) 잔 노이즈(스캔 망점) 제거
            for (int i = 0; i < s.blurIterations; i++)
                value = BoxBlur(value, w, h, s.blurRadius);

            // 2) 엣지 보존 스무딩.
            //    박스 블러만 쓰면 노이즈와 함께 피사체 실루엣까지 뭉개져서,
            //    높이맵의 경계가 그림의 경계와 어긋난다. 그러면 패럴랙스가
            //    경계를 넘나들며 번지고 "누끼가 덜 따진" 느낌이 난다.
            //    가이디드 필터는 그림 자체를 가이드로 삼아 경계는 남기고 안쪽만 편다.
            if (s.edgeRadius > 0)
                value = GuidedFilter(value, luminance, w, h, s.edgeRadius, s.edgeStrength * s.edgeStrength);

            // 3) 레벨 · 대비
            float lo = Mathf.Min(s.blackPoint, s.whitePoint - 0.01f);
            float hi = Mathf.Max(s.whitePoint, lo + 0.01f);
            for (int i = 0; i < value.Length; i++)
            {
                float v = Mathf.InverseLerp(lo, hi, value[i]);
                v = Mathf.Clamp01((v - 0.5f) * s.contrast + 0.5f);
                value[i] = s.invertDepth ? 1f - v : v;
            }

            // 4) 계단화. 연속 그라디언트는 물렁한 부조로 보이지만,
            //    몇 개의 평면으로 끊으면 배경/중경/피사체가 분리된 디오라마로 읽힌다.
            if (s.depthLayers >= 2)
            {
                float steps = s.depthLayers - 1;
                for (int i = 0; i < value.Length; i++)
                    value[i] = Mathf.Round(value[i] * steps) / steps;

                // 계단 경계를 그림의 엣지에 다시 스냅시킨다.
                if (s.edgeRadius > 0)
                    value = GuidedFilter(value, luminance, w, h,
                                         Mathf.Max(2, s.edgeRadius / 3), s.edgeStrength * s.edgeStrength);
            }

            ApplyFlatRegions(value, w, h, s);
            return value;
        }

        /// <summary>높이의 원천 신호. 아직 필터링 전.</summary>
        static float[] BuildDepthSignal(Color[] src, float[] luminance, int w, int h, HoloBakeSettings s)
        {
            var value = new float[src.Length];

            if (s.depthSource == DepthSource.SubjectFromBackground)
            {
                Color background = EstimateBackground(src, w, h, s);
                for (int i = 0; i < src.Length; i++)
                {
                    Color c = src[i];
                    // 색상 차이를 크게 본다. 명도만 다른 건 같은 배경의 명암일 때가 많다.
                    float dr = c.r - background.r, dg = c.g - background.g, db = c.b - background.b;
                    float chroma = Mathf.Sqrt(dr * dr + dg * dg + db * db);
                    float value01 = Mathf.Clamp01(chroma * 1.4f);
                    value[i] = value01;
                }
                return value;
            }

            for (int i = 0; i < src.Length; i++)
            {
                Color c = src[i];
                float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                float sat = max > 1e-4f ? (max - min) / max : 0f;

                value[i] = s.depthSource switch
                {
                    DepthSource.Luminance => luminance[i],
                    DepthSource.Saturation => sat,
                    _ => Mathf.Clamp01(luminance[i] * 0.65f + sat * 0.35f)
                };
            }
            return value;
        }

        /// <summary>
        /// 배경색 추정. artWindow 테두리 안쪽 띠를 표본으로 삼는다.
        /// 피사체는 보통 가운데 있으니 그림 영역의 가장자리는 배경일 확률이 높다.
        /// (artWindow 는 포일 모드와 무관하게 "그림이 있는 영역"으로도 쓰인다)
        /// </summary>
        static Color EstimateBackground(Color[] src, int w, int h, HoloBakeSettings s)
        {
            Rect region = s.artWindow.width > 0.05f && s.artWindow.height > 0.05f
                ? s.artWindow
                : new Rect(0f, 0f, 1f, 1f);

            int x0 = Mathf.Clamp(Mathf.RoundToInt(region.xMin * w), 0, w - 1);
            int x1 = Mathf.Clamp(Mathf.RoundToInt(region.xMax * w), 0, w - 1);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(region.yMin * h), 0, h - 1);
            int y1 = Mathf.Clamp(Mathf.RoundToInt(region.yMax * h), 0, h - 1);

            int band = Mathf.Max(2, Mathf.RoundToInt(Mathf.Min(x1 - x0, y1 - y0) * 0.08f));
            double r = 0, g = 0, b = 0;
            long n = 0;

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    bool onBand = x < x0 + band || x > x1 - band || y < y0 + band || y > y1 - band;
                    if (!onBand) continue;
                    // 텍스처는 아래에서 위로 쌓이지만 표본 평균에는 방향이 무관하다.
                    Color c = src[y * w + x];
                    r += c.r; g += c.g; b += c.b; n++;
                }
            }

            if (n == 0) return Color.black;
            return new Color((float)(r / n), (float)(g / n), (float)(b / n), 1f);
        }

        /// <summary>
        /// 지정한 영역을 높이 1.0(최상단 평면)으로 못 박는다.
        ///
        /// POM 은 높이 1.0 에서 레이마칭을 시작하므로, 높이가 정확히 1.0 이면
        /// 첫 반복에서 바로 교차 판정이 나고 UV 오프셋이 0 이 된다. 즉 그 영역은
        /// 완전히 정지한다. 인쇄된 글자를 붙잡아 두는 가장 확실한 방법이다.
        /// </summary>
        static void ApplyFlatRegions(float[] value, int w, int h, HoloBakeSettings s)
        {
            bool hasRects = s.flatRects != null && s.flatRects.Length > 0;
            if (!s.flattenOutsideArtWindow && !hasRects) return;

            float feather = Mathf.Max(s.flatFeather, 1e-4f);

            for (int y = 0; y < h; y++)
            {
                float t = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float protect = 0f;

                    if (s.flattenOutsideArtWindow)
                    {
                        float dx = Mathf.Min(u - s.artWindow.xMin, s.artWindow.xMax - u);
                        float dy = Mathf.Min(t - s.artWindow.yMin, s.artWindow.yMax - t);
                        float inside = Mathf.Clamp01(Mathf.Min(dx, dy) / feather);
                        protect = Mathf.Max(protect, 1f - inside);
                    }

                    if (hasRects)
                    {
                        foreach (Rect rect in s.flatRects)
                        {
                            float dx = Mathf.Min(u - rect.xMin, rect.xMax - u);
                            float dy = Mathf.Min(t - rect.yMin, rect.yMax - t);
                            float d = Mathf.Min(dx, dy);
                            if (d <= -feather) continue;
                            protect = Mathf.Max(protect, Mathf.Clamp01((d + feather) / (feather * 2f)));
                        }
                    }

                    int i = y * w + x;
                    value[i] = Mathf.Lerp(value[i], 1f, protect);
                }
            }
        }

        /// <summary>
        /// 가이디드 필터. 가이드 이미지의 경계를 유지하면서 입력을 매끈하게 만든다.
        /// 박스 블러 몇 번으로 구현되어 O(N) 이라 에디터에서 돌려도 빠르다.
        /// He et al., "Guided Image Filtering".
        /// </summary>
        public static float[] GuidedFilter(float[] p, float[] guide, int w, int h, int radius, float eps)
        {
            int n = p.Length;
            var meanI  = BoxBlur((float[])guide.Clone(), w, h, radius);
            var meanP  = BoxBlur((float[])p.Clone(), w, h, radius);

            var ii = new float[n];
            var ip = new float[n];
            for (int i = 0; i < n; i++)
            {
                ii[i] = guide[i] * guide[i];
                ip[i] = guide[i] * p[i];
            }
            var meanII = BoxBlur(ii, w, h, radius);
            var meanIP = BoxBlur(ip, w, h, radius);

            var a = new float[n];
            var b = new float[n];
            for (int i = 0; i < n; i++)
            {
                float varI  = meanII[i] - meanI[i] * meanI[i];
                float covIP = meanIP[i] - meanI[i] * meanP[i];
                a[i] = covIP / (varI + eps);
                b[i] = meanP[i] - a[i] * meanI[i];
            }

            var meanA = BoxBlur(a, w, h, radius);
            var meanB = BoxBlur(b, w, h, radius);

            var q = new float[n];
            for (int i = 0; i < n; i++)
                q[i] = Mathf.Clamp01(meanA[i] * guide[i] + meanB[i]);

            return q;
        }

        public static float[] BuildFoil(Color[] src, int w, int h, HoloBakeSettings s)
        {
            var value = new float[src.Length];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    float v;

                    if (s.foilMode == FoilMode.FullCard)
                    {
                        v = 1f;
                    }
                    else if (s.foilMode == FoilMode.FullArtAdaptive)
                    {
                        Color c = src[i];
                        float lum = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
                        v = 1f - s.highlightRolloff * Mathf.SmoothStep(0f, 1f,
                                Mathf.InverseLerp(s.highlightStart, 1f, lum));
                    }
                    else if (s.foilMode == FoilMode.ArtWindow)
                    {
                        float u = (x + 0.5f) / w, t = (y + 0.5f) / h;
                        float dx = Mathf.Min(u - s.artWindow.xMin, s.artWindow.xMax - u);
                        float dy = Mathf.Min(t - s.artWindow.yMin, s.artWindow.yMax - t);
                        float d  = Mathf.Min(dx, dy);
                        v = s.artFeather <= 1e-4f ? (d > 0f ? 1f : 0f)
                                                  : Mathf.Clamp01(d / s.artFeather);
                    }
                    else
                    {
                        Color c = src[i];
                        float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                        float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                        float raw = s.foilMode == FoilMode.Saturation
                            ? (max > 1e-4f ? (max - min) / max : 0f)
                            : c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;

                        v = Mathf.Clamp01((raw - s.foilThreshold) / Mathf.Max(s.foilSoftness, 1e-4f));
                    }

                    value[i] = v;
                }
            }

            if (s.foilMode != FoilMode.FullCard)
                value = BoxBlur(value, w, h, 2);

            return value;
        }

        // ── 유틸 ─────────────────────────────────────────────────────────

        /// <summary>높이맵/마스크는 색이 아니라 데이터다. sRGB 를 끄고 Clamp 로 물린다.</summary>
        public static void ConfigureAsDataMap(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.sRGBTexture        = false;
            importer.wrapMode           = TextureWrapMode.Clamp;
            importer.filterMode         = FilterMode.Bilinear;
            importer.mipmapEnabled      = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.alphaSource        = TextureImporterAlphaSource.None;
            importer.npotScale          = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// 카드 아트용 임포트 설정.
        ///
        /// 핵심은 npotScale = None 이다. 기본값 ToNearest 는 2의 거듭제곱이 아닌
        /// 텍스처를 가장 가까운 거듭제곱으로 리샘플하는데, 600x825 카드가 512x1024 로
        /// 바뀌면서 종횡비가 0.727 에서 0.5 로 망가진다. 이 값을 읽어 메시 폭을 잡으면
        /// 카드가 눈에 띄게 홀쭉해지고, 아트도 비균등 리샘플로 뭉개진다.
        /// </summary>
        public static void ConfigureAsCardArt(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.npotScale          = TextureImporterNPOTScale.None;
            importer.wrapMode           = TextureWrapMode.Clamp;
            importer.filterMode         = FilterMode.Bilinear;
            importer.mipmapEnabled      = true;
            importer.maxTextureSize     = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// 원본 파일의 종횡비. 임포트된 Texture2D 의 width/height 는 npotScale 이나
        /// maxTextureSize 에 흔들리므로 카드 비율은 반드시 원본에서 읽어야 한다.
        /// </summary>
        public static float SourceAspect(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                if (w > 0 && h > 0) return (float)w / h;
            }
            return texture.height > 0 ? (float)texture.width / texture.height : 0.716f;
        }

        /// <summary>Read/Write 를 켜지 않고 픽셀을 읽는다. 임포트 설정을 안 건드리는 게 핵심.</summary>
        public static Texture2D MakeReadable(Texture source)
        {
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);

            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return readable;
        }

        /// <summary>
        /// 분리형 박스 블러. 슬라이딩 윈도우라 반경과 무관하게 픽셀당 O(1) 이다.
        ///
        /// 가이디드 필터가 이걸 여섯 번 부르기 때문에 반경에 비례하는 구현이면
        /// 카드 한 장에 수억 번 연산이 되어 에디터가 멈춘다.
        /// </summary>
        public static float[] BoxBlur(float[] src, int w, int h, int radius)
        {
            if (radius <= 0) return src;

            var tmp = new float[src.Length];
            var dst = new float[src.Length];
            float inv = 1f / (radius * 2 + 1);

            // 가로
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                float sum = 0f;
                for (int k = -radius; k <= radius; k++)
                    sum += src[row + Mathf.Clamp(k, 0, w - 1)];
                tmp[row] = sum * inv;

                for (int x = 1; x < w; x++)
                {
                    sum -= src[row + Mathf.Clamp(x - radius - 1, 0, w - 1)];
                    sum += src[row + Mathf.Clamp(x + radius, 0, w - 1)];
                    tmp[row + x] = sum * inv;
                }
            }

            // 세로
            for (int x = 0; x < w; x++)
            {
                float sum = 0f;
                for (int k = -radius; k <= radius; k++)
                    sum += tmp[Mathf.Clamp(k, 0, h - 1) * w + x];
                dst[x] = sum * inv;

                for (int y = 1; y < h; y++)
                {
                    sum -= tmp[Mathf.Clamp(y - radius - 1, 0, h - 1) * w + x];
                    sum += tmp[Mathf.Clamp(y + radius, 0, h - 1) * w + x];
                    dst[y * w + x] = sum * inv;
                }
            }

            return dst;
        }

        public static Texture2D ToTexture(float[] value, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            var px = new Color32[value.Length];
            for (int i = 0; i < value.Length; i++)
            {
                byte v = (byte)Mathf.RoundToInt(Mathf.Clamp01(value[i]) * 255f);
                px[i] = new Color32(v, v, v, 255);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
