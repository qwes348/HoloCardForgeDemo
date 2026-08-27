using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HoloCard.PackOpening
{
    /// <summary>
    /// 카드팩 개봉 연출.
    ///
    ///   Idle       팩이 떠 있고 포인터를 따라 기운다. 클릭하면 시작.
    ///   Tearing    광선이 팩을 가르고 상단 스트립이 날아간다. 팩은 아래로 내려앉는다.
    ///   Revealing  첫 카드가 팩 입구에서 솟아오르며 커지고, 팩은 그 자리에서 사라진다.
    ///   Browsing   <see cref="CardCarousel"/> 가 받아서 한 장씩 넘겨 본다.
    ///
    /// 카드가 팩 **속에서** 나와야 한다. 그래서 카드를 팩 앞이 아니라 봉지 두께
    /// 안쪽(<see cref="revealCardDepth"/>)에 세운다. 팩 앞면이 불투명하니 깊이만으로
    /// 아랫동강이 가려지고, 입구 위로 올라온 부분부터 보이기 시작한다. 앞에 두면
    /// 카드가 처음부터 통째로 보여서 "꺼낸다" 가 아니라 "떠 있다" 가 된다.
    ///
    /// R 키로 다시 뽑는다.
    /// </summary>
    [AddComponentMenu("Holo Card/Pack Opening Director")]
    public class PackOpeningDirector : MonoBehaviour
    {
        public enum Stage { Idle, Tearing, Revealing, Browsing }

        [Header("Scene")]
        public CardPack pack;
        public Transform cardPool;
        public CardCarousel carousel;
        [Tooltip("카드 밑의 ◇/★ 표식과 NEW 뱃지.")]
        public RarityDisplay rarityDisplay;
        public Camera targetCamera;
        public ParticleSystem tearBurst;
        public ParticleSystem rareBurst;
        [Tooltip("배경 층. 레어가 나올 때 색이 흔들린다.")]
        public PackStage stage;
        [Tooltip("팩을 가르는 빛줄기.")]
        public PackSlash slash;

        [Header("Pull")]
        [Tooltip("한 팩에서 나오는 카드 수.")]
        [Range(1, 10)] public int cardsPerPack = 5;
        [Tooltip("앞쪽 자리(마지막 두 장을 뺀 나머지)의 등급 확률.")]
        public RarityOdds commonSlots = RarityOdds.Flat(72f, 24f, 4f, 0f, 0f, 0f, 0f);
        [Tooltip("끝에서 두 번째 자리. 여기서부터 판이 열린다.")]
        public RarityOdds fourthSlot = RarityOdds.Flat(0f, 46f, 38f, 12f, 3f, 1f, 0f);
        [Tooltip("마지막 자리. 절정이라 가장 후하다.")]
        public RarityOdds finalSlot = RarityOdds.Flat(0f, 0f, 30f, 34f, 22f, 10f, 4f);
        [Tooltip("마지막 카드가 최소 이 등급은 되도록 보장한다. Common 이면 보장 없음.")]
        public CardRarity guaranteedFinale = CardRarity.ArtRare;
        [Tooltip("이 등급부터 무지개 번쩍임·스파클 폭발·NEW 뱃지가 붙는다.")]
        public CardRarity rareThreshold = CardRarity.ArtRare;

        [Header("Pack Idle")]
        public float bobAmplitude = 0.035f;
        public float bobSpeed = 1.2f;
        public float packTiltAngle = 14f;
        public float packFollow = 7f;

        [Header("Tearing")]
        [Tooltip("광선이 지나간 뒤 조각이 떨어지기까지의 뜸. 이게 0 이면 무엇이 잘랐는지 안 읽힌다.")]
        public float slashHold = 0.13f;
        [Tooltip("잘린 윗동강이 날아가는 시간.")]
        public float stripFlyTime = 0.85f;
        [Tooltip("스트립이 날아가는 동안 팩이 내려앉는 거리. 입구가 화면 가운데보다 " +
                 "아래로 내려가야 카드가 솟아오를 자리가 생긴다.")]
        public float packSinkY = 0.72f;
        public float packSinkTime = 0.42f;

        [Header("Reveal")]
        [Tooltip("팩이 내려앉고 카드가 나오기까지의 뜸.")]
        public float revealDelay = 0.16f;
        [Tooltip("카드가 입구에서 제자리까지 솟는 시간.")]
        public float riseTime = 0.32f;
        [Tooltip("입구를 갓 지날 때의 카드 크기. 봉지 폭보다 좁아 보여야 한다.")]
        [Range(0.3f, 1f)] public float revealStartScale = 0.66f;
        [Tooltip("시작 위치를 입구보다 이만큼 더 내린다. 0 이면 첫 프레임부터 카드 윗변이 보인다.")]
        public float revealTuck = 0.035f;
        [Tooltip("카드를 세우는 봉지 두께 안쪽 깊이. 앞면보다 뒤, 뒷면보다 앞이어야 한다.")]
        public float revealCardDepth = 0.006f;
        [Tooltip("카드가 얼마쯤 나왔을 때 팩이 빠지기 시작할지 (솟는 시간 비율).")]
        [Range(0f, 1f)] public float packExitAt = 0.26f;
        [Tooltip("팩이 화면 밖 아래로 빠지는 데 걸리는 시간.")]
        public float packExitTime = 0.46f;
        [Tooltip("빠져나가는 거리. 화면 아래 끝을 넉넉히 넘겨야 사라지는 순간이 안 보인다.")]
        public float packExitDrop = 1.6f;
        [Tooltip("빠지면서 도는 각도. 살짝 기울어야 떨어지는 무게가 붙는다.")]
        public float packExitSpin = -22f;

        [Header("Camera")]
        [Tooltip("팩을 보여줄 때의 카메라 거리.")]
        public float packCameraDistance = 3.3f;
        [Tooltip("카드를 볼 때의 거리. 카드가 나오는 동안 여기까지 들어온다.")]
        public float carouselCameraDistance = 2.35f;
        public float cameraDollyTime = 0.55f;

        [Header("Rare")]
        [Tooltip("레어 카드가 자리에 앉을 때의 배경 번쩍임 세기.")]
        [Range(0f, 1f)] public float rareFlash = 1f;

        Stage _stage = Stage.Idle;
        readonly List<HoloCardController> _pulled = new List<HoloCardController>();
        readonly List<HoloCardInfo> _pool = new List<HoloCardInfo>();
        Sequence _sequence;

        Vector3 _packHome;
        Quaternion _packHomeRotation;
        Vector3 _stripHome;
        Quaternion _stripHomeRotation;
        Vector2 _packTilt;

        public Stage Current => _stage;

        void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;

            if (cardPool != null)
                cardPool.GetComponentsInChildren(true, _pool);

            if (pack != null)
            {
                // CardPack.OnEnable 이 아직 안 돌았을 수 있어서 조각을 직접 보장한다.
                // Awake 순서는 오브젝트마다 정해져 있지 않다.
                pack.Rebuild();

                _packHome = pack.transform.localPosition;
                _packHomeRotation = pack.transform.localRotation;
                _stripHome = pack.StripLocalHome;
                _stripHomeRotation = Quaternion.identity;
            }

            if (carousel != null)
            {
                carousel.Settled -= OnCardSettled;
                carousel.Settled += OnCardSettled;
                carousel.Changed -= OnCardChanged;
                carousel.Changed += OnCardChanged;
                carousel.GalleryChanged -= OnGalleryChanged;
                carousel.GalleryChanged += OnGalleryChanged;
                carousel.ZoomChanged -= OnZoomChanged;
                carousel.ZoomChanged += OnZoomChanged;
            }
        }

        void Start() => ResetToIdle();

        void OnDestroy()
        {
            _sequence?.Kill();
            if (carousel != null)
            {
                carousel.Settled -= OnCardSettled;
                carousel.Changed -= OnCardChanged;
                carousel.GalleryChanged -= OnGalleryChanged;
                carousel.ZoomChanged -= OnZoomChanged;
            }
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame &&
                _stage != Stage.Tearing && _stage != Stage.Revealing)
                ResetToIdle();

            if (_stage == Stage.Idle)
            {
                DrivePackIdle();

                var pointer = Pointer.current;
                if (pointer != null && pointer.press.wasPressedThisFrame)
                    BeginOpen();
            }
        }

        // ── Idle ─────────────────────────────────────────────────────────

        /// <summary>팩을 공중에 띄우고 포인터 쪽으로 기울인다.</summary>
        void DrivePackIdle()
        {
            if (pack == null) return;

            Vector2 target = Vector2.zero;
            Pointer pointer = Pointer.current;
            if (pointer != null && targetCamera != null)
            {
                Vector2 p = pointer.position.ReadValue();
                target = new Vector2(
                    Mathf.Clamp((p.x / Screen.width) * 2f - 1f, -1f, 1f),
                    Mathf.Clamp((p.y / Screen.height) * 2f - 1f, -1f, 1f));
            }

            _packTilt = Vector2.Lerp(_packTilt, target, 1f - Mathf.Exp(-packFollow * Time.deltaTime));

            float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            pack.transform.localPosition = _packHome + new Vector3(0f, bob, 0f);
            pack.transform.localRotation = _packHomeRotation * Quaternion.Euler(
                _packTilt.y * packTiltAngle,
                -_packTilt.x * packTiltAngle,
                -_packTilt.x * packTiltAngle * 0.25f);
        }

        // ── 개봉 ─────────────────────────────────────────────────────────

        public void BeginOpen()
        {
            if (_stage != Stage.Idle || pack == null) return;

            _stage = Stage.Tearing;
            PickCards();
            if (carousel != null) carousel.Bind(_pulled);

            _sequence?.Kill();
            _sequence = DOTween.Sequence();

            Transform strip = pack.Strip;
            if (strip != null)
            {
                // ── 광선이 먼저 지나가고, 뜸을 들인 뒤에 조각이 떨어진다.
                //    동시에 터뜨리면 "빛이 갈랐다" 가 아니라 그냥 같이 터진 걸로 읽힌다.
                _sequence.AppendCallback(() =>
                {
                    if (slash != null) slash.Play(pack.MouthLocalY);
                    if (tearBurst != null) { tearBurst.transform.position = MouthWorldPosition(); tearBurst.Play(true); }
                });
                // 베인 순간 팩이 한 번 움찔한다.
                _sequence.Join(pack.transform.DOPunchPosSafe());
                _sequence.AppendInterval(slashHold);

                // 윗동강이 3D 로 텀블링하며 우상단으로 날아가 작아진다.
                _sequence.Append(strip.DOLocalMove(_stripHome + new Vector3(0.62f, 1.55f, -0.4f), stripFlyTime)
                                      .SetEase(Ease.OutQuad));
                _sequence.Join(strip.DOLocalRotate(new Vector3(-430f, 200f, -150f), stripFlyTime,
                                                   RotateMode.LocalAxisAdd).SetEase(Ease.OutQuad));
                _sequence.Join(strip.DOScale(Vector3.one * 0.32f, stripFlyTime).SetEase(Ease.InQuad));

                // 뜯긴 팩이 그 무게로 내려앉는다. 스트립이 아직 날아가는 중에
                // 겹쳐서 시작해야 두 동작이 한 사건으로 읽힌다.
                _sequence.Join(pack.transform.DOLocalMoveY(_packHome.y - packSinkY, packSinkTime)
                                             .SetEase(Ease.OutCubic));
                _sequence.AppendCallback(() => strip.gameObject.SetActive(false));
            }

            _sequence.AppendInterval(revealDelay);
            _sequence.AppendCallback(() => _stage = Stage.Revealing);
            AppendReveal(_sequence);
            _sequence.AppendCallback(EnterBrowsing);
        }

        /// <summary>
        /// 이번 팩에 나올 카드를 뽑는다.
        ///
        /// 자리마다 다른 확률표를 쓴다. 전부 같은 표로 굴리면 다섯 장이 그냥
        /// 무작위 다섯 장이라 "점점 좋아진다" 는 흐름이 안 생긴다. 앞자리는
        /// ◇ 위주, 끝에서 두 번째부터 열리고, 마지막 자리가 판을 뒤집는다.
        ///
        /// 뽑은 뒤 등급 오름차순으로 세운다. 넘겨 볼 때 절정이 마지막에 와야
        /// 한다 — 첫 장이 제일 좋으면 나머지는 소화 과정이 된다.
        /// </summary>
        void PickCards()
        {
            _pulled.Clear();
            if (_pool.Count == 0) return;

            // 등급별 바구니. 풀에 없는 등급이 있어도 뽑기표는 그 등급을 굴리므로
            // 가장 가까운 등급으로 대신 집는다.
            int tiers = (int)CardRarity.Immersive + 1;
            var buckets = new List<HoloCardInfo>[tiers];
            for (int i = 0; i < tiers; i++) buckets[i] = new List<HoloCardInfo>();
            foreach (var info in _pool) buckets[(int)info.rarity].Add(info);

            var chosen = new List<HoloCardInfo>();
            for (int slot = 0; slot < cardsPerPack; slot++)
            {
                bool last = slot == cardsPerPack - 1;
                CardRarity want = OddsFor(slot).Roll();
                if (last && want < guaranteedFinale) want = guaranteedFinale;

                // 절정 자리는 못 채우면 **위로** 올려 잡는다. 아래로 내리면
                // 보장이 깨진다.
                HoloCardInfo pick = TakeNearest(buckets, want, preferUp: last);
                if (pick != null) chosen.Add(pick);
            }

            chosen.Sort((a, b) => ((int)a.rarity).CompareTo((int)b.rarity));

            foreach (var info in chosen)
            {
                var controller = info.GetComponent<HoloCardController>();
                if (controller != null) _pulled.Add(controller);
            }
        }

        RarityOdds OddsFor(int slot)
        {
            if (slot >= cardsPerPack - 1) return finalSlot;
            if (slot >= cardsPerPack - 2) return fourthSlot;
            return commonSlots;
        }

        /// <summary>
        /// 원하는 등급에서 한 장 꺼낸다. 비어 있으면 가장 가까운 등급으로
        /// 번져 나가며 찾는다. 꺼낸 카드는 바구니에서 빼므로 한 팩에 같은 카드가
        /// 두 번 나오지 않는다.
        /// </summary>
        static HoloCardInfo TakeNearest(List<HoloCardInfo>[] buckets, CardRarity want, bool preferUp)
        {
            int n = buckets.Length;
            int w = Mathf.Clamp((int)want, 0, n - 1);

            for (int step = 0; step < n; step++)
            {
                int first = preferUp ? w + step : w - step;
                int second = preferUp ? w - step : w + step;

                if (first >= 0 && first < n && buckets[first].Count > 0) return Draw(buckets[first]);
                if (second >= 0 && second < n && buckets[second].Count > 0) return Draw(buckets[second]);
            }
            return null;
        }

        static HoloCardInfo Draw(List<HoloCardInfo> from)
        {
            int k = Random.Range(0, from.Count);
            HoloCardInfo picked = from[k];
            from.RemoveAt(k);
            return picked;
        }

        // ── 첫 카드가 솟아오른다 ─────────────────────────────────────────

        void AppendReveal(Sequence sequence)
        {
            if (carousel == null || _pulled.Count == 0) return;

            HoloCardController card = _pulled[0];
            Transform track = carousel.transform;
            Transform t = card.transform;

            sequence.AppendCallback(() =>
            {
                // 팩이 다 내려앉은 **뒤에** 입구를 물어야 맞다. 시퀀스를 짤 때
                // 계산해 두면 아직 안 내려간 자리가 잡힌다.
                float half = CardHalfHeight(card) * revealStartScale;
                Vector3 start = track.InverseTransformPoint(MouthWorldPosition());
                start.y -= half + revealTuck;
                start.z = revealCardDepth;

                card.gameObject.SetActive(true);
                card.enabled = false;                       // 트윈이 끝날 때까지 컨트롤러는 쉰다
                t.localPosition = start;
                t.localRotation = Quaternion.identity;      // 앞면이 카메라를 본다
                t.localScale = Vector3.one * revealStartScale;
                card.SetHome(start, Quaternion.identity);
            });

            // 솟기 시작하는 시각을 **미리** 잡아 둔다. 나중에 Duration 에서 빼면
            // 같이 Join 한 카메라 돌리(더 길다)까지 세어져서 어긋난다.
            float riseStart = sequence.Duration(false);

            // 살짝 지나쳤다 내려앉는다. 레퍼런스에서 카드는 딱 멈추지 않는다.
            sequence.Append(t.DOLocalMove(carousel.StackPosition(0), riseTime)
                             .SetEase(Ease.OutBack, 1.04f));
            // 크기는 **늦게** 따라온다. OutCubic 으로 앞에서 다 키워 버리면 카드가
            // 반도 안 나왔는데 이미 봉지만큼 넓어져서, 봉지에서 나온 게 아니라
            // 봉지 뒤에 있던 판이 올라오는 걸로 읽힌다.
            sequence.Join(t.DOScale(Vector3.one, riseTime).SetEase(Ease.InOutSine));

            if (targetCamera != null)
                sequence.Join(targetCamera.transform.DOMoveZ(-carouselCameraDistance, cameraDollyTime)
                                                    .SetEase(Ease.InOutCubic));

            // 카드가 반쯤 나온 시점부터 빈 봉지가 화면 밖 아래로 빠진다.
            // 제자리에서 줄어들면 "없어졌다" 로만 읽히고 어디로 갔는지 안 남는다.
            // 가속(InCubic)으로 빠져야 손에서 놓은 무게가 붙는다.
            float exitAt = riseStart + riseTime * packExitAt;
            sequence.Insert(exitAt, pack.transform
                .DOLocalMoveY(_packHome.y - packSinkY - packExitDrop, packExitTime).SetEase(Ease.InCubic));
            sequence.Insert(exitAt, pack.transform
                .DOLocalRotate(new Vector3(0f, 0f, packExitSpin), packExitTime, RotateMode.LocalAxisAdd)
                .SetEase(Ease.InQuad));
            sequence.InsertCallback(exitAt + packExitTime, () => pack.gameObject.SetActive(false));
        }

        static float CardHalfHeight(HoloCardController card)
        {
            var r = card.GetComponent<Renderer>();
            return r != null && r.localBounds.size.y > 1e-4f ? r.localBounds.size.y * 0.5f : 0.44f;
        }

        void EnterBrowsing()
        {
            _stage = Stage.Browsing;
            if (carousel != null) carousel.Begin(0);
        }

        /// <summary>카드가 치워지기 시작하면 표식도 같이 걷는다.</summary>
        void OnCardChanged(HoloCardController card, int index)
        {
            if (rarityDisplay != null) rarityDisplay.Hide();
        }

        /// <summary>
        /// 결과 화면에서는 표식을 걷는다. 표식·뱃지는 가운데 한 장을 기준으로
        /// 자리를 잡으므로, 카드가 격자로 흩어진 자리에 그대로 두면 아무 카드에도
        /// 안 붙은 채 화면 가운데 떠 있게 된다.
        /// </summary>
        void OnGalleryChanged(bool open)
        {
            if (rarityDisplay == null || carousel == null) return;

            rarityDisplay.Hide();
            if (!open) return;

            var infos = new List<HoloCardInfo>(carousel.Count);
            var slots = new List<Vector3>(carousel.Count);
            for (int i = 0; i < carousel.Count; i++)
            {
                HoloCardController card = carousel.CardAt(i);
                infos.Add(card != null ? card.GetComponent<HoloCardInfo>() : null);
                carousel.GallerySlot(i, out Vector3 pos);
                slots.Add(pos);
            }

            rarityDisplay.ShowGallery(infos, slots, carousel.CardHeight, carousel.galleryScale,
                                      carousel.galleryTime, carousel.galleryStagger);
        }

        /// <summary>확대 중에는 격자 표식을 접는다.</summary>
        void OnZoomChanged(HoloCardController card, int index)
        {
            if (rarityDisplay != null) rarityDisplay.SetGalleryPipsShown(index < 0);
        }

        /// <summary>
        /// 새 카드가 자리에 앉았다. 등급 표식이 하나씩 뜨고, 레어면 배경까지
        /// 같이 반응한다. 카드에만 파티클을 뿌리면 아무리 뿌려도 밋밋하다.
        /// </summary>
        void OnCardSettled(HoloCardController card, int index)
        {
            if (card == null) return;
            var info = card.GetComponent<HoloCardInfo>();
            if (info == null) return;

            bool rare = info.rarity >= rareThreshold;

            if (rarityDisplay != null && carousel != null)
                rarityDisplay.Show(info, carousel.CardWidth, carousel.CardHeight, rare);

            if (!rare) return;

            if (stage != null) stage.FlashRare(rareFlash);
            if (rareBurst != null)
            {
                rareBurst.transform.position = card.transform.position;
                rareBurst.Play(true);
            }
        }

        // ── 좌표 ─────────────────────────────────────────────────────────

        Vector3 MouthWorldPosition()
        {
            if (pack == null) return Vector3.zero;
            return pack.transform.TransformPoint(new Vector3(0f, pack.MouthLocalY, 0f));
        }

        // ── 리셋 ─────────────────────────────────────────────────────────

        public void ResetToIdle()
        {
            _sequence?.Kill();
            _stage = Stage.Idle;

            // 카메라를 팩 거리로 되돌린다. 안 그러면 다시 뽑기 때 팩이 멀리 있다.
            if (targetCamera != null)
            {
                targetCamera.transform.DOKill();
                Vector3 p = targetCamera.transform.position;
                targetCamera.transform.position = new Vector3(p.x, p.y, -packCameraDistance);
            }

            if (carousel != null) carousel.Clear();
            if (rarityDisplay != null) rarityDisplay.HideImmediate();

            foreach (var info in _pool)
            {
                info.gameObject.SetActive(false);
                var controller = info.GetComponent<HoloCardController>();
                if (controller != null) controller.enabled = false;
            }
            _pulled.Clear();

            if (pack == null) return;

            pack.transform.DOKill();
            pack.gameObject.SetActive(true);
            pack.transform.localPosition = _packHome;
            pack.transform.localRotation = _packHomeRotation;
            pack.transform.localScale = Vector3.one;
            if (slash != null) slash.Hide();

            if (pack.Strip != null)
            {
                pack.Strip.DOKill();
                pack.Strip.gameObject.SetActive(true);
                pack.Strip.localPosition = _stripHome;
                pack.Strip.localRotation = _stripHomeRotation;
                pack.Strip.localScale = Vector3.one;
            }
        }
    }

    static class TweenExtensions
    {
        /// <summary>뜯기 직전의 짧은 반동.</summary>
        public static Tween DOPunchPosSafe(this Transform t) =>
            t.DOPunchPosition(new Vector3(0f, -0.045f, 0f), 0.18f, 6, 0.6f);
    }
}
