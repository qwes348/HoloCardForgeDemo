using UnityEngine;

namespace HoloCard.PackOpening
{
    /// <summary>카드 한 장의 표시용 정보. 연출이 등급에 따라 연출 강도를 바꾼다.</summary>
    [AddComponentMenu("Holo Card/Holo Card Info")]
    public class HoloCardInfo : MonoBehaviour
    {
        public string displayName = "Card";
        public string rarity = "Rare Holo";

        [Tooltip("켜져 있으면 팩에서 나올 때 특별 연출이 붙는다.")]
        public bool isRare;
    }
}
