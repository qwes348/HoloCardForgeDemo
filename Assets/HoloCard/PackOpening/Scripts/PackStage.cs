using UnityEngine;
using UnityEngine.InputSystem;

namespace HoloCard.PackOpening
{
    /// <summary>
    /// 개봉 연출의 무대. 깊이가 다른 판 몇 장을 겹쳐 놓고 포인터에 따라 서로 다른
    /// 양만큼 밀어서 패럴랙스를 만든다.
    ///
    /// 판 하나에 그라디언트를 전부 그려 넣으면 아무리 예뻐도 화면이 평평하다.
    /// 층이 **다른 속도로** 움직여야 깊이가 읽힌다. 그래서 층마다
    /// <see cref="Layer.parallax"/> 를 다르게 준다 (뒤 = 거의 안 움직임,
    /// 앞 = 크게 움직임).
    ///
    /// 레어 카드가 나올 때는 <see cref="FlashRare"/> 로 무대 전체 색을 갈아엎는다.
    /// 배경이 같이 반응하지 않으면 아무리 카드에 파티클을 뿌려도 밋밋하다.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Holo Card/Pack Stage")]
    public class PackStage : MonoBehaviour
    {
        [System.Serializable]
        public class Layer
        {
            public Transform target;
            [Tooltip("포인터 1(화면 끝)당 몇 유닛 밀지.")]
            public float parallax = 0.05f;
            [Tooltip("천천히 떠다니는 폭.")]
            public float drift = 0f;
            public float driftSpeed = 0.2f;
            [HideInInspector] public Vector3 home;
        }

        [Header("Layers")]
        public Layer[] layers = new Layer[0];
        [Tooltip("포인터를 따라가는 속도.")]
        public float follow = 6f;

        [Header("Rare Rainbow")]
        [Tooltip("레어가 나올 때 배경을 통째로 덮는 무지개 판. **카드보다 뒤에** 있어야 한다 " +
                 "— 앞에 두면 가산 합성이라 카드가 통째로 하얗게 날아간다.")]
        public Renderer rainbow;
        [Tooltip("무지개가 흐르는 속도(UV/초).")]
        public float rainbowScroll = 0.8f;
        [Tooltip("최대 밝기. 가산이라 1 근처로 올리면 배경이 흰 판이 된다.")]
        [Range(0f, 1f)] public float rainbowStrength = 0.72f;

        [Header("Tint")]
        [Tooltip("무대 전체에 곱해지는 색. 레어 연출이 이걸 흔든다.")]
        public Renderer[] tinted = new Renderer[0];
        public Color baseTint = Color.white;
        public Color rareTint = new Color(1.35f, 0.92f, 1.05f, 1f);
        public float rareFlashIn = 0.12f;
        public float rareFlashOut = 0.9f;

        Vector2 _pointer;
        float _rare;            // 0..1
        float _rareVel;
        float _rainbowPhase;
        MaterialPropertyBlock _mpb;
        static readonly int TintId = Shader.PropertyToID("_BaseColor");
        static readonly int MapStId = Shader.PropertyToID("_BaseMap_ST");

        void OnEnable()
        {
            CaptureHomes();
            ApplyTint(0f);
        }

        /// <summary>
        /// 각 층의 제자리를 지금 위치로 못 박는다.
        ///
        /// 반드시 층을 다 붙인 **뒤에** 불러야 한다. AddComponent 시점에 OnEnable 이
        /// 먼저 도는데 그때는 layers 가 비어 있어서 제자리가 전부 0 으로 잡히고,
        /// 그 상태로 Update 가 돌면 z 까지 0 으로 뭉개서 층이 전부 한 평면에 겹친다
        /// (배경이 통째로 하얗게 뜬다).
        /// </summary>
        public void CaptureHomes()
        {
            if (layers == null) return;
            foreach (var l in layers)
                if (l != null && l.target != null) l.home = l.target.localPosition;
        }

        /// <summary>레어 등장. 무대 색이 확 올랐다가 천천히 돌아온다.</summary>
        public void FlashRare(float strength = 1f)
        {
            _rare = Mathf.Max(_rare, Mathf.Clamp01(strength));
            _rareVel = Mathf.Clamp01(strength) / Mathf.Max(rareFlashIn, 1e-3f);
        }

        void Update()
        {
            // 에디트 모드에서는 층을 건드리지 않는다. 미리보기 몇 픽셀 얻자고
            // 씬의 트랜스폼을 계속 덮어쓰면 사고만 난다.
            if (!Application.isPlaying) return;

            Vector2 target = Vector2.zero;
            Pointer pointer = Pointer.current;
            if (pointer != null && Screen.width > 0 && Screen.height > 0)
            {
                Vector2 p = pointer.position.ReadValue();
                target = new Vector2(Mathf.Clamp((p.x / Screen.width) * 2f - 1f, -1f, 1f),
                                     Mathf.Clamp((p.y / Screen.height) * 2f - 1f, -1f, 1f));
            }

            float dt = Time.deltaTime;
            _pointer = Vector2.Lerp(_pointer, target, 1f - Mathf.Exp(-follow * dt));

            float t = Time.time;
            foreach (var l in layers)
            {
                if (l == null || l.target == null) continue;
                // 층은 포인터와 **반대로** 민다. 카메라가 움직인 것처럼 읽히는 방향.
                var offset = new Vector3(-_pointer.x * l.parallax, -_pointer.y * l.parallax, 0f);
                if (l.drift > 1e-4f)
                    offset += new Vector3(Mathf.Sin(t * l.driftSpeed) * l.drift,
                                          Mathf.Cos(t * l.driftSpeed * 0.77f) * l.drift * 0.6f, 0f);
                l.target.localPosition = l.home + offset;
            }

            if (_rare > 0f || _rareVel > 0f)
            {
                _rare = Mathf.Clamp01(_rare + _rareVel * dt);
                if (_rare >= 1f) _rareVel = 0f;
                if (_rareVel <= 0f) _rare = Mathf.MoveTowards(_rare, 0f, dt / Mathf.Max(rareFlashOut, 1e-3f));
                _rainbowPhase += dt * rainbowScroll;
                ApplyTint(_rare);
            }
        }

        void ApplyTint(float amount)
        {
            _mpb ??= new MaterialPropertyBlock();

            if (tinted != null)
            {
                Color c = Color.Lerp(baseTint, rareTint, amount);
                foreach (var r in tinted)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(_mpb);
                    _mpb.SetColor(TintId, c);
                    r.SetPropertyBlock(_mpb);
                }
            }

            if (rainbow == null) return;

            // 꺼져 있을 때는 렌더러째 끈다. 가산 판을 밝기 0 으로만 두면 매 프레임
            // 화면 전체를 한 번 더 그리는 값을 그대로 낸다.
            bool on = amount > 0.002f;
            if (rainbow.enabled != on) rainbow.enabled = on;
            if (!on) return;

            // 가산이라 알파가 세기다. 밝기까지 같이 올리면 금방 흰 판이 된다.
            float k = amount * rainbowStrength;
            rainbow.GetPropertyBlock(_mpb);
            _mpb.SetColor(TintId, new Color(1f, 1f, 1f, k));
            _mpb.SetVector(MapStId, new Vector4(1f, 1f, _rainbowPhase, 0f));
            rainbow.SetPropertyBlock(_mpb);
        }
    }
}
