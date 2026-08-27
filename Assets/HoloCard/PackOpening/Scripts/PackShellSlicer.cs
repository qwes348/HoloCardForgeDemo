using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoloCard.PackOpening
{
    /// <summary>
    /// 닫힌 팩 셸을 뜯는 선(<see cref="TearSeam"/>)으로 두 조각으로 자른다.
    ///
    /// 자른 자리에 뚜껑을 덮지 않는다. 봉지가 열렸으면 안이 보이는 게 맞고,
    /// 이 모델은 크림프에서 앞뒷면이 맞닿아 있어 비다양체(non-manifold) 엣지가
    /// 2500 개쯤 된다. 절단면 루프를 따라 걷는 방식은 거기서 반드시 터진다.
    /// 대신 필름 셰이더를 양면으로 그리고 뒷면을 어둡게 죽여 "봉지 속" 을 만든다.
    /// </summary>
    public static class PackShellSlicer
    {
        /// <summary>
        /// 뜯는 선. 톱니를 연속 함수로 두면(해시를 그대로 부르면) 삼각형마다
        /// 백색잡음이 걸려 절단면이 지저분해진다. 프리즘 경로와 똑같이 등간격으로
        /// 샘플한 폴리라인을 만들어 두고 그 사이만 선형 보간한다.
        /// </summary>
        public struct TearSeam
        {
            public float baseY;
            public float halfWidth;
            /// <summary>t=0 이 x=+halfWidth, t=1 이 x=-halfWidth.</summary>
            public float[] offsets;

            public float YAt(float x)
            {
                if (offsets == null || offsets.Length < 2) return baseY;

                int n = offsets.Length - 1;
                float t = Mathf.Clamp01((halfWidth - x) / Mathf.Max(halfWidth * 2f, 1e-5f)) * n;
                int i = Mathf.Min((int)t, n - 1);
                return baseY + Mathf.Lerp(offsets[i], offsets[i + 1], t - i);
            }
        }

        struct Vertex
        {
            public Vector3 position;
            public Vector3 normal;
            public Vector4 tangent;
            public Vector2 uv;
        }

        /// <summary>원본 셸(높이 1 로 정규화된 것)의 정점 배열 묶음.</summary>
        public struct Shell
        {
            public Vector3[] positions;
            public Vector3[] normals;
            public Vector4[] tangents;
            public Vector2[] uvs;
            public int[] triangles;

            public bool IsValid => positions != null && triangles != null && positions.Length > 0;
        }

        static readonly List<Vertex> Upper = new List<Vertex>(4);
        static readonly List<Vertex> Lower = new List<Vertex>(4);

        /// <param name="scale">셸은 높이 1 로 정규화돼 있다. 실제 높이를 곱해 준다.</param>
        /// <param name="stripPivot">위쪽 조각의 원점. 제자리에서 돌게 하려면 자기 중심이어야 한다.</param>
        public static void Slice(in Shell shell, float scale, in TearSeam seam,
                                 Vector3 stripPivot, Mesh bodyMesh, Mesh stripMesh)
        {
            var body  = new Builder(Vector3.zero, shell.positions.Length);
            var strip = new Builder(stripPivot,   shell.positions.Length);

            int[] tris = shell.triangles;
            var corner = new Vertex[3];
            var dist   = new float[3];
            var index  = new int[3];

            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int above = 0;
                for (int k = 0; k < 3; k++)
                {
                    index[k]  = tris[t + k];
                    corner[k] = Read(shell, index[k], scale);
                    dist[k]   = corner[k].position.y - seam.YAt(corner[k].position.x);
                    if (dist[k] > 0f) above++;
                }

                if (above == 0) { body.AddSourceTriangle(shell, scale, index[0], index[1], index[2]); continue; }
                if (above == 3) { strip.AddSourceTriangle(shell, scale, index[0], index[1], index[2]); continue; }

                // 경계를 물고 있는 삼각형 → 위/아래 다각형으로 쪼갠다.
                Upper.Clear();
                Lower.Clear();
                for (int k = 0; k < 3; k++)
                {
                    int k2 = (k + 1) % 3;
                    bool aUp = dist[k]  > 0f;
                    bool bUp = dist[k2] > 0f;

                    if (aUp) Upper.Add(corner[k]); else Lower.Add(corner[k]);
                    if (aUp == bUp) continue;

                    Vertex cut = Intersect(corner[k], corner[k2], dist[k], dist[k2], seam);
                    Upper.Add(cut);
                    Lower.Add(cut);
                }

                strip.AddFan(Upper);
                body.AddFan(Lower);
            }

            body.Fill(bodyMesh);
            strip.Fill(stripMesh);
        }

        static Vertex Read(in Shell shell, int i, float scale) => new Vertex
        {
            position = shell.positions[i] * scale,
            normal   = shell.normals  != null && shell.normals.Length  > i ? shell.normals[i]  : Vector3.back,
            tangent  = shell.tangents != null && shell.tangents.Length > i ? shell.tangents[i] : new Vector4(1f, 0f, 0f, -1f),
            uv       = shell.uvs      != null && shell.uvs.Length      > i ? shell.uvs[i]      : Vector2.zero,
        };

        /// <summary>
        /// 이음매가 폴리라인이라 선형 추정만으로는 교점이 어긋난다.
        /// 가위치법으로 네 번 조이면 톱니 한 칸보다 훨씬 작게 수렴한다.
        /// </summary>
        static Vertex Intersect(in Vertex a, in Vertex b, float da, float db, in TearSeam seam)
        {
            float lo = 0f, hi = 1f;
            float dLo = da, dHi = db;
            float s = Mathf.Clamp01(dLo / Mathf.Max(dLo - dHi, 1e-6f));

            for (int i = 0; i < 4; i++)
            {
                Vector3 p = Vector3.Lerp(a.position, b.position, s);
                float d = p.y - seam.YAt(p.x);
                if ((d > 0f) == (dLo > 0f)) { lo = s; dLo = d; }
                else                        { hi = s; dHi = d; }

                float span = dLo - dHi;
                if (Mathf.Abs(span) < 1e-7f) break;
                s = Mathf.Clamp(lo + (hi - lo) * (dLo / span), lo, hi);
            }

            Vector3 tan = Vector3.Lerp(a.tangent, b.tangent, s);
            return new Vertex
            {
                position = Vector3.Lerp(a.position, b.position, s),
                normal   = Vector3.Slerp(a.normal, b.normal, s),
                tangent  = new Vector4(tan.x, tan.y, tan.z, a.tangent.w),
                uv       = Vector2.Lerp(a.uv, b.uv, s),
            };
        }

        /// <summary>
        /// 한 조각의 버텍스 버퍼. 잘리지 않은 삼각형은 원본 인덱스를 재사용해서
        /// 정점이 세 배로 불어나는 걸 막는다 (셸이 2만 삼각형이라 무시 못 한다).
        /// </summary>
        sealed class Builder
        {
            readonly Vector3 _pivot;
            readonly int[] _remap;
            readonly List<Vector3> _positions = new List<Vector3>();
            readonly List<Vector3> _normals   = new List<Vector3>();
            readonly List<Vector4> _tangents  = new List<Vector4>();
            readonly List<Vector2> _uvs       = new List<Vector2>();
            readonly List<int>     _triangles = new List<int>();

            public Builder(Vector3 pivot, int sourceVertexCount)
            {
                _pivot = pivot;
                _remap = new int[sourceVertexCount];
                for (int i = 0; i < _remap.Length; i++) _remap[i] = -1;
            }

            public void AddSourceTriangle(in Shell shell, float scale, int i0, int i1, int i2)
            {
                _triangles.Add(Reuse(shell, scale, i0));
                _triangles.Add(Reuse(shell, scale, i1));
                _triangles.Add(Reuse(shell, scale, i2));
            }

            public void AddFan(List<Vertex> polygon)
            {
                if (polygon.Count < 3) return;

                int first = Append(polygon[0]);
                int prev  = Append(polygon[1]);
                for (int i = 2; i < polygon.Count; i++)
                {
                    int cur = Append(polygon[i]);
                    _triangles.Add(first);
                    _triangles.Add(prev);
                    _triangles.Add(cur);
                    prev = cur;
                }
            }

            int Reuse(in Shell shell, float scale, int source)
            {
                int existing = _remap[source];
                if (existing >= 0) return existing;

                int added = Append(Read(shell, source, scale));
                _remap[source] = added;
                return added;
            }

            int Append(in Vertex v)
            {
                _positions.Add(v.position - _pivot);
                _normals.Add(v.normal);
                _tangents.Add(v.tangent);
                _uvs.Add(v.uv);
                return _positions.Count - 1;
            }

            public void Fill(Mesh mesh)
            {
                mesh.Clear();
                if (_positions.Count == 0) return;

                mesh.indexFormat = _positions.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(_positions);
                mesh.SetNormals(_normals);
                mesh.SetTangents(_tangents);
                mesh.SetUVs(0, _uvs);
                mesh.subMeshCount = 1;
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateBounds();
            }
        }
    }
}
