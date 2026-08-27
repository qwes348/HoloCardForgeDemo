using UnityEngine;

namespace HoloCard.PackOpening
{
    /// <summary>
    /// 팩을 가르는 빛줄기.
    ///
    /// 레퍼런스에서 이 연출은 0.15초 남짓 살아 있고, 그 짧은 시간에 세 가지가
    /// 동시에 일어난다.
    ///   1. 가로로 뻗은 광선이 팩 폭보다 **길게** 지나간다. 짧으면 그냥 흰 줄이다.
    ///   2. 지나가는 동안 길이가 늘었다가 두께가 줄어든다 (렌즈 플레어처럼).
    ///   3. 절단면에 잠깐 밝은 테두리가 남는다.
    ///
    /// 셰이더 없이 쿼드 하나에 절차 텍스처를 물려 스케일만 굴린다. 파티클로
    /// 만들면 타이밍을 프레임 단위로 못 잡는다.
    /// </summary>
    [AddComponentMenu("Holo Card/Pack Slash")]
    public class PackSlash : MonoBehaviour
    {
        [Header("Refs")]
        public Transform streak;
        public Renderer streakRenderer;
        [Tooltip("절단 직후 잠깐 터지는 섬광. 없으면 생략한다.")]
        public Transform flash;
        public Renderer flashRenderer;

        [Header("Shape")]
        [Tooltip("광선이 최대로 뻗었을 때의 길이. 팩 폭(약 0.7)보다 훨씬 길어야 한다.")]
        public float length = 3.8f;
        public float thickness = 0.13f;
        [Tooltip("광선이 지나가는 총 시간(초).")]
        public float duration = 0.16f;
        [Tooltip("섬광이 남아 있는 시간(초).")]
        public float flashDuration = 0.20f;
        public float flashSize = 0.62f;

        float _t = -1f;
        MaterialPropertyBlock _mpb;
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        Color _streakColor = new Color(1f, 0.96f, 0.80f, 1f);
        Color _flashColor  = new Color(1f, 0.98f, 0.90f, 1f);

        void Awake() => Hide();

        public void Hide()
        {
            _t = -1f;
            if (streak != null) streak.gameObject.SetActive(false);
            if (flash != null) flash.gameObject.SetActive(false);
        }

        /// <param name="localY">팩 로컬 기준 자를 높이. 광선이 이 선을 지난다.</param>
        public void Play(float localY)
        {
            _t = 0f;
            if (streak != null)
            {
                streak.localPosition = new Vector3(0f, localY, -0.09f);
                streak.localRotation = Quaternion.identity;
                streak.gameObject.SetActive(true);
            }
            if (flash != null)
            {
                flash.localPosition = new Vector3(0f, localY, -0.10f);
                flash.gameObject.SetActive(true);
            }
        }

        void Update()
        {
            if (_t < 0f) return;
            _t += Time.deltaTime;

            // ── 광선. 앞 30% 에 길이가 다 뻗고, 나머지 구간에서 두께가 죽는다.
            if (streak != null && streak.gameObject.activeSelf)
            {
                float u = Mathf.Clamp01(_t / Mathf.Max(duration, 1e-3f));
                float grow  = Mathf.Clamp01(u / 0.3f);
                float decay = Mathf.Clamp01((u - 0.25f) / 0.75f);

                float len = Mathf.Lerp(length * 0.25f, length, EaseOutExpo(grow));
                float thick = thickness * (1f - decay * decay);
                streak.localScale = new Vector3(len, Mathf.Max(thick, 1e-4f), 1f);

                SetColor(streakRenderer, _streakColor, (1f - decay) * (0.35f + grow * 0.65f));
                if (u >= 1f) streak.gameObject.SetActive(false);
            }

            // ── 섬광. 광선보다 조금 더 오래 남아 절단면을 태운다.
            if (flash != null && flash.gameObject.activeSelf)
            {
                float u = Mathf.Clamp01(_t / Mathf.Max(flashDuration, 1e-3f));
                float pop = Mathf.Sin(Mathf.Clamp01(u * 3f) * Mathf.PI * 0.5f);
                flash.localScale = Vector3.one * flashSize * Mathf.Lerp(0.35f, 1f, pop);
                SetColor(flashRenderer, _flashColor, Mathf.Pow(1f - u, 1.6f) * 0.75f);
                if (u >= 1f) flash.gameObject.SetActive(false);
            }

            if (_t > Mathf.Max(duration, flashDuration)) _t = -1f;
        }

        void SetColor(Renderer r, Color c, float alpha)
        {
            if (r == null) return;
            _mpb ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(_mpb);
            // 가산 합성이라 알파가 아니라 밝기로 페이드해야 한다.
            _mpb.SetColor(ColorId, new Color(c.r, c.g, c.b, 1f) * Mathf.Max(alpha, 0f));
            r.SetPropertyBlock(_mpb);
        }

        static float EaseOutExpo(float x) => x >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * x);
    }
}
