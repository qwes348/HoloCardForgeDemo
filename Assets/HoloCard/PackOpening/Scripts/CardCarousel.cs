using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HoloCard.PackOpening
{
    /// <summary>
    /// 뽑은 카드를 한 장씩 넘겨 보는 캐러셀. **손에 든 카드 뭉치** 모형이다.
    ///
    /// 옆으로 늘어놓고 트랙을 미는 방식이 아니다. 카드는 한 자리에 겹쳐 쌓여
    /// 있고, 넘기면 맨 앞 장이 옆으로 치워지면서 **그 밑에 있던 다음 장**이
    /// 드러난다. 옆에서 새 카드가 들어오는 것과는 읽히는 게 완전히 다르다 —
    /// 전자는 목록을 훑는 느낌, 후자는 내 손의 뭉치를 확인하는 느낌이다.
    ///
    /// 쌓인 카드는 뒤로 갈수록 조금씩 오른쪽·아래로 밀리고, 작아지고, 기운다
    /// (<see cref="stackStep"/>). 완전히 정렬해 두면 한 장짜리와 구별이 안 되고,
    /// 많이 어긋내면 부채꼴이 된다. 밑장이 몇 mm 씩 보이는 정도가 맞다.
    ///
    /// 깊이는 <see cref="visibleDepth"/> 장까지만 켠다. POM 카드는 한 장이 비싸고,
    /// 어차피 네 장 밑은 앞 카드에 완전히 가린다.
    /// </summary>
    [AddComponentMenu("Holo Card/Card Carousel")]
    public class CardCarousel : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("비우면 Camera.main.")]
        public Camera targetCamera;
        public CarouselArrow leftArrow;
        public CarouselArrow rightArrow;

        [Header("Stack")]
        [Tooltip("맨 앞 장 뒤로 몇 장까지 켤지.")]
        [Range(0, 6)] public int visibleDepth = 3;
        [Tooltip("한 장 뒤로 갈 때마다의 어긋남. z 는 앞뒤 순서를 가른다. " +
                 "z 는 맨 앞 장이 기울 때 가장자리가 뒤로 스치는 거리보다 커야 한다 " +
                 "— 카드 반폭 x sin(최대 기울기). 좁게 잡으면 카드를 기울이는 순간 " +
                 "밑장이 앞 장을 뚫고 나온다.")]
        public Vector3 stackStep = new Vector3(0.016f, -0.013f, 0.090f);
        [Tooltip("한 장 뒤로 갈 때마다 기우는 각도.")]
        public float stackTilt = -1.4f;
        [Tooltip("한 장 뒤로 갈 때마다 줄어드는 비율.")]
        public float stackShrink = 0.016f;

        [Header("Sweep")]
        [Tooltip("맨 앞 장이 치워지는 시간.")]
        public float sweepTime = 0.30f;
        [Tooltip("치우는 가속. 손에서 튕겨 나가듯 뒤로 갈수록 빨라져야 한다.")]
        public Ease sweepEase = Ease.InQuad;
        [Tooltip("치워지는 동안 카드가 도는 각도.")]
        public float sweepSpin = 14f;
        [Tooltip("치워지면서 아래로 처지는 거리.")]
        public float sweepDrop = 0.16f;
        [Tooltip("밑장이 앞으로 올라오는 시간.")]
        public float riseTime = 0.34f;
        [Tooltip("치우기가 시작하고 밑장이 올라오기까지의 뜸. 0 이면 같이 움직여서 " +
                 "'밑에 있던 것' 이 아니라 '같이 밀린 것' 으로 읽힌다.")]
        public float riseLag = 0.06f;
        public Ease riseEase = Ease.OutCubic;
        [Tooltip("연달아 넘길 때의 최소 간격.")]
        public float slideCooldown = 0.06f;

        [Header("Input")]
        public bool acceptInput = true;
        [Tooltip("넘기기로 인정할 드래그 거리(화면 폭 비율).")]
        [Range(0.01f, 0.4f)] public float swipeThreshold = 0.06f;
        [Tooltip("끝에서 더 넘기면 반대쪽 끝으로 돌아간다.")]
        public bool wrap = true;

        readonly List<HoloCardController> _cards = new List<HoloCardController>();
        int _index;
        float _cardWidth = 0.64f;
        float _cardHeight = 0.88f;
        Sequence _slide;
        float _lastSlideTime = -99f;

        bool _dragging;
        float _dragStartX;
        bool _dragConsumed;

        public int Index => _index;
        public int Count => _cards.Count;
        public float CardWidth => _cardWidth;
        public float CardHeight => _cardHeight;
        public bool Sliding => _slide != null && _slide.IsActive() && _slide.IsPlaying();

        public HoloCardController Current =>
            _index >= 0 && _index < _cards.Count ? _cards[_index] : null;

        /// <summary>가운데 카드가 바뀌기 **시작**할 때. 표식·뱃지는 여기서 치운다.</summary>
        public event Action<HoloCardController, int> Changed;

        /// <summary>새 카드가 자리에 앉았을 때. 표식·뱃지·레어 연출이 여기서 뜬다.</summary>
        public event Action<HoloCardController, int> Settled;

        void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
        }

        void OnDestroy() => _slide?.Kill();

        // ── 자리 ─────────────────────────────────────────────────────────

        /// <summary>뭉치에서 k 번째(0 = 맨 앞) 장의 자리.</summary>
        public Vector3 StackPosition(int k) => new Vector3(stackStep.x * k, stackStep.y * k, stackStep.z * k);
        public Quaternion StackRotation(int k) => Quaternion.Euler(0f, 0f, stackTilt * k);
        public Vector3 StackScale(int k) => Vector3.one * Mathf.Max(0.2f, 1f - stackShrink * k);

        /// <summary>치워진 카드가 가 있는 자리. 화면 왼쪽 밖.</summary>
        public Vector3 ExitPosition()
        {
            float halfWidth = HalfViewWidth();
            return new Vector3(-(halfWidth + _cardWidth * 1.1f), -sweepDrop, -0.05f);
        }

        public Quaternion ExitRotation() => Quaternion.Euler(0f, 0f, sweepSpin);

        float HalfViewWidth()
        {
            if (targetCamera == null) return 1.6f;
            float distance = Mathf.Abs(transform.position.z - targetCamera.transform.position.z);
            float halfHeight = targetCamera.orthographic
                ? targetCamera.orthographicSize
                : distance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return halfHeight * targetCamera.aspect;
        }

        /// <summary>카드 j 가 지금 뭉치의 몇 번째인지. 0 = 맨 앞.</summary>
        int SlotOf(int j) => _cards.Count == 0 ? 0 : ((j - _index) % _cards.Count + _cards.Count) % _cards.Count;

        // ── 싣기 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 카드를 뭉치에 싣는다. 첫 장은 아직 켜지 않는다 — 개봉 연출이 팩에서
        /// 끌어올린 뒤 <see cref="Begin"/> 로 넘겨받는다.
        /// </summary>
        public void Bind(IList<HoloCardController> cards)
        {
            _slide?.Kill();
            _cards.Clear();
            _index = 0;

            if (cards != null)
                foreach (var c in cards) if (c != null) _cards.Add(c);

            MeasureCard();

            foreach (var c in _cards)
            {
                c.transform.SetParent(transform, false);
                c.transform.DOKill();
                c.enabled = false;
                c.gameObject.SetActive(false);
            }

            PlaceArrows();
            ShowArrows(false, true);
        }

        /// <summary>개봉 연출이 첫 장을 올려놓은 뒤 캐러셀을 넘겨받는다.</summary>
        public void Begin(int index = 0)
        {
            if (_cards.Count == 0) return;

            _index = Mathf.Clamp(index, 0, _cards.Count - 1);
            PlaceArrows();
            SnapAll();

            if (Current != null) Current.enabled = true;
            ShowArrows(_cards.Count > 1, false);
            Settled?.Invoke(Current, _index);
        }

        public void Clear()
        {
            _slide?.Kill();
            foreach (var c in _cards)
            {
                if (c == null) continue;
                c.transform.DOKill();
                c.enabled = false;
                c.gameObject.SetActive(false);
            }
            _cards.Clear();
            _index = 0;
            _dragging = false;
            ShowArrows(false, true);
        }

        /// <summary>트윈 없이 모든 카드를 지금 순서의 제자리에 놓는다.</summary>
        void SnapAll()
        {
            for (int j = 0; j < _cards.Count; j++)
            {
                HoloCardController c = _cards[j];
                if (c == null) continue;

                int k = SlotOf(j);
                bool visible = k <= visibleDepth;
                c.transform.DOKill();
                c.gameObject.SetActive(visible);
                c.enabled = false;
                if (!visible) continue;

                Pose(c, k);
            }
        }

        void Pose(HoloCardController c, int k)
        {
            Transform t = c.transform;
            t.localPosition = StackPosition(k);
            t.localRotation = StackRotation(k);
            t.localScale = StackScale(k);
            // 컨트롤러의 기울기는 이 자세를 기준으로 얹힌다.
            c.SetHome(t.localPosition, t.localRotation);
        }

        void MeasureCard()
        {
            foreach (var c in _cards)
            {
                var r = c != null ? c.GetComponent<Renderer>() : null;
                if (r == null || r.localBounds.size.x <= 1e-4f) continue;
                _cardWidth = r.localBounds.size.x;
                _cardHeight = r.localBounds.size.y;
                return;
            }
        }

        /// <summary>
        /// 화살표는 화면에 붙박이라 카드와 같이 흐르면 안 된다. 그래서 뭉치의
        /// **자식이 아니고**, 자리도 뭉치가 아니라 그 부모 기준으로 잡는다.
        /// </summary>
        void PlaceArrows()
        {
            Transform basis = transform.parent != null ? transform.parent : transform;
            Vector3 plane = new Vector3(0f, transform.localPosition.y, transform.localPosition.z);
            float x = _cardWidth * 0.5f + 0.11f;

            if (rightArrow != null)
                rightArrow.transform.position = basis.TransformPoint(plane + new Vector3(x, 0f, 0f));
            if (leftArrow != null)
                leftArrow.transform.position = basis.TransformPoint(plane + new Vector3(-x, 0f, 0f));
        }

        void ShowArrows(bool on, bool immediate)
        {
            if (leftArrow != null)
            {
                if (immediate) leftArrow.SetShownImmediate(on); else leftArrow.SetShown(on);
            }
            if (rightArrow != null)
            {
                if (immediate) rightArrow.SetShownImmediate(on); else rightArrow.SetShown(on);
            }
        }

        // ── 넘기기 ───────────────────────────────────────────────────────

        public void Go(int delta)
        {
            if (_cards.Count < 2 || delta == 0) return;
            if (Sliding || Time.time - _lastSlideTime < slideCooldown) return;

            int from = _index;
            int next = _index + delta;
            if (wrap) next = (next % _cards.Count + _cards.Count) % _cards.Count;
            else next = Mathf.Clamp(next, 0, _cards.Count - 1);
            if (next == from) return;

            _index = next;
            _lastSlideTime = Time.time;

            ShowArrows(false, false);
            Changed?.Invoke(Current, _index);

            _slide?.Kill();
            _slide = DOTween.Sequence();

            for (int j = 0; j < _cards.Count; j++)
            {
                HoloCardController c = _cards[j];
                if (c == null) continue;

                c.enabled = false;
                c.transform.DOKill();

                // 앞으로 넘기기: 맨 앞 장이 옆으로 치워진다.
                if (delta > 0 && j == from) { SweepOut(c); continue; }

                // 뒤로 넘기기: 방금 치웠던 장이 다시 날아 들어와 맨 앞에 얹힌다.
                if (delta < 0 && j == next) { SweepIn(c); continue; }

                MoveToSlot(c, SlotOf(j));
            }

            _slide.OnComplete(() =>
            {
                SnapAll();
                if (Current != null) Current.enabled = true;
                ShowArrows(true, false);
                Settled?.Invoke(Current, _index);
            });
        }

        void SweepOut(HoloCardController c)
        {
            Transform t = c.transform;
            _slide.Insert(0f, t.DOLocalMove(ExitPosition(), sweepTime).SetEase(sweepEase));
            _slide.Insert(0f, t.DOLocalRotateQuaternion(ExitRotation(), sweepTime).SetEase(sweepEase));
            _slide.InsertCallback(sweepTime, () => c.gameObject.SetActive(false));
        }

        void SweepIn(HoloCardController c)
        {
            Transform t = c.transform;
            c.gameObject.SetActive(true);
            t.localPosition = ExitPosition();
            t.localRotation = ExitRotation();
            t.localScale = Vector3.one;

            _slide.Insert(0f, t.DOLocalMove(StackPosition(0), sweepTime).SetEase(Ease.OutCubic));
            _slide.Insert(0f, t.DOLocalRotateQuaternion(StackRotation(0), sweepTime).SetEase(Ease.OutCubic));
        }

        void MoveToSlot(HoloCardController c, int k)
        {
            Transform t = c.transform;
            bool visible = k <= visibleDepth;

            if (!visible)
            {
                // 뭉치 뒤로 밀려 사라지는 장. 한 칸 더 뒤까지만 움직이고 끈다.
                if (!c.gameObject.activeSelf) return;
                _slide.Insert(riseLag, t.DOLocalMove(StackPosition(visibleDepth + 1), riseTime).SetEase(riseEase));
                _slide.Insert(riseLag, t.DOScale(StackScale(visibleDepth + 1), riseTime).SetEase(riseEase));
                _slide.InsertCallback(riseLag + riseTime, () => c.gameObject.SetActive(false));
                return;
            }

            // 뒤에 숨어 있다가 이제 보이기 시작하는 장은 한 칸 뒤에서 출발시킨다.
            if (!c.gameObject.activeSelf)
            {
                c.gameObject.SetActive(true);
                Pose(c, k + 1);
            }

            _slide.Insert(riseLag, t.DOLocalMove(StackPosition(k), riseTime).SetEase(riseEase));
            _slide.Insert(riseLag, t.DOLocalRotateQuaternion(StackRotation(k), riseTime).SetEase(riseEase));
            _slide.Insert(riseLag, t.DOScale(StackScale(k), riseTime).SetEase(riseEase));
        }

        // ── 입력 ─────────────────────────────────────────────────────────

        void Update()
        {
            if (!acceptInput || _cards.Count < 2) return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) Go(-1);
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) Go(1);
            }

            ReadPointer();
        }

        void ReadPointer()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null || targetCamera == null || Screen.width <= 0) return;

            Vector2 p = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                _dragging = true;
                _dragConsumed = false;
                _dragStartX = p.x;
            }

            // 끄는 도중에 문턱을 넘으면 손을 떼기 전에 바로 넘어간다. 떼기를
            // 기다리면 밀어붙이는 손맛이 안 난다.
            if (_dragging && !_dragConsumed)
            {
                float dx = (p.x - _dragStartX) / Screen.width;
                if (Mathf.Abs(dx) >= swipeThreshold)
                {
                    // 왼쪽으로 밀면 앞 장을 치우는 것 = 다음 장.
                    Go(dx < 0f ? 1 : -1);
                    _dragConsumed = true;
                }
            }

            if (pointer.press.wasReleasedThisFrame)
            {
                bool wasDrag = _dragConsumed;
                _dragging = false;
                _dragConsumed = false;
                if (!wasDrag) ClickArrow(p);
            }
        }

        void ClickArrow(Vector2 screenPosition)
        {
            Ray ray = targetCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 100f)) return;

            if (rightArrow != null && rightArrow.hitArea != null && hit.collider == rightArrow.hitArea) Go(1);
            else if (leftArrow != null && leftArrow.hitArea != null && hit.collider == leftArrow.hitArea) Go(-1);
        }
    }
}
