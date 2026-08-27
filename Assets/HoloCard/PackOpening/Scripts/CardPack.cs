using System.Collections.Generic;
using UnityEngine;

namespace HoloCard.PackOpening
{
    /// <summary>
    /// 카드팩 포장지 메시. 본체와 뜯겨 나가는 상단 스트립 두 조각으로 나뉘며,
    /// 둘은 같은 지그재그 이음매를 공유해서 뜯기 전에는 정확히 맞물린다.
    ///
    /// 만드는 방법이 두 가지다.
    ///   셸 (<see cref="shellMesh"/> 지정)  구워 둔 봉지 메시를 이음매로 자른다.
    ///                                     크림프와 부푼 단면이 있어 비닐로 읽힌다.
    ///   프리즘 (셸 비움)                   둥근 사각형을 밀어낸 판때기. 폴백.
    ///
    /// 어느 쪽이든 UV 는 "팩 전체" 기준이라 포장지 아트가 이음매를 가로질러 이어진다.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Holo Card/Card Pack")]
    public class CardPack : MonoBehaviour
    {
        [Header("Shell")]
        [Tooltip("Tools > Holo Card > Bake Pack Shell 로 구운 봉지 메시. " +
                 "지정하면 Width / Thickness 는 이 메시의 비율에서 자동으로 나온다.")]
        public Mesh shellMesh;

        [Header("Size")]
        [Min(0.05f)] public float width = 0.684f;
        [Min(0.05f)] public float height = 1.30f;
        [Min(0.001f)] public float thickness = 0.059f;
        [Tooltip("프리즘 폴백에서만 쓴다.")]
        [Min(0f)] public float cornerRadius = 0.028f;
        [Tooltip("프리즘 폴백에서만 쓴다.")]
        [Range(1, 12)] public int cornerSegments = 5;

        [Header("Tear Seam")]
        [Tooltip("상단 스트립이 차지하는 높이 비율. 셸의 크림프 폭이 대략 0.06 이다.")]
        [Range(0.03f, 0.30f)] public float stripHeightRatio = 0.085f;
        [Tooltip("찢어진 가장자리의 톱니 개수. 셸을 자를 때는 메시 격자(가로 약 37칸)보다 " +
                 "촘촘하면 삼각형 안에서 뭉개지므로 20 을 넘기지 말 것.")]
        [Range(6, 80)] public int tearSegments = 16;
        [Tooltip("톱니 높이. 0 이면 직선으로 잘린다.")]
        [Min(0f)] public float tearJitter = 0.013f;
        public int tearSeed = 20260823;

        [Header("Materials")]
        public Material frontMaterial;
        [Tooltip("프리즘 폴백의 뒷면. 셸은 한 덩어리라 Front 만 쓴다.")]
        public Material backMaterial;
        [Tooltip("프리즘 폴백의 옆면. 셸은 한 덩어리라 Front 만 쓴다.")]
        public Material sideMaterial;

        [Header("Rebuild")]
        public bool rebuildOnValidate = true;

        Transform _body, _strip;
        Mesh _bodyMesh, _stripMesh;

        // 셸 정점은 Rebuild 마다 다시 읽으면 매번 배열을 새로 뽑는다. 슬라이더를
        // 끄는 동안 초당 수십 번 도는 자리라 메시가 바뀔 때만 갱신한다.
        //
        // 함정: 도메인 리로드를 건너면 둘의 운명이 다르다. Mesh 는 오브젝트
        // 참조라 살아남고, Shell 은 [Serializable] 아닌 구조체라 통째로 날아간다.
        // "원본이 그대로면 캐시도 그대로" 로만 판단하면 리로드 뒤에 빈 캐시를
        // 유효한 것으로 믿고 조용히 프리즘 폴백으로 떨어진다. 내용도 같이 본다.
        [System.NonSerialized] Mesh _cachedShellSource;
        [System.NonSerialized] PackShellSlicer.Shell _cachedShell;

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
            _body  = EnsureChild("Body", ref _bodyMesh, "CardPack_Body");
            _strip = EnsureChild("Strip", ref _stripMesh, "CardPack_Strip");

            if (TryBuildFromShell()) return;
            BuildPrism();
        }

        // ── 셸 경로 ──────────────────────────────────────────────────────

        bool TryBuildFromShell()
        {
            if (shellMesh == null) return false;

            if (!shellMesh.isReadable)
            {
                Debug.LogError($"[CardPack] 셸 메시 '{shellMesh.name}' 를 읽을 수 없습니다. " +
                               "임포터의 Read/Write 를 켜거나 Bake Pack Shell 로 다시 구우세요.", this);
                return false;
            }

            if (_cachedShellSource != shellMesh || !_cachedShell.IsValid)
            {
                _cachedShell = new PackShellSlicer.Shell
                {
                    positions = shellMesh.vertices,
                    normals   = shellMesh.normals,
                    tangents  = shellMesh.tangents,
                    uvs       = shellMesh.uv,
                    triangles = shellMesh.triangles,
                };
                _cachedShellSource = shellMesh;
            }

            if (!_cachedShell.IsValid)
            {
                Debug.LogError($"[CardPack] 셸 메시 '{shellMesh.name}' 에 정점이나 삼각형이 없습니다.", this);
                return false;
            }

            // 셸은 높이 1 로 정규화돼 있다. 가로·두께는 비율에서 나오므로
            // 인스펙터 값을 실제와 맞춰 둔다 (직접 고쳐도 다음 Rebuild 에 덮인다).
            Vector3 shellSize = shellMesh.bounds.size;
            width     = shellSize.x * height;
            thickness = shellSize.z * height;

            float hw = width * 0.5f;
            var seam = new PackShellSlicer.TearSeam
            {
                baseY = MouthLocalY,
                halfWidth = hw,
                offsets = BuildSeamOffsets(),
            };

            Vector3 stripPivot = StripLocalHome;
            PackShellSlicer.Slice(_cachedShell, height, seam, stripPivot, _bodyMesh, _stripMesh);
            _strip.localPosition = stripPivot;

            ApplyMaterials(_body, 1);
            ApplyMaterials(_strip, 1);
            return true;
        }

        // ── 프리즘 폴백 ──────────────────────────────────────────────────

        void BuildPrism()
        {
            float hw = width * 0.5f;
            float hh = height * 0.5f;
            float r  = Mathf.Min(cornerRadius, Mathf.Min(hw, hh) * 0.4f);
            float seamY = MouthLocalY;

            List<Vector2> seam = BuildSeamPolyline(hw, seamY);

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

            ApplyMaterials(_body, 3);
            ApplyMaterials(_strip, 3);
        }

        // ── 이음매 ───────────────────────────────────────────────────────

        /// <summary>
        /// 톱니 오프셋을 등간격으로 샘플한다. 셸 슬라이서는 이 배열을 폴리라인으로
        /// 보간해서 쓰고, 프리즘은 그대로 외곽선 점으로 쓴다. 두 경로가 같은 값을
        /// 봐야 셸이든 판때기든 같은 모양으로 찢어진다.
        /// </summary>
        float[] BuildSeamOffsets()
        {
            int n = Mathf.Max(2, tearSegments);
            var offsets = new float[n + 1];

            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;

                // 굵은 물결 + 잔 톱니를 섞어야 가위질이 아니라 찢은 것처럼 보인다.
                float wave  = Hash(t * 6.3f, tearSeed) - 0.5f;
                float teeth = Hash(t * 27.1f, tearSeed * 3 + 7) - 0.5f;
                float jitter = (wave * 0.55f + teeth * 0.45f) * 2f * tearJitter;

                // 양 끝 15% 는 서서히 직선으로 수렴시킨다.
                float taper = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(t, 1f - t) / 0.15f));
                offsets[i] = jitter * taper;
            }
            return offsets;
        }

        /// <summary>지그재그 이음매를 (hw, seamY) → (-hw, seamY) 폴리라인으로.</summary>
        List<Vector2> BuildSeamPolyline(float hw, float seamY)
        {
            float[] offsets = BuildSeamOffsets();
            int n = offsets.Length - 1;

            var seam = new List<Vector2>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                seam.Add(new Vector2(Mathf.Lerp(hw, -hw, t), seamY + offsets[i]));
            }
            return seam;
        }

        // ── 공통 ─────────────────────────────────────────────────────────

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

        /// <summary>
        /// 서브메시 개수에 맞춰 머티리얼을 건다. 개수가 어긋나면 남는 서브메시가
        /// 통째로 안 그려진다 (프리즘은 앞/뒤/옆 3개, 셸은 1개).
        /// </summary>
        void ApplyMaterials(Transform piece, int slots)
        {
            if (piece == null) return;
            var renderer = piece.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            var mats = new Material[slots];
            for (int i = 0; i < slots; i++)
            {
                mats[i] = i == 1 ? (backMaterial != null ? backMaterial : frontMaterial)
                        : i == 2 ? (sideMaterial != null ? sideMaterial : frontMaterial)
                        : frontMaterial;
            }
            renderer.sharedMaterials = mats;
        }

        static float Hash(float x, int seed)
        {
            float n = Mathf.Sin(x * 127.1f + seed * 0.0137f) * 43758.5453f;
            return n - Mathf.Floor(n);
        }
    }
}
