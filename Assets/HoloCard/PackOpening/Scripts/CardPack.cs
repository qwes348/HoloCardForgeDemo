using System.Collections.Generic;
using UnityEngine;

namespace HoloCard.PackOpening
{
    /// <summary>
    /// 카드팩 포장지 메시. 본체와 뜯겨 나가는 상단 스트립 두 조각으로 나뉘며,
    /// 둘은 같은 지그재그 이음매를 공유해서 뜯기 전에는 정확히 맞물린다.
    ///
    /// UV 는 두 조각 모두 "팩 전체" 기준이라 포장지 아트가 이음매를 가로질러 이어진다.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Holo Card/Card Pack")]
    public class CardPack : MonoBehaviour
    {
        [Header("Size")]
        [Min(0.05f)] public float width = 0.76f;
        [Min(0.05f)] public float height = 1.22f;
        [Min(0.001f)] public float thickness = 0.055f;
        [Min(0f)] public float cornerRadius = 0.028f;
        [Range(1, 12)] public int cornerSegments = 5;

        [Header("Tear Seam")]
        [Tooltip("상단 스트립이 차지하는 높이 비율.")]
        [Range(0.03f, 0.30f)] public float stripHeightRatio = 0.085f;
        [Tooltip("찢어진 가장자리의 톱니 개수.")]
        [Range(6, 80)] public int tearSegments = 34;
        [Tooltip("톱니 높이. 0 이면 직선으로 잘린다.")]
        [Min(0f)] public float tearJitter = 0.013f;
        public int tearSeed = 20260823;

        [Header("Materials")]
        public Material frontMaterial;
        public Material sideMaterial;

        [Header("Rebuild")]
        public bool rebuildOnValidate = true;

        Transform _body, _strip;
        Mesh _bodyMesh, _stripMesh;

        /// <summary>본체. 카드가 여기서 나온다.</summary>
        public Transform Body => _body;
        /// <summary>뜯겨 날아가는 상단 조각.</summary>
        public Transform Strip => _strip;

        /// <summary>스트립을 뗀 자리(팩 로컬 좌표)의 y. 카드가 여기서 솟아오른다.</summary>
        public float MouthLocalY => height * 0.5f - height * stripHeightRatio;

        /// <summary>
        /// 스트립의 제자리(팩 로컬). 지오메트리에서 계산하므로 Rebuild 전에 물어도 맞다.
        /// Transform 에서 읽으면 Awake 순서에 따라 아직 0 일 수 있다.
        /// </summary>
        public Vector3 StripLocalHome => new Vector3(0f, (MouthLocalY + height * 0.5f) * 0.5f, 0f);

        void OnEnable() { Rebuild(); }
        void OnValidate() { if (rebuildOnValidate && isActiveAndEnabled) Rebuild(); }

        void OnDestroy()
        {
            DestroyMesh(ref _bodyMesh);
            DestroyMesh(ref _stripMesh);
        }

        static void DestroyMesh(ref Mesh m)
        {
            if (m == null) return;
            if (Application.isPlaying) Destroy(m); else DestroyImmediate(m);
            m = null;
        }

        public void Rebuild()
        {
            float hw = width * 0.5f;
            float hh = height * 0.5f;
            float r  = Mathf.Min(cornerRadius, Mathf.Min(hw, hh) * 0.4f);
            float seamY = hh - height * stripHeightRatio;

            List<Vector2> seam = BuildSeam(hw, seamY, r);

            _body  = EnsureChild("Body", ref _bodyMesh, "CardPack_Body");
            _strip = EnsureChild("Strip", ref _stripMesh, "CardPack_Strip");

            var uvOrigin = new Vector2(-hw, -hh);
            var uvSize   = new Vector2(width, height);

            // ── 본체: 아래 두 코너는 둥글게, 위는 지그재그 이음매
            var bodyOutline = new List<Vector2>();
            HoloCardPrism.AppendArc(bodyOutline, new Vector2(hw - r, -hh + r), r, -90f, 90f, cornerSegments);
            bodyOutline.AddRange(seam);                       // (hw, seamY) → (-hw, seamY)
            HoloCardPrism.AppendArc(bodyOutline, new Vector2(-hw + r, -hh + r), r, 180f, 90f, cornerSegments);

            HoloCardPrism.Build(_bodyMesh, bodyOutline, thickness,
                                pivot: Vector2.zero, uvOrigin: uvOrigin, uvSize: uvSize);

            // ── 스트립: 이음매를 뒤집어 쓰고 위 두 코너는 둥글게.
            //    자기 중심을 피벗으로 삼아야 뜯길 때 제자리에서 돈다.
            var stripOutline = new List<Vector2>();
            for (int i = seam.Count - 1; i >= 0; i--) stripOutline.Add(seam[i]);   // (-hw, seamY) → (hw, seamY)
            HoloCardPrism.AppendArc(stripOutline, new Vector2(hw - r, hh - r), r, 0f, 90f, cornerSegments);
            HoloCardPrism.AppendArc(stripOutline, new Vector2(-hw + r, hh - r), r, 90f, 90f, cornerSegments);

            var stripPivot = new Vector2(0f, (seamY + hh) * 0.5f);
            HoloCardPrism.Build(_stripMesh, stripOutline, thickness,
                                pivot: stripPivot, uvOrigin: uvOrigin, uvSize: uvSize);
            _strip.localPosition = new Vector3(stripPivot.x, stripPivot.y, 0f);

            ApplyMaterials(_body);
            ApplyMaterials(_strip);
        }

        /// <summary>
        /// 지그재그 이음매. 두 조각이 같은 배열을 쓰므로 뜯기 전에는 빈틈이 없다.
        /// 양 끝은 옆면과 깔끔하게 만나도록 톱니를 죽인다.
        /// </summary>
        List<Vector2> BuildSeam(float hw, float seamY, float cornerR)
        {
            var seam = new List<Vector2>();
            int n = Mathf.Max(2, tearSegments);

            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float x = Mathf.Lerp(hw, -hw, t);

                // 굵은 물결 + 잔 톱니를 섞어야 가위질이 아니라 찢은 것처럼 보인다.
                float wave  = Hash(t * 6.3f, tearSeed) - 0.5f;
                float teeth = Hash(t * 27.1f, tearSeed * 3 + 7) - 0.5f;
                float jitter = (wave * 0.55f + teeth * 0.45f) * 2f * tearJitter;

                // 양 끝 15% 는 서서히 직선으로 수렴시킨다.
                float taper = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(t, 1f - t) / 0.15f));
                seam.Add(new Vector2(x, seamY + jitter * taper));
            }
            return seam;
        }

        Transform EnsureChild(string childName, ref Mesh mesh, string meshName)
        {
            Transform t = transform.Find(childName);
            if (t == null)
            {
                var go = new GameObject(childName, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                t = go.transform;
            }

            if (mesh == null)
                mesh = new Mesh { name = meshName, hideFlags = HideFlags.DontSave };

            t.GetComponent<MeshFilter>().sharedMesh = mesh;
            return t;
        }

        void ApplyMaterials(Transform piece)
        {
            if (piece == null) return;
            var renderer = piece.GetComponent<MeshRenderer>();
            if (renderer == null) return;
            renderer.sharedMaterials = new[] { frontMaterial, sideMaterial };
        }

        static float Hash(float x, int seed)
        {
            float n = Mathf.Sin(x * 127.1f + seed * 0.0137f) * 43758.5453f;
            return n - Mathf.Floor(n);
        }
    }
}
