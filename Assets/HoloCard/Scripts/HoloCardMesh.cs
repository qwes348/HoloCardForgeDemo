using System.Collections.Generic;
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
    /// 서브메시 1 = 뒷면 + 옆면 (카드 뒷면 머티리얼)
    ///
    /// 앞면은 -Z 를 본다. Unity 기본 Quad 와 같은 규약이라 카메라를 그대로 둘 수 있다.
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
            {
                _mesh = new Mesh { name = "HoloCard" };
                _mesh.hideFlags = HideFlags.DontSave;
            }
            _mesh.Clear();

            float hw = width * 0.5f;
            float hh = height * 0.5f;
            float hz = thickness * 0.5f;
            float r  = Mathf.Min(cornerRadius, Mathf.Min(hw, hh) * 0.999f);

            List<Vector2> outline = BuildOutline(hw, hh, r, cornerSegments);
            int n = outline.Count;

            var verts    = new List<Vector3>();
            var normals  = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uvs      = new List<Vector2>();
            var frontTris = new List<int>();
            var bodyTris  = new List<int>();

            // ── 앞면 (-Z). 팬 구조: 0 = 중심, 1..n = 외곽.
            int frontBase = verts.Count;
            verts.Add(new Vector3(0f, 0f, -hz));
            normals.Add(new Vector3(0f, 0f, -1f));
            tangents.Add(new Vector4(1f, 0f, 0f, -1f));
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int i = 0; i < n; i++)
            {
                Vector2 p = outline[i];
                verts.Add(new Vector3(p.x, p.y, -hz));
                normals.Add(new Vector3(0f, 0f, -1f));
                tangents.Add(new Vector4(1f, 0f, 0f, -1f));
                uvs.Add(new Vector2((p.x + hw) / width, (p.y + hh) / height));
            }

            // 외곽이 XY 기준 CCW 이므로 -Z 에서 볼 때 CW 가 되도록 뒤집어 감는다.
            for (int i = 0; i < n; i++)
            {
                int a = frontBase + 1 + i;
                int b = frontBase + 1 + (i + 1) % n;
                frontTris.Add(frontBase);
                frontTris.Add(b);
                frontTris.Add(a);
            }

            // ── 뒷면 (+Z)
            int backBase = verts.Count;
            verts.Add(new Vector3(0f, 0f, hz));
            normals.Add(new Vector3(0f, 0f, 1f));
            tangents.Add(new Vector4(-1f, 0f, 0f, -1f));
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int i = 0; i < n; i++)
            {
                Vector2 p = outline[i];
                verts.Add(new Vector3(p.x, p.y, hz));
                normals.Add(new Vector3(0f, 0f, 1f));
                tangents.Add(new Vector4(-1f, 0f, 0f, -1f));
                // 뒷면은 좌우가 뒤집혀 보이므로 U 를 미러링해 아트가 바로 서게 한다.
                uvs.Add(new Vector2(1f - (p.x + hw) / width, (p.y + hh) / height));
            }

            for (int i = 0; i < n; i++)
            {
                int a = backBase + 1 + i;
                int b = backBase + 1 + (i + 1) % n;
                bodyTris.Add(backBase);
                bodyTris.Add(a);
                bodyTris.Add(b);
            }

            // ── 옆면. 세그먼트마다 정점을 복제해 노멀이 각지게 유지되도록.
            if (thickness > 0f)
            {
                float perimeter = 0f;
                var cumulative = new float[n + 1];
                for (int i = 0; i < n; i++)
                {
                    cumulative[i] = perimeter;
                    perimeter += Vector2.Distance(outline[i], outline[(i + 1) % n]);
                }
                cumulative[n] = perimeter;
                if (perimeter <= 0f) perimeter = 1f;

                for (int i = 0; i < n; i++)
                {
                    Vector2 pA = outline[i];
                    Vector2 pB = outline[(i + 1) % n];

                    Vector2 edge = (pB - pA).normalized;
                    // XY 기준 CCW 외곽이므로 바깥쪽은 (edge.y, -edge.x).
                    Vector3 nrm = new Vector3(edge.y, -edge.x, 0f).normalized;
                    Vector4 tan = new Vector4(edge.x, edge.y, 0f, -1f);

                    float uA = cumulative[i] / perimeter;
                    float uB = cumulative[i + 1] / perimeter;

                    int baseIdx = verts.Count;
                    verts.Add(new Vector3(pA.x, pA.y, -hz)); // 0 front A
                    verts.Add(new Vector3(pB.x, pB.y, -hz)); // 1 front B
                    verts.Add(new Vector3(pB.x, pB.y,  hz)); // 2 back  B
                    verts.Add(new Vector3(pA.x, pA.y,  hz)); // 3 back  A

                    for (int k = 0; k < 4; k++) { normals.Add(nrm); tangents.Add(tan); }
                    uvs.Add(new Vector2(uA, 0f));
                    uvs.Add(new Vector2(uB, 0f));
                    uvs.Add(new Vector2(uB, 1f));
                    uvs.Add(new Vector2(uA, 1f));

                    bodyTris.Add(baseIdx + 0); bodyTris.Add(baseIdx + 1); bodyTris.Add(baseIdx + 2);
                    bodyTris.Add(baseIdx + 0); bodyTris.Add(baseIdx + 2); bodyTris.Add(baseIdx + 3);
                }
            }

            _mesh.indexFormat = verts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            _mesh.SetVertices(verts);
            _mesh.SetNormals(normals);
            _mesh.SetTangents(tangents);
            _mesh.SetUVs(0, uvs);
            _mesh.subMeshCount = 2;
            _mesh.SetTriangles(frontTris, 0);
            _mesh.SetTriangles(bodyTris, 1);
            _mesh.RecalculateBounds();

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

        /// <summary>둥근 사각형 외곽을 XY 평면에 CCW 로 만든다.</summary>
        static List<Vector2> BuildOutline(float hw, float hh, float r, int segments)
        {
            var pts = new List<Vector2>();

            if (r <= 1e-5f)
            {
                pts.Add(new Vector2( hw, -hh));
                pts.Add(new Vector2( hw,  hh));
                pts.Add(new Vector2(-hw,  hh));
                pts.Add(new Vector2(-hw, -hh));
                return pts;
            }

            float ix = hw - r;
            float iy = hh - r;

            // 코너 중심을 CCW 로: 우하 → 우상 → 좌상 → 좌하
            Vector2[] centers = { new Vector2(ix, -iy), new Vector2(ix, iy), new Vector2(-ix, iy), new Vector2(-ix, -iy) };
            float[] startAngle = { -90f, 0f, 90f, 180f };

            for (int c = 0; c < 4; c++)
            {
                for (int s = 0; s <= segments; s++)
                {
                    float a = (startAngle[c] + 90f * s / segments) * Mathf.Deg2Rad;
                    pts.Add(centers[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
            }
            return pts;
        }
    }
}
