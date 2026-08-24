using UnityEngine;

namespace HoloCard
{
    /// <summary>
    /// 두께와 둥근 모서리를 가진 카드 메시를 만든다.
    ///
    /// 쿼드 한 장이 아니라 실제 부피가 있는 물체라서, 기울이면 옆면이 드러나고
    /// 그림자가 두께만큼 두껍게 진다. 셰이더 안쪽의 패럴랙스(POM)와 합쳐지면
    /// "표면 아래로 파인 디오라마 + 물리적으로 두꺼운 카드"가 된다.
    ///
    /// 서브메시 0 = 앞면 (홀로 셰이더)
    /// 서브메시 1 = 뒷면 (카드 뒷면 아트)
    /// 서브메시 2 = 옆면 (종이 단면)
    ///
    /// 앞면은 -Z 를 본다. Unity 기본 Quad 와 같은 규약이라 카메라를 그대로 둘 수 있다.
    /// 실제 정점 생성은 <see cref="HoloCardPrism"/> 이 맡는다 (팩 포장지와 공유).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("Holo Card/Holo Card Mesh")]
    public class HoloCardMesh : MonoBehaviour
    {
        [Header("Size (실제 TCG 카드 = 63 x 88 mm)")]
        [Min(0.01f)] public float width  = 0.63f;
        [Min(0.01f)] public float height = 0.88f;
        [Min(0f)]    public float thickness = 0.012f;

        [Header("Corners")]
        [Min(0f)] public float cornerRadius = 0.03f;
        [Range(1, 16)] public int cornerSegments = 6;

        [Header("Rebuild")]
        [Tooltip("값이 바뀔 때마다 다시 만든다. 런타임에 끄면 약간 아낀다.")]
        public bool rebuildOnValidate = true;

        Mesh _mesh;

        void OnEnable()  { Rebuild(); }
        void OnValidate() { if (rebuildOnValidate && isActiveAndEnabled) Rebuild(); }

        void OnDestroy()
        {
            if (_mesh == null) return;
            if (Application.isPlaying) Destroy(_mesh);
            else DestroyImmediate(_mesh);
        }

        public void Rebuild()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter == null) return;

            if (_mesh == null)
                _mesh = new Mesh { name = "HoloCard", hideFlags = HideFlags.DontSave };

            float hw = width * 0.5f;
            float hh = height * 0.5f;
            float r  = Mathf.Min(cornerRadius, Mathf.Min(hw, hh) * 0.999f);

            var outline = HoloCardPrism.RoundedRect(hw, hh, r, cornerSegments);
            HoloCardPrism.Build(_mesh, outline, thickness,
                                pivot:    Vector2.zero,
                                uvOrigin: new Vector2(-hw, -hh),
                                uvSize:   new Vector2(width, height));

            filter.sharedMesh = _mesh;

            // 클릭 판정용 박스. 여기서 AddComponent 를 하면 OnValidate 경로에서
            // 경고가 나므로, 이미 붙어 있을 때만 크기를 맞춘다.
            var box = GetComponent<BoxCollider>();
            if (box != null)
            {
                box.center = Vector3.zero;
                box.size = new Vector3(width, height, Mathf.Max(thickness, 0.002f));
            }
        }
    }
}
