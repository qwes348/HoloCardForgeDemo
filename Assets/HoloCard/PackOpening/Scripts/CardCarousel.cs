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
    ///
    /// 마지막 장에서 한 번 더 넘기면 <see cref="OpenGallery"/> — 뽑은 카드를 전부
    /// 펼쳐 놓는 결과 화면으로 넘어간다. 무한히 순환시키면 "다 봤다" 는 순간이
    /// 없어서 개봉이 안 끝난다.
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

        [Header("Gallery")]
        [Tooltip("마지막 장에서 더 넘기면 뽑은 카드를 전부 펼쳐 보여준다.")]
        public bool galleryAtEnd = true;
        [Range(1, 6)] public int galleryColumns = 5;
        [Tooltip("펼쳤을 때 카드 크기.")]
        public float galleryScale = 0.56f;
        [Tooltip("카드 사이 여백.")]
        public Vector2 galleryGap = new Vector2(0.055f, 0.09f);
        public float galleryTime = 0.5f;
        [Tooltip("한 장씩 차례로 날아가는 간격. 0 이면 한꺼번에 펼쳐져서 밋밋하다.")]
        public float galleryStagger = 0.07f;

        [Header("Gallery Zoom")]
        [Tooltip("결과 화면에서 카드를 클릭하면 확대해서 본다.")]
        public bool galleryZoom = true;
        [Tooltip("확대했을 때 크기.")]
        public float zoomScale = 1f;
        [Tooltip("카메라 쪽으로 당기는 거리. 나머지 카드보다 확실히 앞에 서야 한다.")]
        public float zoomLift = 0.18f;
        [Tooltip("확대 중 나머지 카드를 뒤로 미는 거리.")]
        public float zoomPushBack = 0.06f;
        public float zoomTime = 0.32f;
        [Tooltip("확대 중 나머지 카드의 밝기.")]
        [Range(0f, 1f)] public float zoomDim = 0.45f;

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
        bool _gallery;
        int _zoomed = -1;
        MaterialPropertyBlock _mpb;

        public int Index => _index;
        public int Count => _cards.Count;
        public float CardWidth => _cardWidth;
        public float CardHeight => _cardHeight;
        public bool Sliding => _slide != null && _slide.IsActive() && _slide.IsPlaying();

        public HoloCardController Current =>
            _index >= 0 && _index < _cards.Count ? _cards[_index] : null;

        /// <summary>싣는 순서대로 i 번째 카드. 결과 화면이 등급 표식을 붙일 때 쓴다.</summary>
        public HoloCardController CardAt(int i) =>
            i >= 0 && i < _cards.Count ? _cards[i] : null;

        /// <summary>가운데 카드가 바뀌기 **시작**할 때. 표식·뱃지는 여기서 치운다.</summary>
        public event Action<HoloCardController, int> Changed;

        /// <summary>새 카드가 자리에 앉았을 때. 표식·뱃지·레어 연출이 여기서 뜬다.</summary>
        public event Action<HoloCardController, int> Settled;

        /// <summary>결과 화면(갤러리)에 들어가고 나올 때.</summary>
        public event Action<bool> GalleryChanged;

        public bool InGallery => _gallery;
        public bool Zoomed => _zoomed >= 0;

        /// <summary>결과 화면에서 카드를 확대하거나 풀 때. 풀면 null.</summary>
        public event Action<HoloCardController, int> ZoomChanged;

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
            ClearZoom();
            _gallery = false;
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
            ClearZoom();
            _cards.Clear();
            _index = 0;
            _dragging = false;
            _gallery = false;
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

            // 갤러리에서는 격자 바깥으로 물린다. 뭉치 기준 자리에 두면 화살표
            // 판정이 가운데 카드들 위에 겹쳐서 카드 클릭을 가로챈다.
            float x = _gallery
                ? GalleryHalfWidth() + 0.13f
                : _cardWidth * 0.5f + 0.11f;

            if (rightArrow != null)
                rightArrow.transform.position = basis.TransformPoint(plane + new Vector3(x, 0f, 0f));
            if (leftArrow != null)
                leftArrow.transform.position = basis.TransformPoint(plane + new Vector3(-x, 0f, 0f));
        }

        void ShowArrows(bool on, bool immediate) => ShowArrows(on, on, immediate);

        /// <summary>좌우를 따로 켠다. 갤러리에서는 되돌아가는 쪽만 살아 있다.</summary>
        void ShowArrows(bool left, bool right, bool immediate)
        {
            if (leftArrow != null)
            {
                if (immediate) leftArrow.SetShownImmediate(left); else leftArrow.SetShown(left);
            }
            if (rightArrow != null)
            {
                if (immediate) rightArrow.SetShownImmediate(right); else rightArrow.SetShown(right);
            }
        }

        // ── 넘기기 ───────────────────────────────────────────────────────

        public void Go(int delta)
        {
            if (_cards.Count == 0 || delta == 0) return;
            if (Sliding || Time.time - _lastSlideTime < slideCooldown) return;

            if (_gallery)
            {
                // 확대 중에는 화살표가 **옆 카드로 넘어간다.** 확대를 푸는 게 아니라 —
                // 크게 보다가 옆 것도 크게 보고 싶은 게 자연스럽다. 푸는 건 클릭·ESC.
                if (_zoomed >= 0)
                {
                    int step = Mathf.Clamp(_zoomed + delta, 0, _cards.Count - 1);
                    if (step != _zoomed) ZoomTo(step);
                    return;
                }
                // 그 밖에는 되돌아가는 것만 된다.
                if (delta < 0) CloseGallery();
                return;
            }

            int from = _index;
            int next = _index + delta;

            // 마지막 장을 넘기면 순환하는 대신 결과 화면으로 나간다.
            if (next >= _cards.Count)
            {
                if (galleryAtEnd) { OpenGallery(); return; }
                if (!wrap) return;
                next = 0;
            }
            else if (next < 0)
            {
                // 갤러리가 켜져 있으면 앞쪽 끝은 그냥 막는다. 여기서 순환시키면
                // 마지막 장으로 건너뛰었다가 다시 갤러리로 나가는 길이 생겨 헷갈린다.
                if (galleryAtEnd || !wrap) return;
                next = _cards.Count - 1;
            }
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

        // ── 결과 화면 ────────────────────────────────────────────────────

        /// <summary>
        /// 뽑은 카드를 전부 펼친다. 가챠의 마지막 박자 — "이만큼 나왔다" 를 한눈에
        /// 보여 주는 자리다.
        ///
        /// 한 장씩 <see cref="galleryStagger"/> 간격으로 날아간다. 한꺼번에 펼치면
        /// 그냥 배치가 바뀐 것으로 보이고, 차례로 놓이면 세는 맛이 생긴다.
        /// 뭉치 순서가 등급 오름차순이라 제일 좋은 카드가 마지막에 자리를 잡는다.
        /// </summary>
        public void OpenGallery()
        {
            if (_gallery || _cards.Count == 0) return;
            _gallery = true;
            _lastSlideTime = Time.time;

            PlaceArrows();
            ShowArrows(false, false);
            GalleryChanged?.Invoke(true);

            _slide?.Kill();
            _slide = DOTween.Sequence();

            for (int j = 0; j < _cards.Count; j++)
            {
                HoloCardController c = _cards[j];
                if (c == null) continue;

                c.enabled = false;
                c.transform.DOKill();

                // 뭉치 뒤에 숨어 있던 장은 제자리에서 시작시킨다. 그래야 뭉치에서
                // 뽑혀 나오는 것으로 읽힌다 (없던 데서 튀어나오지 않는다).
                if (!c.gameObject.activeSelf)
                {
                    c.gameObject.SetActive(true);
                    Pose(c, Mathf.Min(SlotOf(j), visibleDepth + 1));
                }

                GallerySlot(j, out Vector3 pos);
                float at = galleryStagger * j;
                _slide.Insert(at, c.transform.DOLocalMove(pos, galleryTime).SetEase(Ease.OutBack, 1.05f));
                _slide.Insert(at, c.transform.DOLocalRotateQuaternion(Quaternion.identity, galleryTime)
                                             .SetEase(Ease.OutCubic));
                _slide.Insert(at, c.transform.DOScale(Vector3.one * galleryScale, galleryTime)
                                             .SetEase(Ease.OutCubic));
            }

            _slide.OnComplete(() =>
            {
                for (int j = 0; j < _cards.Count; j++)
                {
                    HoloCardController c = _cards[j];
                    if (c == null) continue;
                    GallerySlot(j, out Vector3 pos);
                    c.SetHome(pos, Quaternion.identity);
                    // 펼쳐 놓은 카드는 전부 살려 둔다. 각도가 안 변하면 포일이
                    // 죽어서 결과 화면이 인쇄물 사진처럼 보인다.
                    c.enabled = true;
                }
                // 되돌아가는 쪽만 켠다.
                ShowArrows(true, false, false);
            });
        }

        /// <summary>결과 화면에서 다시 뭉치로 돌아간다. 마지막 장이 앞에 온다.</summary>
        public void CloseGallery()
        {
            if (!_gallery) return;
            _gallery = false;
            ClearZoom();
            _index = _cards.Count - 1;
            _lastSlideTime = Time.time;

            PlaceArrows();
            ShowArrows(false, false);
            GalleryChanged?.Invoke(false);

            _slide?.Kill();
            _slide = DOTween.Sequence();

            for (int j = 0; j < _cards.Count; j++)
            {
                HoloCardController c = _cards[j];
                if (c == null) continue;

                c.enabled = false;
                c.transform.DOKill();

                int k = SlotOf(j);
                int target = Mathf.Min(k, visibleDepth + 1);
                float at = galleryStagger * (_cards.Count - 1 - j);

                _slide.Insert(at, c.transform.DOLocalMove(StackPosition(target), galleryTime).SetEase(Ease.InOutCubic));
                _slide.Insert(at, c.transform.DOLocalRotateQuaternion(StackRotation(target), galleryTime).SetEase(Ease.InOutCubic));
                _slide.Insert(at, c.transform.DOScale(StackScale(target), galleryTime).SetEase(Ease.InOutCubic));
            }

            _slide.OnComplete(() =>
            {
                SnapAll();
                if (Current != null) Current.enabled = true;
                ShowArrows(true, false);
                Settled?.Invoke(Current, _index);
            });
        }

        /// <summary>
        /// 격자에서 j 번째 카드의 자리. 마지막 줄은 개수가 모자라도 가운데 정렬한다
        /// — 왼쪽으로 몰아 두면 결과 화면이 한쪽으로 기운 것처럼 보인다.
        /// </summary>
        public void GallerySlot(int j, out Vector3 position)
        {
            int cols = Mathf.Max(1, Mathf.Min(_cards.Count, galleryColumns));
            int rows = Mathf.CeilToInt(_cards.Count / (float)cols);

            int row = j / cols;
            int col = j % cols;
            int inRow = Mathf.Min(_cards.Count - row * cols, cols);

            float cellW = _cardWidth * galleryScale + galleryGap.x;
            float cellH = _cardHeight * galleryScale + galleryGap.y;

            position = new Vector3(
                (col - (inRow - 1) * 0.5f) * cellW,
                -(row - (rows - 1) * 0.5f) * cellH,
                0f);
        }

        /// <summary>격자 절반 폭. 갤러리에서 화살표를 바깥으로 물릴 때 쓴다.</summary>
        float GalleryHalfWidth()
        {
            int cols = Mathf.Max(1, Mathf.Min(_cards.Count, galleryColumns));
            float cellW = _cardWidth * galleryScale + galleryGap.x;
            return (cols - 1) * 0.5f * cellW + _cardWidth * galleryScale * 0.5f;
        }

        // ── 확대 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 결과 화면에서 카드 한 장을 앞으로 끌어내 확대한다.
        ///
        /// 나머지는 어둡게 깔고 살짝 뒤로 민다. 그냥 확대만 하면 뒤의 카드들이
        /// 같은 밝기로 남아서 어느 쪽을 보라는 건지 안 읽힌다.
        ///
        /// 확대한 카드만 컨트롤러를 살려 포인터를 따라 기울게 한다 — 확대해서 보는
        /// 목적이 결국 포일이라, 각도가 안 변하면 크게 볼 이유가 없다.
        /// </summary>
        public void ZoomTo(int j)
        {
            if (!_gallery || j < 0 || j >= _cards.Count) return;
            if (_zoomed == j) return;

            // 다른 카드로 옮길 때 이전 카드의 추적을 반드시 되돌린다. 안 그러면
            // 격자로 돌아간 뒤에도 그 카드만 포인터를 끝까지 따라다닌다.
            if (_zoomed >= 0 && _zoomed < _cards.Count && _cards[_zoomed] != null)
                _cards[_zoomed].trackOutsideBounds = false;

            _zoomed = j;
            ShowArrows(false, false);

            _slide?.Kill();
            _slide = DOTween.Sequence();

            for (int i = 0; i < _cards.Count; i++)
            {
                HoloCardController c = _cards[i];
                if (c == null) continue;

                c.transform.DOKill();
                GallerySlot(i, out Vector3 slot);

                if (i == j)
                {
                    c.enabled = false;
                    _slide.Insert(0f, c.transform.DOLocalMove(new Vector3(0f, 0f, -zoomLift), zoomTime)
                                                 .SetEase(Ease.OutCubic));
                    _slide.Insert(0f, c.transform.DOScale(Vector3.one * zoomScale, zoomTime)
                                                 .SetEase(Ease.OutCubic));
                    _slide.Insert(0f, DimTween(c, 1f));
                }
                else
                {
                    // 자세를 **전부** 되돌린다. 위치만 되돌리면 방금 확대했던 카드가
                    // 큰 채로 격자에 앉아 혼자 튄다 (확대 대상을 옆으로 옮길 때).
                    c.enabled = false;
                    _slide.Insert(0f, c.transform.DOLocalMove(slot + new Vector3(0f, 0f, zoomPushBack), zoomTime)
                                                 .SetEase(Ease.OutCubic));
                    _slide.Insert(0f, c.transform.DOLocalRotateQuaternion(Quaternion.identity, zoomTime)
                                                 .SetEase(Ease.OutCubic));
                    _slide.Insert(0f, c.transform.DOScale(Vector3.one * galleryScale, zoomTime)
                                                 .SetEase(Ease.OutCubic));
                    _slide.Insert(0f, DimTween(c, zoomDim));
                }
            }

            HoloCardController focus = _cards[j];
            _slide.OnComplete(() =>
            {
                if (focus == null) return;
                focus.SetHome(new Vector3(0f, 0f, -zoomLift), Quaternion.identity);
                // 카드 밖으로 나가도 따라오게 한다. 확대 상태에서 카드 위에만
                // 반응하면 조금만 벗어나도 뚝 끊긴다.
                focus.trackOutsideBounds = true;
                focus.enabled = true;
            });

            ZoomChanged?.Invoke(focus, j);
        }

        /// <summary>확대를 풀고 격자로 되돌린다.</summary>
        public void ZoomOut()
        {
            if (_zoomed < 0) return;

            HoloCardController was = _cards[_zoomed];
            if (was != null) was.trackOutsideBounds = false;
            _zoomed = -1;

            _slide?.Kill();
            _slide = DOTween.Sequence();

            for (int i = 0; i < _cards.Count; i++)
            {
                HoloCardController c = _cards[i];
                if (c == null) continue;

                c.enabled = false;
                c.transform.DOKill();
                GallerySlot(i, out Vector3 slot);

                _slide.Insert(0f, c.transform.DOLocalMove(slot, zoomTime).SetEase(Ease.OutCubic));
                _slide.Insert(0f, c.transform.DOLocalRotateQuaternion(Quaternion.identity, zoomTime).SetEase(Ease.OutCubic));
                _slide.Insert(0f, c.transform.DOScale(Vector3.one * galleryScale, zoomTime).SetEase(Ease.OutCubic));
                _slide.Insert(0f, DimTween(c, 1f));
            }

            _slide.OnComplete(() =>
            {
                for (int i = 0; i < _cards.Count; i++)
                {
                    HoloCardController c = _cards[i];
                    if (c == null) continue;
                    GallerySlot(i, out Vector3 slot);
                    c.SetHome(slot, Quaternion.identity);
                    c.enabled = true;
                }
                ShowArrows(true, false, false);
            });

            ZoomChanged?.Invoke(null, -1);
        }

        /// <summary>트윈 없이 확대 상태를 지운다. 리셋 경로에서 쓴다.</summary>
        void ClearZoom()
        {
            if (_zoomed >= 0 && _zoomed < _cards.Count && _cards[_zoomed] != null)
                _cards[_zoomed].trackOutsideBounds = false;
            _zoomed = -1;

            // 밝기를 반드시 되돌린다. MaterialPropertyBlock 은 렌더러에 남으므로
            // 안 지우면 다음 팩의 카드가 어두운 채로 나온다.
            foreach (var c in _cards)
                if (c != null) SetDim(c, 1f);
        }

        Tween DimTween(HoloCardController card, float to)
        {
            float from = 1f;
            var r = card.GetComponent<Renderer>();
            if (r != null)
            {
                _mpb ??= new MaterialPropertyBlock();
                r.GetPropertyBlock(_mpb);
                Color c = _mpb.GetColor(HoloCardIDs.BaseColor);
                // 블록이 비어 있으면 검정이 돌아온다. 그건 "설정 안 됨" 이지 검정이 아니다.
                from = c.maxColorComponent > 0.001f ? c.r : 1f;
            }
            return DOVirtual.Float(from, to, zoomTime, v => SetDim(card, v)).SetEase(Ease.OutCubic);
        }

        void SetDim(HoloCardController card, float value)
        {
            if (card == null) return;
            var r = card.GetComponent<Renderer>();
            if (r == null) return;

            // 컨트롤러도 같은 블록에 _VirtualView 등을 쓴다. Get -> 수정 -> Set 이라
            // 서로 덮어쓰지 않는다.
            _mpb ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(HoloCardIDs.BaseColor, new Color(value, value, value, 1f));
            r.SetPropertyBlock(_mpb);
        }

        // ── 입력 ─────────────────────────────────────────────────────────

        void Update()
        {
            if (!acceptInput || _cards.Count == 0) return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    if (_zoomed >= 0) { ZoomOut(); return; }
                    if (_gallery) { CloseGallery(); return; }
                }
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
                if (!wasDrag) ClickAt(p);
            }
        }

        void ClickAt(Vector2 screenPosition)
        {
            Ray ray = targetCamera.ScreenPointToRay(screenPosition);
            bool hit = Physics.Raycast(ray, out RaycastHit info, 100f);

            if (hit)
            {
                if (rightArrow != null && rightArrow.hitArea != null && info.collider == rightArrow.hitArea) { Go(1); return; }
                if (leftArrow != null && leftArrow.hitArea != null && info.collider == leftArrow.hitArea) { Go(-1); return; }
            }

            if (!_gallery) return;

            // 확대 중에는 어디를 눌러도 풀린다. 확대한 카드를 다시 누르는 것도 포함 —
            // "닫기" 를 따로 찾아야 하면 결과 화면에서 갇힌 느낌이 난다.
            if (_zoomed >= 0) { ZoomOut(); return; }

            if (!galleryZoom || !hit || Sliding) return;
            int j = IndexOfCollider(info.collider);
            if (j >= 0) ZoomTo(j);
        }

        int IndexOfCollider(Collider col)
        {
            if (col == null) return -1;
            for (int j = 0; j < _cards.Count; j++)
                if (_cards[j] != null && col.transform == _cards[j].transform) return j;
            return -1;
        }
    }
}
