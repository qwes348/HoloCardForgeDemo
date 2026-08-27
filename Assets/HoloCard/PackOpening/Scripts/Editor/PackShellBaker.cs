using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoloCard.PackOpening.Editor
{
    /// <summary>
    /// 스케치팹에서 받은 카드팩 FBX 를 프로젝트 규약에 맞는 "셸 메시" 로 굽는다.
    ///
    /// 절차 메시(<see cref="HoloCardPrism"/>)로는 못 만드는 것이 두 가지 있다.
    ///   - 위아래로 눌린 크림프(압착 밀봉). 팩이 "봉지"로 읽히는 실루엣의 핵심.
    ///   - 옆으로 둥글게 부푼 단면. 평면 프리즘은 시선 벡터가 면 전체에서 같아서
    ///     시트 반사가 균일한 띠로 깔리는데, 곡면이면 그 띠가 몸통을 감아 돈다.
    ///     "비닐" 로 읽히느냐 "코팅된 종이" 로 읽히느냐가 여기서 갈린다.
    ///
    /// 원본을 그대로 쓸 수 없는 이유
    ///   - 축이 다르다. 루트에 X 90도 + 비균등 스케일(100, 65, 42)이 걸려 있고
    ///     긴 축이 X 다. 프로젝트 규약은 긴 축 Y, 두께 Z, 앞면 -Z.
    ///   - UV 가 원본 사진(포켓몬 팩 실사) 전용이다. 텍스처 안의 특정 사각형
    ///     (u 0.353~0.644 / v 0.030~0.968)에만 걸쳐 있어서 우리 아트가 안 맞는다.
    ///     다행히 순수 평행투영이라 정점 좌표에서 다시 계산할 수 있다.
    ///
    /// 결과물은 높이 1 로 정규화된 단일 서브메시 메시다. 크기는 CardPack 이 준다.
    /// </summary>
    public static class PackShellBaker
    {
        public const string SourcePath = "Assets/HoloCard/Model/trading-card-pack/packmodel/source/cardpack2.fbx";
        const string OutputDir  = "Assets/HoloCard/PackOpening/Meshes";
        public const string OutputPath = OutputDir + "/PackShell.asset";

        /// <summary>
        /// 가운데를 부풀리는 양. 원본은 두께가 일정한 판에 모서리만 둥근 형태라
        /// 정면에서 보면 결국 카드처럼 납작하다. 실제 팩은 안에 든 카드가 필름을
        /// 밀어내서 가운데가 볼록하고, 그래야 하이라이트가 한 줄로 지나가지 않고
        /// 몸통을 타고 휜다. 0.6 = 가운데 두께 1.6배.
        /// </summary>
        const float Bulge = 0.6f;

        /// <summary>
        /// 법선 이웃 평균 횟수. 원본은 평평한 앞면과 둥근 베벨이 각지게 맞닿아 있어서
        /// 그 경계선이 소프트박스 반사에 사각형 테두리로 드러난다.
        ///
        /// **많이 돌린다고 좋아지지 않는다.** 이 메시는 베벨 쪽 정점 밀도가 앞면보다
        /// 훨씬 높아서, 우산 연산자가 법선을 촘촘한 쪽으로 끌어당긴다. 열 번쯤 넘기면
        /// 원래 꺾임 대신 밀도 경계에 새 능선이 생겨 오히려 더 또렷해진다. 5 정도가
        /// 꺾임은 눕히고 새 능선은 안 만드는 지점.
        /// </summary>
        const int NormalSmoothPasses = 5;

        [MenuItem("Tools/Holo Card/Bake Pack Shell", false, 39)]
        public static void BakeMenu()
        {
            Mesh shell = Bake();
            if (shell == null) return;

            Selection.activeObject = shell;
            Vector3 s = shell.bounds.size;
            Debug.Log($"[Pack Opening] 셸 메시 생성 완료: {OutputPath}\n" +
                      $"정점 {shell.vertexCount} / 삼각형 {shell.triangles.Length / 3} / " +
                      $"비율 가로 {s.x:F3} × 높이 {s.y:F3} × 두께 {s.z:F3}");
        }

        /// <summary>구워 둔 셸이 있으면 그걸 쓰고, 없으면 굽는다.</summary>
        public static Mesh LoadOrBake()
        {
            Mesh cached = AssetDatabase.LoadAssetAtPath<Mesh>(OutputPath);
            return cached != null ? cached : Bake();
        }

        public static Mesh Bake()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == null)
            {
                Debug.LogError($"[Pack Opening] 팩 모델을 찾을 수 없습니다: {SourcePath}");
                return null;
            }

            MeshFilter filter = source.GetComponentInChildren<MeshFilter>();
            Mesh raw = filter != null ? filter.sharedMesh : null;
            if (raw == null)
            {
                Debug.LogError("[Pack Opening] FBX 안에 메시가 없습니다.");
                return null;
            }

            // 프리팹 루트는 항등이라 이게 곧 모델 공간 변환이다 (X 90도 + 비균등 스케일).
            Matrix4x4 toModel  = filter.transform.localToWorldMatrix;
            Matrix4x4 toNormal = toModel.inverse.transpose;   // 비균등 스케일이라 역전치가 필요하다

            Vector3[] srcVerts   = raw.vertices;
            Vector3[] srcNormals = raw.normals;
            int[]     srcTris    = raw.triangles;

            int count = srcVerts.Length;
            var verts   = new Vector3[count];
            var normals = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                verts[i]   = SwizzleAxes(toModel.MultiplyPoint3x4(srcVerts[i]));
                normals[i] = SwizzleAxes(toNormal.MultiplyVector(srcNormals[i])).normalized;
            }

            // 높이 1 로 균등 정규화. 가로·두께 비율은 모델 그대로 둔다.
            Bounds box = Encapsulate(verts);
            float scale = 1f / Mathf.Max(box.size.y, 1e-5f);
            for (int i = 0; i < count; i++)
                verts[i] = (verts[i] - box.center) * scale;

            Bounds norm = Encapsulate(verts);
            Vector3 min = norm.min, size = norm.size;

            // 가운데를 볼록하게. 두께(z)를 통째로 곱하는 방식이라 실루엣(u = 0, 1)과
            // 이미 눌려 있는 크림프는 z 가 0 에 가까워 저절로 제자리에 남는다.
            if (Bulge > 1e-4f)
            {
                for (int i = 0; i < count; i++)
                {
                    float u = (verts[i].x - min.x) / Mathf.Max(size.x, 1e-5f);
                    float dome = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 1.4f);
                    verts[i].z *= 1f + Bulge * dome;
                }
                RecalculateSmoothNormals(verts, srcTris, normals, NormalSmoothPasses);
            }

            var uvs      = new Vector2[count];
            var tangents = new Vector4[count];

            for (int i = 0; i < count; i++)
            {
                // 원본과 같은 평행투영. 크림프 구간에서 아트가 눌리는 것까지 실물과 같다
                // (실제 팩도 밀봉하면서 인쇄가 접혀 들어간다).
                uvs[i] = new Vector2((verts[i].x - min.x) / Mathf.Max(size.x, 1e-5f),
                                     (verts[i].y - min.y) / Mathf.Max(size.y, 1e-5f));
                tangents[i] = BuildTangent(normals[i]);
            }

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(OutputPath);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();

            mesh.Clear();
            mesh.name = "PackShell";
            mesh.indexFormat = count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.uv = uvs;
            // 축 교환(x,y,z) → (z,x,y) 은 행렬식이 +1 이라 감김이 유지된다.
            mesh.triangles = srcTris;
            mesh.RecalculateBounds();

            if (isNew)
            {
                Directory.CreateDirectory(OutputDir);
                AssetDatabase.CreateAsset(mesh, OutputPath);
            }
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            return mesh;
        }

        /// <summary>모델 (긴축 X, 두께 Y, 가로 Z) → 프로젝트 (가로 X, 높이 Y, 두께 Z).</summary>
        static Vector3 SwizzleAxes(Vector3 p) => new Vector3(p.z, p.x, p.y);

        /// <summary>
        /// 부풀린 뒤 법선을 다시 잡는다.
        ///
        /// Mesh.RecalculateNormals 를 그대로 부르면 안 된다. 원본이 UV 이음매마다
        /// 정점을 쪼개 놔서 그 선이 전부 각진다. 위치로 용접해서 평균한다.
        ///
        /// 그 다음 이웃 평균을 몇 번 돌린다(<paramref name="smoothPasses"/>). 원본은
        /// 완전히 평평한 앞면과 둥근 베벨이 맞닿는 구조라 그 경계에서 법선의
        /// **기울기**가 툭 꺾인다. 확산광에서는 안 보이지만, 가장자리가 또렷한
        /// 소프트박스 반사는 법선 오차를 두 배로 키워 보여 주기 때문에 팩 한가운데에
        /// 사각형 테두리가 그대로 드러난다. 몇 칸에 걸쳐 펴 주면 사라진다.
        /// </summary>
        static void RecalculateSmoothNormals(Vector3[] verts, int[] tris, Vector3[] normals, int smoothPasses)
        {
            var buckets = new Dictionary<Vector3Int, int>(verts.Length);
            var group = new int[verts.Length];
            var accumulated = new List<Vector3>();

            for (int i = 0; i < verts.Length; i++)
            {
                var key = new Vector3Int(Mathf.RoundToInt(verts[i].x * 100000f),
                                         Mathf.RoundToInt(verts[i].y * 100000f),
                                         Mathf.RoundToInt(verts[i].z * 100000f));
                if (!buckets.TryGetValue(key, out int g))
                {
                    g = accumulated.Count;
                    buckets.Add(key, g);
                    accumulated.Add(Vector3.zero);
                }
                group[i] = g;
            }

            int groupCount = accumulated.Count;

            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                Vector3 faceNormal = Vector3.Cross(b - a, c - a);   // 넓이로 가중된다
                accumulated[group[tris[t]]]     += faceNormal;
                accumulated[group[tris[t + 1]]] += faceNormal;
                accumulated[group[tris[t + 2]]] += faceNormal;
            }

            var smoothed = new Vector3[groupCount];
            for (int g = 0; g < groupCount; g++)
                smoothed[g] = accumulated[g].sqrMagnitude > 1e-12f ? accumulated[g].normalized : Vector3.forward;

            if (smoothPasses > 0)
            {
                // 삼각형 변에서 이웃 관계를 뽑는다 (양방향, 중복 허용 — 가중치가 될 뿐).
                var neighborStart = new int[groupCount + 1];
                int triCount = tris.Length / 3;
                for (int t = 0; t < triCount; t++)
                {
                    int a = group[tris[t * 3]], b = group[tris[t * 3 + 1]], c = group[tris[t * 3 + 2]];
                    neighborStart[a] += 2; neighborStart[b] += 2; neighborStart[c] += 2;
                }
                int total = 0;
                for (int g = 0; g < groupCount; g++) { int n = neighborStart[g]; neighborStart[g] = total; total += n; }
                neighborStart[groupCount] = total;

                var cursor = (int[])neighborStart.Clone();
                var neighbors = new int[total];
                for (int t = 0; t < triCount; t++)
                {
                    int a = group[tris[t * 3]], b = group[tris[t * 3 + 1]], c = group[tris[t * 3 + 2]];
                    neighbors[cursor[a]++] = b; neighbors[cursor[a]++] = c;
                    neighbors[cursor[b]++] = c; neighbors[cursor[b]++] = a;
                    neighbors[cursor[c]++] = a; neighbors[cursor[c]++] = b;
                }

                var next = new Vector3[groupCount];
                for (int pass = 0; pass < smoothPasses; pass++)
                {
                    for (int g = 0; g < groupCount; g++)
                    {
                        Vector3 sum = Vector3.zero;
                        for (int k = neighborStart[g]; k < neighborStart[g + 1]; k++) sum += smoothed[neighbors[k]];
                        if (sum.sqrMagnitude < 1e-12f) { next[g] = smoothed[g]; continue; }
                        next[g] = Vector3.Lerp(smoothed[g], sum.normalized, 0.5f).normalized;
                    }
                    (smoothed, next) = (next, smoothed);
                }
            }

            // 감김 규약을 가정하지 않고 원본 법선과 맞춰 본 뒤 필요하면 통째로 뒤집는다.
            float agreement = 0f;
            for (int i = 0; i < verts.Length; i++)
                agreement += Vector3.Dot(smoothed[group[i]], normals[i]);
            float sign = agreement >= 0f ? 1f : -1f;

            for (int i = 0; i < verts.Length; i++)
                normals[i] = smoothed[group[i]] * sign;
        }

        /// <summary>
        /// UV 가 XY 평행투영이라 u 방향은 언제나 오브젝트 +X, v 방향은 +Y 다.
        /// 그걸 접평면에 투영하면 곡면 어디서나 일관된 탄젠트가 나온다.
        /// RecalculateTangents 는 옆면(투영 방향과 나란한 면)에서 UV 넓이가 0 이라
        /// 쓰레기 값을 뱉으므로 쓰지 않는다.
        /// </summary>
        static Vector4 BuildTangent(Vector3 n)
        {
            Vector3 t = Vector3.right - n * n.x;
            if (t.sqrMagnitude < 1e-6f) t = Vector3.Cross(n, Vector3.up);   // 법선이 ±X 인 실루엣 선
            if (t.sqrMagnitude < 1e-6f) t = Vector3.right;
            t.Normalize();

            // 바이탄젠트 = cross(n, t) * w 가 +v(=+Y) 를 향하도록 w 를 고른다.
            // 앞면(-Z)에서는 -1 이 나와 카드와 같은 (1,0,0,-1) 규약이 된다.
            float w = Vector3.Dot(Vector3.Cross(n, t), Vector3.up) >= 0f ? 1f : -1f;
            return new Vector4(t.x, t.y, t.z, w);
        }

        static Bounds Encapsulate(Vector3[] points)
        {
            var box = new Bounds(points[0], Vector3.zero);
            for (int i = 1; i < points.Length; i++) box.Encapsulate(points[i]);
            return box;
        }
    }
}
