using UnityEngine;

namespace HoloCard.PackOpening
{
    /// <summary>
    /// 캐러셀 좌우의 넘기기 화살표.
    ///
    /// 레퍼런스의 화살표는 가만히 있지 않는다. 제자리 획 하나가 계속 떠 있고,
    /// 그 위로 두 번째 획이 바깥으로 흘러 나가며 사라지기를 반복한다
    /// (<c>‹</c> → <c>‹‹</c> → <c>‹</c>). 정지한 화살표는 "여기 버튼이 있다" 로만
    /// 읽히지만, 흘러 나가는 획이 있으면 "이쪽으로 밀어라" 가 된다.
    ///
    /// 바깥 방향은 항상 로컬 +X 다. 왼쪽 화살표는 루트의 스케일 x 를 -1 로 뒤집어
    /// 쓴다 (그래서 무대 머티리얼의 Cull Off 가 필요하다).
    /// </summary>
    [AddComponentMenu("Holo Card/Carousel Arrow")]
    public class CarouselArrow : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("제자리에 떠 있는 획.")]
        public Transform blade;
        public Renderer bladeRenderer;
        [Tooltip("바깥으로 흘러 나가며 사라지는 획.")]
        public Transform ghost;
        public Renderer ghostRenderer;
        [Tooltip("클릭 판정용. 획보다 넉넉해야 누를 만하다.")]
        public Collider hitArea;

        [Header("Pulse")]
        [Tooltip("고스트가 바깥으로 흘러 나가는 거리.")]
        public float travel = 0.10f;
        [Tooltip("한 주기 길이(초).")]
        public float period = 1.25f;
        [Tooltip("그중 고스트가 살아 있는 시간(초). 나머지는 쉰다.")]
        public float pulseSpan = 0.58f;

        [Header("Look")]
        public Color color = new Color(1f, 1f, 1f, 0.88f);
        [Tooltip("숨기고 나타나는 데 걸리는 시간. 슬라이드 중에는 화살표를 치운다.")]
        public float fadeTime = 0.12f;

        float _shown = 1f;
        float _target = 1f;
        float _phase;
        Vector3 _ghostHome;
        MaterialPropertyBlock _mpb;
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            if (ghost != null) _ghostHome = ghost.localPosition;
        }

        /// <summary>슬라이드 중에는 꺼 둔다. 같이 흐르면 화면이 지저분해진다.</summary>
        public void SetShown(bool on) => _target = on ? 1f : 0f;

        /// <summary>페이드 없이 즉시. 리셋에서 쓴다.</summary>
        public void SetShownImmediate(bool on)
        {
            _target = _shown = on ? 1f : 0f;
            _phase = 0f;
            Apply();
        }

        void OnEnable() => Apply();

        void Update()
        {
            float dt = Time.deltaTime;

            _shown = fadeTime > 1e-4f
                ? Mathf.MoveTowards(_shown, _target, dt / fadeTime)
                : _target;

            // 숨어 있는 동안 주기를 굴리면 다시 나타날 때 아무 데서나 시작한다.
            if (_shown > 0.001f)
            {
                _phase += dt;
                if (_phase >= period) _phase -= period;
            }
            else _phase = 0f;

            Apply();
        }

        void Apply()
        {
            if (hitArea != null && hitArea.enabled != _shown > 0.5f)
                hitArea.enabled = _shown > 0.5f;

            SetColor(bladeRenderer, color.a * _shown);

            if (ghost == null) return;

            float span = Mathf.Max(pulseSpan, 1e-3f);
            float g = Mathf.Clamp01(_phase / span);

            // 처음엔 빠르게 벌어졌다가 끝에서 느려지며 사라진다.
            float out01 = 1f - Mathf.Pow(1f - g, 2.2f);
            ghost.localPosition = _ghostHome + new Vector3(travel * out01, 0f, 0f);

            // 떴다 사라진다. 그냥 1 에서 시작하면 아직 제자리 획과 겹쳐 있는
            // 순간에 알파가 두 배로 겹쳐서 획이 한 번 두꺼워졌다 갈라진다.
            float alpha = _phase < span ? Mathf.Sin(Mathf.PI * Mathf.Pow(g, 0.7f)) : 0f;
            SetColor(ghostRenderer, color.a * alpha * 0.9f * _shown);
        }

        void SetColor(Renderer r, float alpha)
        {
            if (r == null) return;
            _mpb ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorId, new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha)));
            r.SetPropertyBlock(_mpb);
        }
    }
}
