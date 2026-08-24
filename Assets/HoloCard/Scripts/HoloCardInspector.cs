using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HoloCard
{
    /// <summary>
    /// 카드를 클릭하면 카메라 앞으로 끌어와 크게 보여준다. 한 번 더 클릭하거나
    /// 빈 곳을 클릭하면 제자리로 돌아간다.
    ///
    /// 확대된 카드는 화면 어디서든 포인터를 따라가고 기울기 범위도 넓어진다.
    /// 갤러리에서 작게 볼 때는 잘 안 보이는 패럴랙스 깊이와 글리터가
    /// 이 상태에서 제대로 드러난다.
    ///
    /// 조작:
    ///   좌클릭      카드 선택 / 해제
    ///   ESC         해제
    ///   ← →         확대 상태에서 이웃 카드로 이동
    /// </summary>
    [AddComponentMenu("Holo Card/Holo Card Inspector")]
    public class HoloCardInspector : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("비우면 Camera.main.")]
        public Camera targetCamera;
        [Tooltip("비우면 씬에 있는 HoloCardController 를 전부 찾는다.")]
        public List<HoloCardController> cards = new List<HoloCardController>();

        [Header("Focus Pose")]
        [Tooltip("카메라 앞 어느 거리에 띄울지.")]
        public float focusDistance = 1.2f;
        [Tooltip("확대된 카드가 화면 높이의 몇 %를 차지할지.")]
        [Range(0.3f, 1f)] public float focusHeightRatio = 0.84f;
        [Tooltip("이동에 걸리는 시간(초).")]
        public float transitionTime = 0.45f;

        [Header("Focused Card")]
        [Tooltip("확대 상태의 최대 기울기(도). 크게 주면 POM 이 그레이징 각에서 늘어난다.")]
        public float focusedTiltAngle = 14f;
        [Tooltip("확대 상태에서 카드 밖으로 나가도 포인터를 따라간다.")]
        public bool trackPointerAnywhere = true;

        [Header("Other Cards")]
        [Tooltip("확대 중일 때 나머지 카드의 밝기.")]
        [Range(0f, 1f)] public float dimAmount = 0.28f;
        [Tooltip("확대 중일 때 나머지 카드를 뒤로 미는 거리.")]
        public float pushBack = 0.4f;

        class Entry
        {
            public HoloCardController controller;
            public Transform tr;
            public Renderer renderer;

            public Vector3 homePosition;
            public Quaternion homeRotation;
            public Vector3 homeScale;
            public float cardHeight;

            // 원래 컨트롤러 설정 (확대 해제 시 되돌린다)
            public float originalTilt;
            public bool  originalTrackOutside;

            // 전환 보간
            public Vector3 fromPosition, toPosition;
            public Quaternion fromRotation, toRotation;
            public Vector3 fromScale, toScale;
            public float fromDim, toDim;
        }

        readonly List<Entry> _entries = new List<Entry>();
        MaterialPropertyBlock _mpb;

        int _focused = -1;
        float _t = 1f;          // 전환 진행도 0..1
        bool _dirty;

        public int FocusedIndex => _focused;
        public bool HasFocus => _focused >= 0;

        /// <summary>어떤 카드가 확대됐는지 알린다. 해제되면 null.</summary>
        public event System.Action<HoloCardController> FocusChanged;

        /// <summary>등록된 카드 목록. 순서는 화면 배치를 따른다(위 줄부터, 줄 안에서는 왼쪽부터).</summary>
        public int CardCount => _entries.Count;
        public HoloCardController CardAt(int index) =>
            index >= 0 && index < _entries.Count ? _entries[index].controller : null;

        /// <summary>
        /// 카드의 원래 자리를 갱신한다. 연출이 카드를 옮겼을 때 호출해야
        /// 확대 해제 시 새 자리로 돌아간다.
        /// </summary>
        public void SetHome(HoloCardController card, Vector3 localPosition, Quaternion localRotation)
        {
            foreach (Entry e in _entries)
            {
                if (e.controller != card) continue;
                e.homePosition = localPosition;
                e.homeRotation = localRotation;
                if (_focused < 0)
                {
                    e.toPosition = localPosition;
                    e.toRotation = localRotation;
                    e.controller.SetHome(localPosition, localRotation);
                    e.tr.localPosition = localPosition;
                }
                return;
            }
        }

        void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            _mpb = new MaterialPropertyBlock();

            if (cards.Count == 0)
                cards.AddRange(FindObjectsByType<HoloCardController>(FindObjectsSortMode.None));

            CollectEntries();
            SnapAllToHome();
        }

        /// <summary>cards 목록에서 항목을 만들고 화면 배치 순으로 정렬한다.</summary>
        void CollectEntries()
        {
            foreach (var c in cards)
            {
                if (c == null) continue;
                var mesh = c.GetComponent<HoloCardMesh>();
                _entries.Add(new Entry
                {
                    controller = c,
                    tr = c.transform,
                    renderer = c.GetComponent<Renderer>(),
                    homePosition = c.transform.localPosition,
                    homeRotation = c.transform.localRotation,
                    homeScale = c.transform.localScale,
                    cardHeight = mesh != null ? mesh.height : 0.88f,
                    originalTilt = c.maxTiltAngle,
                    originalTrackOutside = c.trackOutsideBounds,
                });
            }

            // ← → 이동이 화면 배치를 따라가도록 위쪽 줄부터, 줄 안에서는 왼쪽부터.
            // 부채꼴 배치는 같은 줄이어도 y 가 조금씩 달라서 허용 오차로 줄을 묶는다.
            const float rowTolerance = 0.3f;
            _entries.Sort((a, b) =>
            {
                float dy = b.tr.position.y - a.tr.position.y;
                if (Mathf.Abs(dy) > rowTolerance) return dy > 0f ? 1 : -1;
                return a.tr.position.x.CompareTo(b.tr.position.x);
            });
        }

        void Update()
        {
            HandleInput();
            Advance(Time.deltaTime);
        }

        // ── 입력 ─────────────────────────────────────────────────────────

        void HandleInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) Focus(-1);
                if (HasFocus)
                {
                    if (keyboard.leftArrowKey.wasPressedThisFrame)  Focus(Wrap(_focused - 1));
                    if (keyboard.rightArrowKey.wasPressedThisFrame) Focus(Wrap(_focused + 1));
                }
            }

            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;

            int hit = Pick(pointer.position.ReadValue());
            if (hit < 0)          Focus(-1);          // 빈 곳 → 해제
            else if (hit == _focused) Focus(-1);      // 같은 카드 → 해제
            else                  Focus(hit);
        }

        int Wrap(int i) => _entries.Count == 0 ? -1 : (i % _entries.Count + _entries.Count) % _entries.Count;

        /// <summary>포인터 아래의 카드 인덱스. 없으면 -1.</summary>
        int Pick(Vector2 screenPosition)
        {
            if (targetCamera == null) return -1;

            Ray ray = targetCamera.ScreenPointToRay(screenPosition);

            // 확대된 카드는 다른 카드 앞에 있으니 자연스럽게 먼저 맞는다.
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f)) return -1;

            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].tr == hit.transform) return i;

            return -1;
        }

        // ── 포커스 ───────────────────────────────────────────────────────

        public void Focus(int index)
        {
            if (index == _focused) return;
            if (index >= _entries.Count) index = -1;

            _focused = index;
            _dirty = true;
            _t = 0f;

            for (int i = 0; i < _entries.Count; i++)
            {
                Entry e = _entries[i];

                // 현재 위치에서 출발해야 전환 중에 끊기지 않는다.
                e.fromPosition = e.controller.HomePosition;
                e.fromRotation = e.controller.HomeRotation;
                e.fromScale = e.tr.localScale;
                e.fromDim = e.toDim;

                bool isFocused = i == _focused;

                if (isFocused)
                {
                    ComputeFocusPose(e, out e.toPosition, out e.toRotation, out e.toScale);
                    e.toDim = 1f;
                    e.controller.maxTiltAngle = focusedTiltAngle;
                    e.controller.trackOutsideBounds = trackPointerAnywhere;
                }
                else
                {
                    e.toPosition = e.homePosition;
                    e.toRotation = e.homeRotation;
                    e.toScale = e.homeScale;
                    e.toDim = HasFocus ? dimAmount : 1f;

                    if (HasFocus && pushBack > 0f)
                        e.toPosition += ToLocalDirection(e.tr, targetCamera.transform.forward) * pushBack;

                    e.controller.maxTiltAngle = e.originalTilt;
                    e.controller.trackOutsideBounds = e.originalTrackOutside;
                }
            }

            FocusChanged?.Invoke(_focused >= 0 ? _entries[_focused].controller : null);
        }

        /// <summary>씬이 만들어진 뒤 카드를 넣거나 뺐을 때 다시 수집한다.</summary>
        public void Rescan()
        {
            cards.Clear();
            cards.AddRange(FindObjectsByType<HoloCardController>(FindObjectsSortMode.None));
            _entries.Clear();
            _focused = -1;
            CollectEntries();
            SnapAllToHome();
        }

        void ComputeFocusPose(Entry e, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            Transform cam = targetCamera.transform;

            Vector3 worldPosition = cam.position + cam.forward * focusDistance;
            // 카드 앞면은 -Z 를 본다. forward 를 카메라 시선과 맞추면 앞면이 이쪽을 향한다.
            Quaternion worldRotation = Quaternion.LookRotation(cam.forward, cam.up);

            Transform parent = e.tr.parent;
            position = parent != null ? parent.InverseTransformPoint(worldPosition) : worldPosition;
            rotation = parent != null ? Quaternion.Inverse(parent.rotation) * worldRotation : worldRotation;

            // 화면 높이의 focusHeightRatio 만큼 차지하도록 스케일을 역산한다.
            float visibleHeight = targetCamera.orthographic
                ? targetCamera.orthographicSize * 2f
                : 2f * focusDistance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);

            float parentScale = parent != null ? parent.lossyScale.y : 1f;
            float wanted = visibleHeight * focusHeightRatio / Mathf.Max(e.cardHeight * parentScale, 1e-4f);
            scale = Vector3.one * wanted;
        }

        static Vector3 ToLocalDirection(Transform tr, Vector3 worldDirection)
        {
            return tr.parent != null ? tr.parent.InverseTransformDirection(worldDirection) : worldDirection;
        }

        // ── 전환 ─────────────────────────────────────────────────────────

        void Advance(float dt)
        {
            if (!_dirty) return;

            if (transitionTime <= 0f) _t = 1f;
            else _t = Mathf.Min(_t + dt / transitionTime, 1f);

            float k = Mathf.SmoothStep(0f, 1f, _t);

            foreach (Entry e in _entries)
            {
                Vector3 position = Vector3.Lerp(e.fromPosition, e.toPosition, k);
                Quaternion rotation = Quaternion.Slerp(e.fromRotation, e.toRotation, k);

                // 컨트롤러가 이 기준 위에 기울기를 얹는다.
                e.controller.SetHome(position, rotation);
                e.tr.localPosition = position;
                e.tr.localScale = Vector3.Lerp(e.fromScale, e.toScale, k);

                ApplyDim(e, Mathf.Lerp(e.fromDim, e.toDim, k));
            }

            if (_t >= 1f) _dirty = false;
        }

        void ApplyDim(Entry e, float value)
        {
            if (e.renderer == null) return;

            // 컨트롤러도 같은 블록을 쓰므로 Get -> 수정 -> Set 으로 서로 보존한다.
            e.renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(HoloCardIDs.BaseColor, new Color(value, value, value, 1f));
            e.renderer.SetPropertyBlock(_mpb);
        }

        void SnapAllToHome()
        {
            foreach (Entry e in _entries)
            {
                e.toDim = 1f;
                e.controller.SetHome(e.homePosition, e.homeRotation);
                e.tr.localPosition = e.homePosition;
                e.tr.localScale = e.homeScale;
                ApplyDim(e, 1f);
            }
        }
    }
}
