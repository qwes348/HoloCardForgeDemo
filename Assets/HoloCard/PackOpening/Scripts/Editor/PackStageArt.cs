using System.IO;
using UnityEditor;
using UnityEngine;

namespace HoloCard.PackOpening.Editor
{
    /// <summary>
    /// 무대(배경 층)와 슬래시 이펙트에 쓰는 텍스처를 절차적으로 만든다.
    ///
    /// 전부 알파를 쓰므로 카드 아트용 <see cref="PackArtGenerator"/> 와 저장 경로를
    /// 나눠 둔다 (그쪽은 알파를 1 로 밀어 버린다).
    /// </summary>
    public static class PackStageArt
    {
        const string Dir = "Assets/HoloCard/PackOpening/Textures/Stage";

        // 레퍼런스에서 뽑은 값. 위가 밝고 아래로 갈수록 파래진다.
        static readonly Color SkyTop    = new Color32(212, 225, 250, 255);
        static readonly Color SkyBottom = new Color32(138, 174, 216, 255);

        [MenuItem("Tools/Holo Card/Generate Stage Art", false, 42)]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(Dir);

            Write("StageSky",    BuildSky(8, 512),        8,  512);
            Write("StageRays",   BuildRays(512, 512),   512,  512);
            Write("StageMotes",  BuildMotes(512, 512),  512,  512);
            Write("SlashStreak", BuildStreak(1024, 64), 1024,  64);
            Write("SlashFlash",  BuildFlash(256, 256),  256,  256);
            Write("Chevron",     BuildChevron(128, 192), 128,  192);
            Write("PipDiamond",  BuildDiamond(128, 128), 128,  128);
            Write("PipStar",     BuildStar(128, 128),    128,  128);
            Write("NewBadge",    BuildNewBadge(256, 128), 256, 128);
            // 무지개는 가로로 흘러야 해서 유일하게 Repeat 이다.
            Write("StageRainbow", BuildRainbow(512, 256), 512, 256, repeat: true);

            AssetDatabase.Refresh();
            Debug.Log($"[Pack Opening] 무대 아트 생성 완료 → {Dir}");
        }

        // ── 배경 세로 그라디언트 ─────────────────────────────────────────

        static Color[] BuildSky(int w, int h)
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                // v=0 이 아래. 아래가 더 짙은 파랑이다.
                float v = (y + 0.5f) / h;
                Color c = Color.Lerp(SkyBottom, SkyTop, Mathf.Pow(v, 0.85f));
                for (int x = 0; x < w; x++) px[y * w + x] = c;
            }
            return px;
        }

        // ── 중간 층: 위에서 퍼지는 부드러운 빛살 ─────────────────────────

        static Color[] BuildRays(int w, int h)
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f;
                    float v = (y + 0.5f) / h * 2f - 1f;

                    // 광원은 화면 위쪽 바깥. 거기서 부챗살로 퍼진다.
                    float dx = u, dy = v - 1.35f;
                    float ang = Mathf.Atan2(dx, -dy);
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    float fan = Mathf.Cos(ang * 7f + Fbm(u * 1.6f, v * 1.6f, 3) * 2.4f) * 0.5f + 0.5f;
                    fan = Mathf.Pow(fan, 2.6f);

                    // 광원에서 멀어질수록, 중심에서 벗어날수록 죽인다.
                    float reach = Mathf.Clamp01(1f - (r - 0.35f) / 1.5f);
                    float cone  = Mathf.Clamp01(1f - Mathf.Abs(ang) / 0.95f);

                    // 빛살은 "있는 듯 없는 듯" 이어야 한다. 세게 주면 위쪽에 흰 덩어리가 뜬다.
                    float a = fan * reach * cone * 0.16f;
                    px[y * w + x] = new Color(1f, 0.99f, 0.96f, Mathf.Clamp01(a));
                }
            }
            return px;
        }

        // ── 앞 층: 떠다니는 작은 빛 알갱이 ───────────────────────────────

        static Color[] BuildMotes(int w, int h)
        {
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 1f, 1f, 0f);

            // 이 판은 512px 텍스처가 화면 높이보다 크게 늘어난다 (텍셀 하나가 화면
            // 세 픽셀쯤). 반경을 텍스처 기준으로 크게 잡으면 눈보라가 된다.
            var rng = new System.Random(20260827);
            for (int i = 0; i < 320; i++)
            {
                float cx = (float)rng.NextDouble() * w;
                float cy = (float)rng.NextDouble() * h;
                float radius = 0.5f + (float)rng.NextDouble() * 1.3f;
                float peak = 0.20f + (float)rng.NextDouble() * 0.45f;
                var tint = Color.Lerp(new Color(1f, 1f, 1f), new Color(0.82f, 0.93f, 1f),
                                      (float)rng.NextDouble());

                int r = Mathf.CeilToInt(radius * 3f);
                for (int y = Mathf.Max(0, (int)cy - r); y < Mathf.Min(h, (int)cy + r); y++)
                {
                    for (int x = Mathf.Max(0, (int)cx - r); x < Mathf.Min(w, (int)cx + r); x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                        float a = Mathf.Exp(-(d * d) / (radius * radius)) * peak;
                        if (a <= 0.002f) continue;

                        int k = y * w + x;
                        float na = Mathf.Clamp01(px[k].a + a);
                        px[k] = new Color(tint.r, tint.g, tint.b, na);
                    }
                }
            }
            return px;
        }

        // ── 슬래시 광선 ──────────────────────────────────────────────────

        static Color[] BuildStreak(int w, int h)
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h * 2f - 1f;          // -1..1 (두께 방향)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f;      // -1..1 (길이 방향)

                    // 양 끝으로 갈수록 가늘어지는 방추형. 렌즈 플레어의 기본형이다.
                    float taper = Mathf.Pow(Mathf.Clamp01(1f - u * u), 0.55f);
                    if (taper <= 0f) continue;

                    float core = Mathf.Exp(-Mathf.Pow(v / (taper * 0.16f + 1e-3f), 2f));
                    float glow = Mathf.Exp(-Mathf.Pow(v / (taper * 0.75f + 1e-3f), 2f)) * 0.45f;
                    float a = Mathf.Clamp01(core + glow) * taper;

                    // 코어는 흰색, 번지는 부분은 따뜻하게. 차가운 흰 줄만이면 밋밋하다.
                    Color c = Color.Lerp(new Color(1f, 0.82f, 0.52f), Color.white, Mathf.Clamp01(core * 1.4f));
                    px[y * w + x] = new Color(c.r, c.g, c.b, a);
                }
            }
            return px;
        }

        static Color[] BuildFlash(int w, int h)
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f;
                    float v = (y + 0.5f) / h * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float ang = Mathf.Atan2(v, u);

                    float core = Mathf.Exp(-r * r * 14f);
                    float spikes = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 2f)), 26f) * Mathf.Exp(-r * r * 3.2f);
                    float halo = Mathf.Exp(-r * r * 5f) * 0.28f;

                    float a = Mathf.Clamp01(core + spikes * 0.9f + halo);
                    Color c = Color.Lerp(new Color(1f, 0.88f, 0.62f), Color.white, Mathf.Clamp01(core * 1.6f));
                    px[y * w + x] = new Color(c.r, c.g, c.b, a);
                }
            }
            return px;
        }

        // ── 캐러셀 화살표 ────────────────────────────────────────────────

        /// <summary>
        /// 오른쪽을 가리키는 홑화살괄호(<c>›</c>). 왼쪽 화살표는 이걸 x 로 뒤집어 쓴다.
        ///
        /// 두 선분까지의 거리로 그린다. 폴리곤을 채우면 꼭짓점 안쪽이 뭉치고
        /// 끝이 뾰족해지는데, 거리장은 획 두께가 어디서나 같아서 끝이 둥글게
        /// 마감된다 — 레퍼런스의 화살표가 그 모양이다.
        /// </summary>
        static Color[] BuildChevron(int w, int h)
        {
            var px = new Color[w * h];

            // 텍스처가 세로로 기니 좌표는 짧은 변(가로) 기준으로 정규화한다.
            var apex = new Vector2( 0.46f, 0f);
            var top  = new Vector2(-0.42f, 0.74f);
            var bot  = new Vector2(-0.42f, -0.74f);
            const float half = 0.135f;                 // 획 두께의 절반

            float feather = 2.2f / w * 2f;             // 가장자리만 두 픽셀 남짓

            for (int y = 0; y < h; y++)
            {
                float v = ((y + 0.5f) / h * 2f - 1f) * ((float)h / w);
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f;
                    var p = new Vector2(u, v);

                    float d = Mathf.Min(SegmentDistance(p, apex, top), SegmentDistance(p, apex, bot));
                    float a = Mathf.Clamp01((half - d) / Mathf.Max(feather, 1e-4f));
                    if (a <= 0f) continue;

                    px[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            }
            return px;
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a, ap = p - a;
            float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / Mathf.Max(Vector2.Dot(ab, ab), 1e-6f));
            return (ap - ab * t).magnitude;
        }

        // ── 레어도 표식 ──────────────────────────────────────────────────

        /// <summary>◇ — 속이 빈 마름모. 다각형 거리장의 등고선만 남긴다.</summary>
        static Color[] BuildDiamond(int w, int h)
        {
            var poly = new[]
            {
                new Vector2( 0f,    0.80f),
                new Vector2( 0.56f, 0f),
                new Vector2( 0f,   -0.80f),
                new Vector2(-0.56f, 0f),
            };
            return Stroke(w, h, poly, 0.085f);
        }

        /// <summary>★ — 속을 채운 오각별.</summary>
        static Color[] BuildStar(int w, int h)
        {
            var poly = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                // 꼭짓점이 위를 보게 90도 돌린다.
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = (i % 2 == 0) ? 0.86f : 0.40f;
                poly[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            return Fill(w, h, poly);
        }

        // ── 레어 무지개 ──────────────────────────────────────────────────

        /// <summary>
        /// 레어가 나올 때 배경을 덮는 무지개. 가산 합성이라 알파가 곧 세기다.
        /// u 로 흘리므로 가로 방향은 반드시 이어져야 한다 — 색상환도 띠도
        /// u 에 대해 주기가 1 이어야 이음매가 안 보인다.
        /// </summary>
        static Color[] BuildRainbow(int w, int h)
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;

                    float hue = Mathf.Repeat(u + v * 0.26f, 1f);
                    Color c = Color.HSVToRGB(hue, 0.92f, 1f);

                    // 굵은 띠 세 줄. 균일하게 깔면 무지개가 아니라 회색 막이 된다.
                    float band = 0.5f + 0.5f * Mathf.Cos((u * 3f + v * 0.8f) * Mathf.PI * 2f);
                    // 위아래 끝은 살짝 죽여 화면 밖으로 자연스럽게 빠지게 한다.
                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.Min(v, 1f - v) / 0.18f);

                    // 골을 깊게 판다. 균일하게 깔면 색이 서로 섞여 연분홍 막이
                    // 되고, 띠 사이가 비어야 각 띠의 색이 그대로 읽힌다.
                    px[y * w + x] = new Color(c.r, c.g, c.b,
                        (0.08f + Mathf.Pow(band, 1.7f) * 0.92f) * Mathf.Lerp(0.5f, 1f, edge));
                }
            }
            return px;
        }

        // ── NEW 뱃지 ─────────────────────────────────────────────────────

        /// <summary>
        /// 둥근 모서리 태그 위의 흰 "NEW".
        ///
        /// 폰트를 쓰지 않는 이유: TMP 폰트 에셋을 끌어들이면 이 씬 하나 때문에
        /// 프로젝트에 uGUI/TMP 의존이 생긴다. 세 글자뿐이라 획을 직접 긋는 게 싸다.
        /// </summary>
        static Color[] BuildNewBadge(int w, int h)
        {
            var px = new Color[w * h];

            var fill   = new Color(1f, 0.74f, 0.14f);   // 호박색 태그
            var border = new Color(1f, 0.97f, 0.88f);
            var ink    = Color.white;

            float aspect = (float)w / h;
            float feather = 2.4f / h * 2f;

            // 글자 세 개를 가운데 정렬로 배치한다. 좌표는 짧은 변(세로) 기준.
            const float glyphH = 0.40f;      // 글자 반높이
            const float glyphW = 0.26f;      // 글자 반너비
            const float gap    = 0.20f;
            float pitch = glyphW * 2f + gap;
            var centers = new[] { -pitch, 0f, pitch };

            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h * 2f - 1f;
                for (int x = 0; x < w; x++)
                {
                    float u = ((x + 0.5f) / w * 2f - 1f) * aspect;

                    // 태그 본체
                    float box = RoundedBoxDistance(new Vector2(u, v),
                                                   new Vector2(aspect - 0.14f, 0.82f), 0.46f);
                    float inside = Mathf.Clamp01((-box) / feather);
                    if (inside <= 0f) continue;

                    // 테두리는 안쪽으로 한 겹
                    float rim = Mathf.Clamp01((box + 0.10f) / feather);
                    Color c = Color.Lerp(fill, border, rim);

                    // 글자. 좌표계를 늘여 놨으므로 두께 판정도 같은 비율로 되돌린다.
                    float d = float.MaxValue;
                    for (int g = 0; g < 3; g++)
                    {
                        var local = new Vector2((u - centers[g]) / glyphW, v / glyphH);
                        d = Mathf.Min(d, GlyphDistance(g, local));
                    }
                    float letter = Mathf.Clamp01((0.30f - d) / (feather / glyphH));
                    c = Color.Lerp(c, ink, letter);

                    px[y * w + x] = new Color(c.r, c.g, c.b, inside);
                }
            }
            return px;
        }

        /// <summary>0 = N, 1 = E, 2 = W. 좌표는 -1..1 로 정규화된 글자 상자.</summary>
        static float GlyphDistance(int glyph, Vector2 p)
        {
            switch (glyph)
            {
                case 0:  // N
                    return Min3(
                        SegmentDistance(p, new Vector2(-1f, -1f), new Vector2(-1f, 1f)),
                        SegmentDistance(p, new Vector2(-1f,  1f), new Vector2( 1f, -1f)),
                        SegmentDistance(p, new Vector2( 1f, -1f), new Vector2( 1f,  1f)));
                case 1:  // E
                    return Mathf.Min(Min3(
                        SegmentDistance(p, new Vector2(-1f, -1f), new Vector2(-1f,  1f)),
                        SegmentDistance(p, new Vector2(-1f,  1f), new Vector2(0.80f, 1f)),
                        SegmentDistance(p, new Vector2(-1f,  0f), new Vector2(0.50f, 0f))),
                        SegmentDistance(p, new Vector2(-1f, -1f), new Vector2(0.80f, -1f)));
                default: // W
                    return Mathf.Min(Min3(
                        SegmentDistance(p, new Vector2(-1f,    1f), new Vector2(-0.50f, -1f)),
                        SegmentDistance(p, new Vector2(-0.50f, -1f), new Vector2( 0f,    0.35f)),
                        SegmentDistance(p, new Vector2( 0f,    0.35f), new Vector2( 0.50f, -1f))),
                        SegmentDistance(p, new Vector2( 0.50f, -1f), new Vector2( 1f,     1f)));
            }
        }

        static float Min3(float a, float b, float c) => Mathf.Min(a, Mathf.Min(b, c));

        static float RoundedBoxDistance(Vector2 p, Vector2 half, float radius)
        {
            radius = Mathf.Min(radius, Mathf.Min(half.x, half.y));
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - (half - Vector2.one * radius);
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude
                   + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        // ── 다각형 ───────────────────────────────────────────────────────

        /// <summary>다각형 윤곽선만 남긴다 (속 빈 도형).</summary>
        static Color[] Stroke(int w, int h, Vector2[] poly, float half)
        {
            var px = new Color[w * h];
            float feather = 2.2f / w * 2f;
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h * 2f - 1f;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f;
                    float d = EdgeDistance(new Vector2(u, v), poly);
                    float a = Mathf.Clamp01((half - d) / feather);
                    if (a > 0f) px[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            }
            return px;
        }

        /// <summary>다각형 속을 채운다.</summary>
        static Color[] Fill(int w, int h, Vector2[] poly)
        {
            var px = new Color[w * h];
            float feather = 2.2f / w * 2f;
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h * 2f - 1f;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f;
                    var p = new Vector2(u, v);
                    float d = EdgeDistance(p, poly);
                    // 안이면 부호를 뒤집어 경계에서만 부드럽게 한다.
                    float signed = Inside(p, poly) ? -d : d;
                    float a = Mathf.Clamp01(0.5f - signed / feather);
                    if (a > 0f) px[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            }
            return px;
        }

        static float EdgeDistance(Vector2 p, Vector2[] poly)
        {
            float d = float.MaxValue;
            for (int i = 0; i < poly.Length; i++)
                d = Mathf.Min(d, SegmentDistance(p, poly[i], poly[(i + 1) % poly.Length]));
            return d;
        }

        static bool Inside(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) == (poly[j].y > p.y)) continue;
                float t = (p.y - poly[i].y) / (poly[j].y - poly[i].y);
                if (p.x < poly[i].x + t * (poly[j].x - poly[i].x)) inside = !inside;
            }
            return inside;
        }

        // ── 유틸 ─────────────────────────────────────────────────────────

        static void Write(string name, Color[] px, int w, int h, bool repeat = false)
        {
            string path = $"{Dir}/{name}.png";

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.npotScale          = TextureImporterNPOTScale.None;
            importer.wrapMode           = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode         = FilterMode.Bilinear;
            importer.mipmapEnabled      = true;
            importer.alphaSource        = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture        = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static float Hash(int x, int y)
        {
            float n = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return n - Mathf.Floor(n);
        }

        static float ValueNoise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3f - 2f * xf);
            float v = yf * yf * (3f - 2f * yf);
            float a = Hash(xi, yi), b = Hash(xi + 1, yi);
            float c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += ValueNoise(x * freq, y * freq) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2.03f;
            }
            return sum / Mathf.Max(norm, 1e-4f);
        }
    }
}
