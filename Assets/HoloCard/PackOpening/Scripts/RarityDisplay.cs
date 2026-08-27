using DG.Tweening;
using UnityEngine;

namespace HoloCard.PackOpening
{
    /// <summary>
    /// 카드 밑의 레어도 표식(◇ / ★)과 왼쪽 위의 NEW 뱃지.
    ///
    /// 표식은 **한 번에 다 뜨지 않는다.** 한 개씩 차례로 튀어나오며 그때마다
    /// 작은 섬광이 터진다. 네 개를 동시에 띄우면 그냥 아이콘 줄이지만, 하나씩
    /// 세어 주면 "◇◇◇ 이구나" 를 읽을 시간이 생겨서 등급이 사건이 된다.
    /// 마지막 한 개가 늦게 뜰 때의 그 반 박자가 연출의 전부다.
    ///
    /// 표식·뱃지·섬광 모두 월드 공간 쿼드다. 캔버스를 쓰지 않는 이유는 무대의
    /// 나머지(배경 층·슬래시·화살표)가 전부 쿼드라서, 굳이 uGUI 를 끌어들이면
    /// 해상도·앵커 규약이 두 벌이 되기 때문이다.
    /// </summary>
    [AddComponentMenu("Holo Card/Rarity Display")]
    public class RarityDisplay : MonoBehaviour
    {
        [System.Serializable]
        public class Pip
        {
            public Transform root;
            [Tooltip("◇ 또는 ★ 쿼드. 머티리얼은 등급에 따라 갈아 끼운다.")]
            public Renderer mark;
            [Tooltip("표식이 뜰 때 같이 터지는 섬광. root 의 **자식이 아니다** — " +
                     "root 는 팝인 하느라 스케일이 0 에서 출발하는데, 자식으로 두면 " +
                     "거기에 섬광 스케일까지 곱해져서 아무것도 안 보인다.")]
            public Transform sparkle;
            public Renderer sparkleRenderer;
        }

        [Header("Refs")]
        [Tooltip("최대 개수만큼 미리 만들어 둔다. ◇ 는 4개, ★ 는 3개까지 쓴다.")]
        public Pip[] pips = new Pip[4];
        public Material diamondMaterial;
        public Material starMaterial;
        [Tooltip("레어 등급에서만 뜨는 뱃지.")]
        public Transform newBadge;
        public Renderer newBadgeRenderer;

        [Header("Layout")]
        [Tooltip("표식 사이 간격.")]
        public float pipSpacing = 0.092f;
        [Tooltip("카드 밑변에서 표식까지의 거리.")]
        public float pipGap = 0.086f;
        public float pipSize = 0.072f;
        public float sparkleSize = 0.13f;

        [Header("Badge Layout")]
        public Vector2 badgeSize = new Vector2(0.20f, 0.10f);
        [Tooltip("카드 왼쪽 위 모서리에서의 어긋남. y 를 올려 카드 **밖**으로 빼 둔다 — " +
                 "안쪽에 얹으면 하필 카드 이름 위에 앉아서 무슨 카드인지 안 보인다.")]
        public Vector2 badgeOffset = new Vector2(0f, 0.115f);

        [Header("Timing")]
        [Tooltip("표식이 하나씩 뜨는 간격.")]
        public float pipInterval = 0.085f;
        [Tooltip("첫 표식이 뜨기까지의 뜸. 카드가 자리에 앉고 나서 떠야 한다.")]
        public float pipDelay = 0.12f;
        public float pipPop = 0.30f;
        public float sparkleTime = 0.38f;
        public float badgeDelay = 0.26f;
        public float badgePop = 0.42f;
        public float hideTime = 0.09f;

        [Header("Look")]
        public Color diamondColor = new Color(1f, 1f, 1f, 0.95f);
        public Color starColor = new Color(1f, 0.93f, 0.55f, 1f);
        // 가산이라 흰색을 그대로 쓰면 절정에서 표식을 통째로 삼킨다. 반짝임은
        // 표식을 **띄우는** 것이지 가리는 게 아니다.
        public Color sparkleColor = new Color(0.86f, 0.83f, 0.68f, 1f);
        public Color badgeColor = Color.white;

        Sequence _sequence;
        MaterialPropertyBlock _mpb;
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        void Awake() => HideImmediate();

        void OnDestroy() => _sequence?.Kill();

        /// <summary>표식을 즉시 치운다. 씬 리셋용.</summary>
        public void HideImmediate()
        {
            _sequence?.Kill();
            foreach (var p in pips)
            {
                if (p?.root == null) continue;
                p.root.DOKill();
                p.root.localScale = Vector3.zero;
                p.root.gameObject.SetActive(false);
                if (p.sparkle != null) p.sparkle.gameObject.SetActive(false);
            }
            if (newBadge != null)
            {
                newBadge.DOKill();
                newBadge.localScale = Vector3.zero;
                newBadge.gameObject.SetActive(false);
            }
        }

        /// <summary>카드가 치워지는 동안 잠깐 사라진다.</summary>
        public void Hide()
        {
            _sequence?.Kill();
            _sequence = DOTween.Sequence();

            foreach (var p in pips)
            {
                if (p?.root == null || !p.root.gameObject.activeSelf) continue;
                Transform root = p.root;
                root.DOKill();
                _sequence.Insert(0f, root.DOScale(Vector3.zero, hideTime).SetEase(Ease.InQuad)
                                         .OnComplete(() => root.gameObject.SetActive(false)));
                if (p.sparkle != null) p.sparkle.gameObject.SetActive(false);
            }

            if (newBadge != null && newBadge.gameObject.activeSelf)
            {
                Transform badge = newBadge;
                badge.DOKill();
                _sequence.Insert(0f, badge.DOScale(Vector3.zero, hideTime).SetEase(Ease.InQuad)
                                          .OnComplete(() => badge.gameObject.SetActive(false)));
            }
        }

        /// <summary>
        /// 새 카드의 등급을 띄운다.
        /// </summary>
        /// <param name="showBadge">NEW 뱃지를 같이 띄울지. 레어 등급에서만 true.</param>
        public void Show(HoloCardInfo info, float cardWidth, float cardHeight, bool showBadge)
        {
            HideImmediate();
            if (info == null) return;

            int count = Mathf.Clamp(info.PipCount, 0, pips.Length);
            Material mark = info.IsStar ? starMaterial : diamondMaterial;
            Color markColor = info.IsStar ? starColor : diamondColor;

            _sequence = DOTween.Sequence();

            float halfSpan = (count - 1) * 0.5f;
            float y = -cardHeight * 0.5f - pipGap;

            for (int i = 0; i < count; i++)
            {
                Pip p = pips[i];
                if (p?.root == null) continue;

                p.root.localPosition = new Vector3((i - halfSpan) * pipSpacing, y, 0f);
                p.root.localRotation = Quaternion.identity;
                p.root.localScale = Vector3.zero;
                p.root.gameObject.SetActive(true);

                if (p.mark != null)
                {
                    p.mark.transform.localScale = new Vector3(pipSize, pipSize, 1f);
                    if (mark != null) p.mark.sharedMaterial = mark;
                    SetAlpha(p.mark, markColor);
                }

                float at = pipDelay + i * pipInterval;

                // 살짝 넘겼다 돌아온다. 그냥 커지면 "떴다" 가 아니라 "켜졌다" 다.
                _sequence.Insert(at, p.root.DOScale(Vector3.one, pipPop).SetEase(Ease.OutBack, 2.6f));

                if (p.sparkle == null || p.sparkleRenderer == null) continue;

                Transform sp = p.sparkle;
                sp.localPosition = p.root.localPosition;
                sp.gameObject.SetActive(true);
                sp.localScale = Vector3.one * sparkleSize * 0.25f;
                SetAdditive(p.sparkleRenderer, sparkleColor, 0f);

                _sequence.Insert(at, sp.DOScale(Vector3.one * sparkleSize, sparkleTime).SetEase(Ease.OutQuad));
                // 가산 합성이라 알파가 아니라 밝기로 죽여야 한다.
                Renderer sr = p.sparkleRenderer;
                _sequence.Insert(at, DOVirtual.Float(1f, 0f, sparkleTime, v => SetAdditive(sr, sparkleColor, v))
                                              .SetEase(Ease.InQuad));
            }

            if (!showBadge || newBadge == null) return;

            newBadge.localPosition = new Vector3(
                -cardWidth * 0.5f + badgeSize.x * 0.5f + badgeOffset.x,
                 cardHeight * 0.5f - badgeSize.y * 0.5f + badgeOffset.y,
                -0.03f);
            newBadge.localRotation = Quaternion.Euler(0f, 0f, 7f);
            newBadge.localScale = Vector3.zero;
            newBadge.gameObject.SetActive(true);
            if (newBadgeRenderer != null) SetAlpha(newBadgeRenderer, badgeColor);

            _sequence.Insert(badgeDelay, newBadge.DOScale(Vector3.one, badgePop).SetEase(Ease.OutBack, 3f));
            _sequence.Insert(badgeDelay, newBadge.DOLocalRotate(Vector3.zero, badgePop).SetEase(Ease.OutBack, 2f));
        }

        /// <summary>알파 블렌드 대상(표식·뱃지). 알파가 곧 불투명도다.</summary>
        void SetAlpha(Renderer r, Color c) => Write(r, c);

        /// <summary>
        /// 가산 합성 대상(섬광). 알파는 아무 일도 안 하므로 **밝기**로 죽여야 한다.
        /// 알파를 내리면 끝까지 그대로 밝게 남는다.
        /// </summary>
        void SetAdditive(Renderer r, Color c, float amount)
        {
            float a = Mathf.Max(amount, 0f);
            Write(r, new Color(c.r * a, c.g * a, c.b * a, 1f));
        }

        void Write(Renderer r, Color c)
        {
            if (r == null) return;
            _mpb ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorId, c);
            r.SetPropertyBlock(_mpb);
        }
    }
}
