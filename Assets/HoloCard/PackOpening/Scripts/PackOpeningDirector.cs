using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HoloCard.PackOpening
{
    /// <summary>
    /// 카드팩 개봉 연출.
    ///
    ///   Idle      팩이 떠 있고 포인터를 따라 기운다. 클릭하면 시작.
    ///   Tearing   상단 스트립이 찢겨 날아가고 포일 조각이 터진다.
    ///   Dealing   카드가 팩 입구에서 한 장씩 뒷면으로 솟아 부채꼴로 깔린다.
    ///   Browsing  카드를 클릭하면 뒤집히며 확대된다 (HoloCardInspector 가 맡는다).
    ///
    /// 카드가 뒷면으로 깔리는 게 핵심이다. HoloCardInspector 는 확대할 때 카드를
    /// 카메라 정면으로 돌리는데, 뒷면 상태에서 그러면 그 회전 자체가 곧 "뒤집기"가
    /// 된다. 별도의 뒤집기 코드가 필요 없다.
    ///
    /// R 키로 다시 뽑는다.
    /// </summary>
    [AddComponentMenu("Holo Card/Pack Opening Director")]
    public class PackOpeningDirector : MonoBehaviour
    {
        public enum Stage { Idle, Tearing, Dealing, Browsing }

        [Header("Scene")]
        public CardPack pack;
        public Transform cardPool;
        public HoloCardInspector inspector;
        public Camera targetCamera;
        public ParticleSystem tearBurst;
        public ParticleSystem rareBurst;

        [Header("Pull")]
        [Tooltip("한 팩에서 나오는 카드 수.")]
        [Range(1, 10)] public int cardsPerPack = 5;
        [Tooltip("그중 확정 레어 수.")]
        [Range(0, 5)] public int guaranteedRares = 1;

        [Header("Pack Idle")]
        public float bobAmplitude = 0.035f;
        public float bobSpeed = 1.2f;
        public float packTiltAngle = 14f;
        public float packFollow = 7f;

        [Header("Timing")]
        public float tearDuration = 0.55f;
        public float dealInterval = 0.13f;
        public float dealDuration = 0.45f;
        [Tooltip("레어가 나올 때 추가로 머무는 시간.")]
        public float rarePause = 0.45f;

        [Header("Fan Layout")]
        public float fanSpacing = 0.52f;
        public float fanArcAngle = 13f;
        public float fanDepthStep = 0.12f;
        public float fanDrop = 0.03f;

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

            if (inspector != null) inspector.enabled = false;
        }

        void Start() => ResetToIdle();

        void OnDestroy() => _sequence?.Kill();

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame && _stage != Stage.Tearing && _stage != Stage.Dealing)
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

            _sequence?.Kill();
            _sequence = DOTween.Sequence();

            Transform strip = pack.Strip;
            if (strip != null)
            {
                // 뜯기 직전 짧게 긴장 — 살짝 눌렀다가 튀어나간다.
                _sequence.Append(pack.transform.DOPunchPosSafe());
                _sequence.Join(strip.DOLocalMoveY(_stripHome.y + 0.55f, tearDuration).SetEase(Ease.InQuad));
                _sequence.Join(strip.DOLocalMoveX(_stripHome.x + 0.28f, tearDuration).SetEase(Ease.InQuad));
                _sequence.Join(strip.DOLocalRotate(new Vector3(28f, 0f, -46f), tearDuration, RotateMode.LocalAxisAdd)
                                    .SetEase(Ease.OutQuad));
                _sequence.InsertCallback(0.06f, () =>
                {
                    if (tearBurst != null) { tearBurst.transform.position = MouthWorldPosition(); tearBurst.Play(true); }
                });
                _sequence.AppendCallback(() => strip.gameObject.SetActive(false));
            }

            _sequence.AppendCallback(() => _stage = Stage.Dealing);
            AppendDeal(_sequence);
            _sequence.AppendCallback(EnterBrowsing);
        }

        /// <summary>풀에서 이번 팩에 나올 카드를 뽑는다. 확정 레어를 보장한다.</summary>
        void PickCards()
        {
            _pulled.Clear();
            if (_pool.Count == 0) return;

            var rares = new List<HoloCardInfo>();
            var normals = new List<HoloCardInfo>();
            foreach (var info in _pool) (info.isRare ? rares : normals).Add(info);

            var chosen = new List<HoloCardInfo>();
            int rareCount = Mathf.Min(guaranteedRares, rares.Count);
            TakeRandom(rares, rareCount, chosen);
            TakeRandom(normals, Mathf.Max(0, cardsPerPack - rareCount), chosen);

            // 모자라면 남은 데서 채운다.
            var leftovers = new List<HoloCardInfo>();
            foreach (var info in _pool) if (!chosen.Contains(info)) leftovers.Add(info);
            TakeRandom(leftovers, cardsPerPack - chosen.Count, chosen);

            // 레어를 마지막에 배치해 절정에서 나오게 한다.
            chosen.Sort((a, b) => a.isRare.CompareTo(b.isRare));

            foreach (var info in chosen)
            {
                var controller = info.GetComponent<HoloCardController>();
                if (controller != null) _pulled.Add(controller);
            }
        }

        static void TakeRandom(List<HoloCardInfo> from, int count, List<HoloCardInfo> into)
        {
            for (int i = 0; i < count && from.Count > 0; i++)
            {
                int k = Random.Range(0, from.Count);
                into.Add(from[k]);
                from.RemoveAt(k);
            }
        }

        void AppendDeal(Sequence sequence)
        {
            Vector3 mouth = MouthLocalPosition();

            for (int i = 0; i < _pulled.Count; i++)
            {
                HoloCardController card = _pulled[i];
                int index = i;
                var info = card.GetComponent<HoloCardInfo>();
                bool rare = info != null && info.isRare;

                GetFanPose(index, _pulled.Count, out Vector3 pose, out Quaternion rot);

                sequence.AppendCallback(() =>
                {
                    card.gameObject.SetActive(true);
                    Transform t = card.transform;
                    t.localPosition = mouth;
                    t.localScale = Vector3.one * 0.82f;
                    // 뒷면이 카메라를 보게 세워 둔다. 확대할 때의 회전이 곧 뒤집기가 된다.
                    t.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    card.SetHome(mouth, t.localRotation);
                    card.enabled = false;
                });

                sequence.Append(card.transform.DOLocalMove(pose, dealDuration).SetEase(Ease.OutBack, 1.1f));
                sequence.Join(card.transform.DOLocalRotateQuaternion(rot, dealDuration).SetEase(Ease.OutCubic));
                sequence.Join(card.transform.DOScale(Vector3.one, dealDuration).SetEase(Ease.OutCubic));

                sequence.AppendCallback(() =>
                {
                    card.SetHome(pose, rot);
                    if (rare && rareBurst != null)
                    {
                        rareBurst.transform.position = card.transform.position;
                        rareBurst.Play(true);
                    }
                });

                if (rare)
                {
                    sequence.Append(card.transform.DOPunchScale(Vector3.one * 0.12f, 0.34f, 8, 0.7f));
                    sequence.AppendInterval(rarePause);
                }
                else
                {
                    sequence.AppendInterval(dealInterval);
                }
            }

            // 팩은 아래로 떨어뜨린다.
            sequence.Append(pack.transform.DOLocalMoveY(_packHome.y - 2.4f, 0.7f).SetEase(Ease.InCubic));
            sequence.Join(pack.transform.DOLocalRotate(new Vector3(0f, 0f, -28f), 0.7f, RotateMode.LocalAxisAdd));
            sequence.AppendCallback(() => pack.gameObject.SetActive(false));
        }

        void EnterBrowsing()
        {
            _stage = Stage.Browsing;

            foreach (var card in _pulled)
                card.enabled = true;

            if (inspector == null) return;
            inspector.enabled = true;
            inspector.Rescan();
            inspector.FocusChanged -= OnFocusChanged;
            inspector.FocusChanged += OnFocusChanged;
        }

        /// <summary>
        /// 한 번 확대해서 앞면을 본 카드는 그 뒤로도 앞면을 유지한다.
        /// 확대를 풀면 원래 자리로 돌아가되 뒤집힌 채로 남는다.
        /// </summary>
        void OnFocusChanged(HoloCardController card)
        {
            if (card == null) return;

            int index = _pulled.IndexOf(card);
            if (index < 0) return;

            GetFanPose(index, _pulled.Count, out Vector3 pose, out Quaternion rot);
            // Y 180 을 뺀 = 앞면이 보이는 자세
            Quaternion faceUp = rot * Quaternion.Euler(0f, 180f, 0f);
            inspector.SetHome(card, pose, faceUp);
        }

        // ── 배치 ─────────────────────────────────────────────────────────

        void GetFanPose(int index, int count, out Vector3 position, out Quaternion rotation)
        {
            float x = (index - (count - 1) * 0.5f) * fanSpacing;
            float z = Mathf.Abs(x) * fanDepthStep;
            float y = -Mathf.Abs(x) * fanDrop;

            position = new Vector3(x, y, z);
            // 뒷면이 보이도록 Y 180 을 얹은 상태가 기본 자세다.
            rotation = Quaternion.Euler(0f, 180f - x * fanArcAngle, 0f);
        }

        Vector3 MouthLocalPosition()
        {
            if (pack == null) return Vector3.zero;
            Vector3 world = MouthWorldPosition();
            Transform parent = cardPool != null ? cardPool.parent : null;
            return parent != null ? parent.InverseTransformPoint(world) : world;
        }

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

            if (inspector != null)
            {
                inspector.Focus(-1);
                inspector.FocusChanged -= OnFocusChanged;
                inspector.enabled = false;
            }

            foreach (var info in _pool)
            {
                info.gameObject.SetActive(false);
                var controller = info.GetComponent<HoloCardController>();
                if (controller != null) controller.enabled = false;
            }
            _pulled.Clear();

            if (pack == null) return;

            pack.gameObject.SetActive(true);
            pack.transform.localPosition = _packHome;
            pack.transform.localRotation = _packHomeRotation;
            if (pack.Strip != null)
            {
                pack.Strip.gameObject.SetActive(true);
                pack.Strip.localPosition = _stripHome;
                pack.Strip.localRotation = _stripHomeRotation;
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
