using System.IO;
using UnityEditor;
using UnityEngine;

namespace HoloCard.PackOpening.Editor
{
    /// <summary>
    /// 팩 포장지와 카드 뒷면 텍스처를 절차적으로 만든다.
    ///
    /// 실제 카드 뒷면·팩 디자인은 저작물이라 쓸 수 없으므로 같은 문법
    /// (방사형 광선 + 중앙 엠블럼 + 금색 테두리)만 빌린 오리지널 도안을 그린다.
    /// Depth·Foil 도 같이 뽑아서 홀로 셰이더를 그대로 물릴 수 있다.
    /// </summary>
    public static class PackArtGenerator
    {
        const string Dir = "Assets/HoloCard/PackOpening/Textures";

        [MenuItem("Tools/Holo Card/Generate Pack Art", false, 40)]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(Dir);

            Write("CardBack", BuildCardBack(734, 1024));
            Write("PackWrap", BuildPackWrap(512, 975));   // 셸 비율 1 : 1.905

            AssetDatabase.Refresh();
            Debug.Log($"[Pack Opening] 팩 아트 생성 완료 → {Dir}");
        }

        struct Layer
        {
            public Color[] art;
            public float[] depth;
            public float[] foil;
            public int width, height;
        }

        static void Write(string name, Layer layer)
        {
            SavePng($"{Dir}/{name}.png", ToTexture(layer.art, layer.width, layer.height));
            SavePng($"{Dir}/{name}_Depth.png", ToGray(layer.depth, layer.width, layer.height));
            SavePng($"{Dir}/{name}_Foil.png", ToGray(layer.foil, layer.width, layer.height));

            AssetDatabase.ImportAsset($"{Dir}/{name}.png", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset($"{Dir}/{name}_Depth.png", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset($"{Dir}/{name}_Foil.png", ImportAssetOptions.ForceUpdate);

            HoloCard.Editor.HoloCardBaker.ConfigureAsCardArt($"{Dir}/{name}.png");
            HoloCard.Editor.HoloCardBaker.ConfigureAsDataMap($"{Dir}/{name}_Depth.png");
            HoloCard.Editor.HoloCardBaker.ConfigureAsDataMap($"{Dir}/{name}_Foil.png");
        }

        // ── 카드 뒷면 ────────────────────────────────────────────────────

        static Layer BuildCardBack(int w, int h)
        {
            var art = new Color[w * h];
            var depth = new float[w * h];
            var foil = new float[w * h];

            var deep   = new Color(0.035f, 0.028f, 0.11f);
            var mid    = new Color(0.13f, 0.09f, 0.34f);
            var glow   = new Color(0.33f, 0.24f, 0.62f);
            var gold   = new Color(0.85f, 0.72f, 0.38f);
            var goldHi = new Color(1.00f, 0.93f, 0.70f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    float u = (x + 0.5f) / w;
                    float v = (y + 0.5f) / h;

                    // 카드 비율을 보정한 중심 기준 좌표
                    float cx = (u - 0.5f) * 2f;
                    float cy = (v - 0.5f) * 2f * ((float)h / w);
                    float r = Mathf.Sqrt(cx * cx + cy * cy);
                    float ang = Mathf.Atan2(cy, cx);

                    // 바탕: 중심에서 퍼지는 방사형 그라디언트
                    float radial = Mathf.Clamp01(1f - r * 0.72f);
                    Color c = Color.Lerp(deep, mid, radial);
                    c = Color.Lerp(c, glow, Mathf.Pow(radial, 3.2f) * 0.85f);

                    // 광선 24 갈래
                    float rays = Mathf.Cos(ang * 24f) * 0.5f + 0.5f;
                    rays = Mathf.Pow(rays, 2.4f);
                    float rayFade = Mathf.Clamp01(1f - Mathf.Abs(r - 0.55f) / 0.62f);
                    c += glow * rays * rayFade * 0.42f;

                    float d = 0.30f + radial * 0.22f + rays * rayFade * 0.10f;
                    float f = 0.35f + rays * rayFade * 0.5f;

                    // 동심원 두 겹
                    foreach (float ring in new[] { 0.40f, 0.46f })
                    {
                        float band = 1f - Mathf.Clamp01(Mathf.Abs(r - ring) / 0.012f);
                        if (band <= 0f) continue;
                        c = Color.Lerp(c, gold, band * 0.75f);
                        d = Mathf.Max(d, 0.62f * band + d * (1f - band));
                        f = Mathf.Max(f, band);
                    }

                    // 중앙 엠블럼 — 회전한 사각형(다이아몬드)
                    float2 rot = Rotate(cx, cy, 0.7853f);
                    float diamond = Mathf.Max(Mathf.Abs(rot.x), Mathf.Abs(rot.y));
                    float emblem = 1f - Mathf.Clamp01((diamond - 0.19f) / 0.02f);
                    if (emblem > 0f)
                    {
                        float shade = Mathf.Clamp01(0.45f + (rot.y + 0.2f) * 1.5f);
                        Color face = Color.Lerp(gold, goldHi, shade);
                        c = Color.Lerp(c, face, emblem);
                        d = Mathf.Lerp(d, 0.90f, emblem);
                        f = Mathf.Lerp(f, 1f, emblem);
                    }

                    // 엠블럼 안쪽 홈
                    float inner = 1f - Mathf.Clamp01((diamond - 0.115f) / 0.014f);
                    if (inner > 0f)
                    {
                        c = Color.Lerp(c, deep * 1.6f, inner * 0.9f);
                        d = Mathf.Lerp(d, 0.55f, inner);
                    }

                    // 테두리 프레임
                    float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    float border = 1f - Mathf.Clamp01((edge - 0.028f) / 0.006f);
                    float borderInner = 1f - Mathf.Clamp01((edge - 0.052f) / 0.004f);
                    if (border > 0f) { c = Color.Lerp(c, gold, border); d = Mathf.Lerp(d, 1f, border); f = Mathf.Lerp(f, 0.9f, border); }
                    if (borderInner > 0f && border <= 0f) { c = Color.Lerp(c, gold * 0.7f, borderInner * 0.8f); d = Mathf.Lerp(d, 0.85f, borderInner); }

                    // 바깥쪽 여백은 짙게
                    float outside = 1f - Mathf.Clamp01(edge / 0.022f);
                    c = Color.Lerp(c, deep * 0.7f, outside * 0.85f);

                    // 미세 노이즈로 인쇄 질감
                    float n = Hash(x * 0.7f, y * 0.7f) - 0.5f;
                    c += new Color(n, n, n) * 0.022f;

                    art[i] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);
                    depth[i] = Mathf.Clamp01(d);
                    foil[i] = Mathf.Clamp01(f);
                }
            }

            return new Layer { art = art, depth = depth, foil = foil, width = w, height = h };
        }

        // ── 팩 포장지 ────────────────────────────────────────────────────

        /// <summary>
        /// 팩 포장지.
        ///
        /// UV 는 셸 메시(PackShellBaker)의 XY 평행투영이라 텍스처 세로 위치가 곧
        /// 팩 높이다. 그래서 위아래 끝의 압착 밀봉(크림프)을 텍스처에서도 그려야
        /// 한다. 다만 실물을 보면 그 구간이 은박으로 **바뀌는** 게 아니라 같은
        /// 인쇄가 눌려서 어두워지고 실링 다이의 세로 골이 얹힌 것이다. 폭도
        /// 생각보다 훨씬 좁다 — 위아래 각각 5% 정도.
        ///
        /// 바탕은 소용돌이다. 평평한 그라디언트 위에서는 필름 셰이더의 정반사가
        /// 갈 곳이 없어 얼룩이 그대로 다 드러난다. 실물 팩 배경이 거의 예외 없이
        /// 감아 나가는 빛줄기인 데는 이유가 있다.
        ///
        /// 구김 음영은 아주 약하게만 굽는다. 움직이는 구김은 PackFilm 셰이더가
        /// 맡으므로, 텍스처에 박힌 정적인 구김이 세면 두 겹이 어긋나 인쇄된
        /// 구김처럼 보인다.
        /// </summary>
        static Layer BuildPackWrap(int w, int h)
        {
            var art = new Color[w * h];
            var depth = new float[w * h];
            var foil = new float[w * h];

            var top    = new Color(0.14f, 0.36f, 0.72f);
            var bottom = new Color(0.04f, 0.05f, 0.22f);
            var foam   = new Color(0.72f, 0.84f, 0.97f);
            var accent = new Color(0.95f, 0.30f, 0.42f);
            var gold   = new Color(0.92f, 0.78f, 0.40f);
            var cream  = new Color(0.96f, 0.94f, 0.88f);
            var silver = new Color(0.58f, 0.62f, 0.70f);

            // 밀봉 띠가 시작하는 높이 (끝에서부터의 거리 기준).
            const float crimp = 0.955f;
            // 뜯는 선은 크림프 바로 아래.
            const float tearLine = 0.925f;

            float aspect = (float)h / w;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    float u = (x + 0.5f) / w;
                    float v = (y + 0.5f) / h;

                    // 위아래가 대칭이므로 "끝에서 얼마나 떨어졌나" 하나로 다룬다.
                    float fromEnd = Mathf.Min(v, 1f - v);
                    float crimpMask = 1f - Mathf.Clamp01((fromEnd - (1f - crimp)) / 0.018f);

                    // ── 바탕 소용돌이
                    float sx = (u - 0.5f) * 2f;
                    float sy = (v - 0.52f) * 2f * aspect;
                    float sr = Mathf.Sqrt(sx * sx + sy * sy);
                    float twist = Mathf.Atan2(sy, sx) + sr * 2.2f;

                    float arms = Mathf.Sin(twist * 5f + Fbm(sx * 1.8f, sy * 1.8f, 3) * 5f) * 0.5f + 0.5f;
                    arms = Mathf.Pow(arms, 2.2f) * Mathf.Clamp01(1.2f - sr * 0.42f);

                    Color c = Color.Lerp(bottom, top, Mathf.Pow(v, 0.7f));
                    c = Color.Lerp(c, foam, arms * 0.5f);

                    float d = 0.42f + arms * 0.10f;
                    float f = 0.80f;

                    // ── 중앙 원형 엠블럼
                    float cx = (u - 0.5f) * 2f;
                    float cy = (v - 0.55f) * 2f * aspect;
                    float r = Mathf.Sqrt(cx * cx + cy * cy);

                    float disc = 1f - Mathf.Clamp01((r - 0.42f) / 0.03f);
                    if (disc > 0f)
                    {
                        c = Color.Lerp(c, bottom * 1.4f, disc * 0.88f);
                        d = Mathf.Lerp(d, 0.60f, disc);
                    }
                    float discRim = 1f - Mathf.Clamp01(Mathf.Abs(r - 0.42f) / 0.022f);
                    if (discRim > 0f)
                    {
                        c = Color.Lerp(c, gold, discRim);
                        d = Mathf.Lerp(d, 0.78f, discRim);
                        f = Mathf.Lerp(f, 1f, discRim);
                    }

                    // 엠블럼 안 다이아몬드
                    float2 rot = Rotate(cx, cy, 0.7853f);
                    float diamond = Mathf.Max(Mathf.Abs(rot.x), Mathf.Abs(rot.y));
                    float gem = 1f - Mathf.Clamp01((diamond - 0.20f) / 0.02f);
                    if (gem > 0f)
                    {
                        float shade = Mathf.Clamp01(0.4f + (rot.y + 0.2f) * 1.6f);
                        c = Color.Lerp(c, Color.Lerp(accent, cream, shade), gem);
                        d = Mathf.Lerp(d, 0.86f, gem);
                        f = Mathf.Lerp(f, 1f, gem);
                    }

                    // ── 하단 라벨 바
                    float bar = 1f - Mathf.Clamp01((Mathf.Abs(v - 0.145f) - 0.030f) / 0.010f);
                    if (bar > 0f)
                    {
                        c = Color.Lerp(c, cream, bar * 0.92f);
                        d = Mathf.Lerp(d, 0.70f, bar);
                        f = Mathf.Lerp(f, 0.2f, bar);
                    }

                    // ── 뜯는 선 (점선)
                    float tear = 1f - Mathf.Clamp01(Mathf.Abs(v - tearLine) / 0.0035f);
                    if (tear > 0f)
                    {
                        float dash = Mathf.Repeat(u * 46f, 1f) < 0.55f ? 1f : 0.15f;
                        c = Color.Lerp(c, cream, tear * dash * 0.75f);
                        d = Mathf.Lerp(d, 0.28f, tear * dash);
                    }

                    // ── 좌우 접합부
                    float edgeU = Mathf.Min(u, 1f - u);
                    float seam = 1f - Mathf.Clamp01(edgeU / 0.030f);
                    c = Color.Lerp(c, bottom * 1.2f, seam * 0.5f);
                    d = Mathf.Lerp(d, 0.30f, seam * 0.7f);

                    // ── 구김. 셰이더가 움직이는 구김을 맡으므로 여기서는 큰 굴곡만.
                    float crease = Crease(u, v, out float coarse);
                    float fold = (coarse - 0.5f) * 2f;

                    d = Mathf.Clamp01(d + fold * 0.05f + crease * 0.08f);
                    c *= 0.96f + fold * 0.04f;
                    c += new Color(0.62f, 0.70f, 0.88f) * crease * 0.06f;
                    f = Mathf.Clamp01(f * (1f - crease * 0.18f));

                    // ── 압착 밀봉. 접히는 선에 그늘이 먼저 지고, 그 위가 눌린 인쇄다.
                    float shoulder = 1f - Mathf.Clamp01(Mathf.Abs(fromEnd - (1f - crimp)) / 0.012f);
                    c = Color.Lerp(c, c * 0.5f, shoulder * 0.75f);

                    if (crimpMask > 0f)
                    {
                        // 완전 등간격이면 기계 무늬가 된다. 위상을 조금 흔든다.
                        float wobble = (ValueNoise(u * 11f, 5.3f) - 0.5f) * 1.4f;
                        float rib = Mathf.Cos((u * 46f + wobble) * Mathf.PI * 2f) * 0.5f + 0.5f;

                        Color pressed = c * (0.45f + rib * 0.75f);
                        pressed = Color.Lerp(pressed, silver * (0.45f + rib * 0.8f), 0.30f);

                        // 맨 끝 가장자리는 얇게 눌려 어둡다.
                        float lip = 1f - Mathf.Clamp01(fromEnd / 0.008f);
                        pressed = Color.Lerp(pressed, pressed * 0.35f, lip * 0.85f);

                        c = Color.Lerp(c, pressed, crimpMask);
                        d = Mathf.Lerp(d, 0.40f + rib * 0.10f, crimpMask);
                        f = Mathf.Lerp(f, 1f, crimpMask);
                    }

                    float n = Hash(x * 0.9f, y * 0.9f) - 0.5f;
                    c += new Color(n, n, n) * 0.02f;

                    art[i] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);
                    depth[i] = Mathf.Clamp01(d);
                    foil[i] = Mathf.Clamp01(f);
                }
            }

            return new Layer { art = art, depth = depth, foil = foil, width = w, height = h };
        }

        // ── 유틸 ─────────────────────────────────────────────────────────

        struct float2 { public float x, y; }

        static float2 Rotate(float x, float y, float a)
        {
            float s = Mathf.Sin(a), c = Mathf.Cos(a);
            return new float2 { x = x * c - y * s, y = x * s + y * c };
        }

        static float Hash(float x, float y)
        {
            float n = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return n - Mathf.Floor(n);
        }

        static float Hash2(int x, int y)
        {
            float n = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return n - Mathf.Floor(n);
        }

        /// <summary>격자 보간 노이즈.</summary>
        static float ValueNoise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3f - 2f * xf);
            float v = yf * yf * (3f - 2f * yf);

            float a = Hash2(xi, yi),     b = Hash2(xi + 1, yi);
            float c = Hash2(xi, yi + 1), d = Hash2(xi + 1, yi + 1);
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

        /// <summary>
        /// 구겨진 비닐의 접힘. fbm 의 등고선을 날카롭게 세워 능선을 만든다.
        /// 값이 클수록 접힌 마루.
        /// </summary>
        static float Crease(float u, float v, out float coarse)
        {
            // 노이즈를 세로로 길게 늘인다. 등방성 노이즈를 그대로 쓰면 대리석
            // 무늬가 되고 비닐로 안 읽힌다. 접힘은 방향이 있어야 한다.
            coarse = Fbm(u * 3.2f, v * 1.1f, 4);
            float ridge = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(coarse * 2f - 1f)), 8f);

            // 대각으로 가로지르는 굵은 접힘
            float diag = Fbm((u + v) * 3.4f, (u - v) * 1.2f, 3);
            float diagRidge = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(diag * 2f - 1f)), 10f);

            // 잔주름. 지수를 높게 줘서 아주 가느다란 선으로만 남긴다.
            float fine = Fbm(u * 17f, v * 8f, 2);
            float fineRidge = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(fine * 2f - 1f)), 12f);

            return Mathf.Clamp01(ridge * 0.55f + diagRidge * 0.35f + fineRidge * 0.18f);
        }

        static Texture2D ToTexture(Color[] px, int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static Texture2D ToGray(float[] v, int w, int h)
        {
            var px = new Color32[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(v[i]) * 255f);
                px[i] = new Color32(b, b, b, 255);
            }
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        static void SavePng(string path, Texture2D tex)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
