using UnityEngine;

namespace HoloCard.PackOpening
{
    /// <summary>
    /// 레어도 등급. TCG Pocket 의 사다리를 그대로 따른다.
    ///
    /// 값의 **순서가 곧 서열**이다. 뽑기 표도 표식 개수도 이 순서로 계산하므로
    /// 중간에 항목을 끼워 넣으면 씬에 이미 저장된 값이 통째로 밀린다.
    /// </summary>
    public enum CardRarity
    {
        Common     = 0,   // ◇
        Uncommon   = 1,   // ◇◇
        Rare       = 2,   // ◇◇◇
        DoubleRare = 3,   // ◇◇◇◇
        ArtRare    = 4,   // ★
        SuperRare  = 5,   // ★★
        Immersive  = 6,   // ★★★
    }

    /// <summary>카드 한 장의 표시용 정보. 연출이 등급에 따라 강도를 바꾼다.</summary>
    [AddComponentMenu("Holo Card/Holo Card Info")]
    public class HoloCardInfo : MonoBehaviour
    {
        public string displayName = "Card";
        public CardRarity rarity = CardRarity.Common;
        [Tooltip("카탈로그의 원본 레어도 문자열. 표시·디버그용.")]
        public string rarityLabel = "Rare Holo";

        /// <summary>◇ 가 아니라 ★ 로 그리는 등급인가.</summary>
        public bool IsStar => rarity >= CardRarity.ArtRare;

        /// <summary>카드 밑에 찍을 표식 개수. ◇ 는 1~4, ★ 는 1~3.</summary>
        public int PipCount => IsStar
            ? (int)rarity - (int)CardRarity.ArtRare + 1
            : (int)rarity + 1;

        /// <summary>등급 이름(표시용).</summary>
        public static string NameOf(CardRarity r)
        {
            switch (r)
            {
                case CardRarity.Common:     return "◇";
                case CardRarity.Uncommon:   return "◇◇";
                case CardRarity.Rare:       return "◇◇◇";
                case CardRarity.DoubleRare: return "◇◇◇◇";
                case CardRarity.ArtRare:    return "★";
                case CardRarity.SuperRare:  return "★★";
                default:                    return "★★★";
            }
        }
    }

    /// <summary>
    /// 한 자리에서 어느 등급이 나올지의 가중치. 슬롯마다 다른 표를 물려
    /// "앞 세 장은 흔하고 마지막 한 장이 판을 뒤집는" 구성을 만든다.
    ///
    /// 합이 100 일 필요는 없다. 굴릴 때 총합으로 나눈다.
    /// </summary>
    [System.Serializable]
    public struct RarityOdds
    {
        [Min(0f)] public float common;
        [Min(0f)] public float uncommon;
        [Min(0f)] public float rare;
        [Min(0f)] public float doubleRare;
        [Min(0f)] public float artRare;
        [Min(0f)] public float superRare;
        [Min(0f)] public float immersive;

        public float Weight(CardRarity r)
        {
            switch (r)
            {
                case CardRarity.Common:     return common;
                case CardRarity.Uncommon:   return uncommon;
                case CardRarity.Rare:       return rare;
                case CardRarity.DoubleRare: return doubleRare;
                case CardRarity.ArtRare:    return artRare;
                case CardRarity.SuperRare:  return superRare;
                default:                    return immersive;
            }
        }

        public CardRarity Roll()
        {
            float total = 0f;
            for (int i = 0; i <= (int)CardRarity.Immersive; i++) total += Weight((CardRarity)i);
            if (total <= 0f) return CardRarity.Common;

            float pick = Random.value * total;
            for (int i = 0; i <= (int)CardRarity.Immersive; i++)
            {
                pick -= Weight((CardRarity)i);
                if (pick <= 0f) return (CardRarity)i;
            }
            return CardRarity.Common;
        }

        public static RarityOdds Flat(params float[] weights)
        {
            var o = new RarityOdds();
            for (int i = 0; i < weights.Length && i <= (int)CardRarity.Immersive; i++)
            {
                switch ((CardRarity)i)
                {
                    case CardRarity.Common:     o.common     = weights[i]; break;
                    case CardRarity.Uncommon:   o.uncommon   = weights[i]; break;
                    case CardRarity.Rare:       o.rare       = weights[i]; break;
                    case CardRarity.DoubleRare: o.doubleRare = weights[i]; break;
                    case CardRarity.ArtRare:    o.artRare    = weights[i]; break;
                    case CardRarity.SuperRare:  o.superRare  = weights[i]; break;
                    default:                    o.immersive  = weights[i]; break;
                }
            }
            return o;
        }
    }
}
