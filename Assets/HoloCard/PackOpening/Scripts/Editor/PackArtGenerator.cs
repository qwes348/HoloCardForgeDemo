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
            Write("PackWrap", BuildPackWrap(512, 900));

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

        static Layer BuildPackWrap(int w, int h)
        {
            var art = new Color[w * h];
            var depth = new float[w * h];
            var foil = new float[w * h];

            var top    = new Color(0.16f, 0.42f, 0.78f);
            var bottom = new Color(0.05f, 0.06f, 0.24f);
            var accent = new Color(0.95f, 0.30f, 0.42f);
            var gold   = new Color(0.92f, 0.78f, 0.40f);
            var cream  = new Color(0.96f, 0.94f, 0.88f);

            // 상단 8% 는 뜯겨 나가는 스트립
            const float tearLine = 0.92f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    float u = (x + 0.5f) / w;
                    float v = (y + 0.5f) / h;

                    // 세로 그라디언트
                    Color c = Color.Lerp(bottom, top, Mathf.Pow(v, 0.75f));

                    // 대각 광택 띠 세 줄
                    float diag = u * 0.75f + v * 0.66f;
                    for (int k = 0; k < 3; k++)
                    {
                        float center = 0.32f + k * 0.34f;
                        float band = Mathf.Exp(-Mathf.Pow((diag - center) * 9f, 2f));
                        c += new Color(0.55f, 0.65f, 0.85f) * band * 0.30f;
                    }

                    float d = 0.42f + Mathf.Pow(v, 0.75f) * 0.18f;
                    float f = 0.75f;

                    // 중앙 원형 엠블럼
                    float cx = (u - 0.5f) * 2f;
                    float cy = (v - 0.62f) * 2f * ((float)h / w);
                    float r = Mathf.Sqrt(cx * cx + cy * cy);

                    float disc = 1f - Mathf.Clamp01((r - 0.42f) / 0.03f);
                    if (disc > 0f)
                    {
                        c = Color.Lerp(c, bottom * 1.4f, disc * 0.85f);
                        d = Mathf.Lerp(d, 0.66f, disc);
                    }
                    float discRim = 1f - Mathf.Clamp01(Mathf.Abs(r - 0.42f) / 0.022f);
                    if (discRim > 0f)
                    {
                        c = Color.Lerp(c, gold, discRim);
                        d = Mathf.Lerp(d, 0.86f, discRim);
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
                        d = Mathf.Lerp(d, 0.94f, gem);
                        f = Mathf.Lerp(f, 1f, gem);
                    }

                    // 하단 라벨 바
                    if (v < 0.16f)
                    {
                        float bar = 1f - Mathf.Clamp01((Mathf.Abs(v - 0.09f) - 0.035f) / 0.012f);
                        if (bar > 0f)
                        {
                            c = Color.Lerp(c, cream, bar * 0.92f);
                            d = Mathf.Lerp(d, 0.78f, bar);
                            f = Mathf.Lerp(f, 0.2f, bar);
                        }
                    }

                    // 뜯는 선 — 점선과 노치
                    float tear = 1f - Mathf.Clamp01(Mathf.Abs(v - tearLine) / 0.004f);
                    if (tear > 0f)
                    {
                        float dash = Mathf.Repeat(u * 46f, 1f) < 0.55f ? 1f : 0.15f;
                        c = Color.Lerp(c, cream, tear * dash * 0.8f);
                        d = Mathf.Lerp(d, 0.3f, tear * dash);
                    }
                    // 스트립 쪽은 살짝 밝게 구분
                    if (v > tearLine) c *= 1.12f;

                    // 테두리 접합부
                    float edgeU = Mathf.Min(u, 1f - u);
                    float seam = 1f - Mathf.Clamp01(edgeU / 0.035f);
                    c = Color.Lerp(c, bottom * 1.2f, seam * 0.55f);
                    d = Mathf.Lerp(d, 0.30f, seam * 0.7f);

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
