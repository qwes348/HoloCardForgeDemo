using System.Collections.Generic;
using UnityEngine;

namespace HoloCard
{
    /// <summary>
    /// 닫힌 외곽선을 두께가 있는 프리즘 메시로 밀어낸다.
    /// 카드(<see cref="HoloCardMesh"/>)와 팩 포장지가 같은 코드를 쓴다.
    ///
    /// 규약
    ///   - 외곽선은 XY 평면에서 CCW.
    ///   - 앞면은 -Z 를 본다 (Unity 기본 Quad 와 같다). 탄젠트는 (1,0,0,-1).
    ///   - 서브메시 0 = 앞면, 1 = 뒷면, 2 = 옆면.
    ///     뒷면과 옆면을 나눠 둬야 카드 뒷면 아트를 옆면에 늘리지 않고 물릴 수 있다.
    /// </summary>
    public static class HoloCardPrism
    {
        /// <param name="outline">XY 평면의 CCW 외곽선. 첫 점과 끝 점을 잇는다.</param>
        /// <param name="thickness">두께. 0 이면 옆면을 만들지 않는다.</param>
        /// <param name="pivot">이 좌표가 메시의 원점이 된다.</param>
        /// <param name="uvOrigin">UV 0 에 대응하는 외곽선 좌표.</param>
        /// <param name="uvSize">UV 1 까지의 크기. 조각을 잘라 써도 아트가 이어지게 한다.</param>
        public static void Build(Mesh mesh, IList<Vector2> outline, float thickness,
                                 Vector2 pivot, Vector2 uvOrigin, Vector2 uvSize)
        {
            mesh.Clear();

            int n = outline.Count;
            if (n < 3) return;

            float hz = thickness * 0.5f;
            float invU = 1f / Mathf.Max(uvSize.x, 1e-5f);
            float invV = 1f / Mathf.Max(uvSize.y, 1e-5f);

            var verts    = new List<Vector3>();
            var normals  = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uvs      = new List<Vector2>();
            var frontTris = new List<int>();
            var backTris  = new List<int>();
            var rimTris   = new List<int>();

            Vector2 centroid = Vector2.zero;
            for (int i = 0; i < n; i++) centroid += outline[i];
            centroid /= n;

            Vector2 UV(Vector2 p) => new Vector2((p.x - uvOrigin.x) * invU, (p.y - uvOrigin.y) * invV);

            // ── 앞면 (-Z). 중심에서 팬으로 감는다.
            int frontBase = verts.Count;
            verts.Add(new Vector3(centroid.x - pivot.x, centroid.y - pivot.y, -hz));
            normals.Add(new Vector3(0f, 0f, -1f));
            tangents.Add(new Vector4(1f, 0f, 0f, -1f));
            uvs.Add(UV(centroid));

            for (int i = 0; i < n; i++)
            {
                Vector2 p = outline[i];
                verts.Add(new Vector3(p.x - pivot.x, p.y - pivot.y, -hz));
                normals.Add(new Vector3(0f, 0f, -1f));
                tangents.Add(new Vector4(1f, 0f, 0f, -1f));
                uvs.Add(UV(p));
            }

            // 외곽선이 CCW 이므로 -Z 에서 볼 때 CW 가 되도록 뒤집어 감는다.
            for (int i = 0; i < n; i++)
            {
                frontTris.Add(frontBase);
                frontTris.Add(frontBase + 1 + (i + 1) % n);
                frontTris.Add(frontBase + 1 + i);
            }

            // ── 뒷면 (+Z)
            int backBase = verts.Count;
            verts.Add(new Vector3(centroid.x - pivot.x, centroid.y - pivot.y, hz));
            normals.Add(new Vector3(0f, 0f, 1f));
            tangents.Add(new Vector4(-1f, 0f, 0f, -1f));
            uvs.Add(new Vector2(1f - UV(centroid).x, UV(centroid).y));

            for (int i = 0; i < n; i++)
            {
                Vector2 p = outline[i];
                Vector2 uv = UV(p);
                verts.Add(new Vector3(p.x - pivot.x, p.y - pivot.y, hz));
                normals.Add(new Vector3(0f, 0f, 1f));
                tangents.Add(new Vector4(-1f, 0f, 0f, -1f));
                // 뒷면은 좌우가 뒤집혀 보이므로 U 를 미러링해 아트가 바로 서게 한다.
                uvs.Add(new Vector2(1f - uv.x, uv.y));
            }

            for (int i = 0; i < n; i++)
            {
                backTris.Add(backBase);
                backTris.Add(backBase + 1 + i);
                backTris.Add(backBase + 1 + (i + 1) % n);
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
                    if (edge.sqrMagnitude < 1e-8f) continue;

                    // CCW 외곽선이므로 바깥쪽은 (edge.y, -edge.x).
                    Vector3 nrm = new Vector3(edge.y, -edge.x, 0f).normalized;
                    Vector4 tan = new Vector4(edge.x, edge.y, 0f, -1f);

                    float uA = cumulative[i] / perimeter;
                    float uB = cumulative[i + 1] / perimeter;

                    int baseIdx = verts.Count;
                    verts.Add(new Vector3(pA.x - pivot.x, pA.y - pivot.y, -hz));
                    verts.Add(new Vector3(pB.x - pivot.x, pB.y - pivot.y, -hz));
                    verts.Add(new Vector3(pB.x - pivot.x, pB.y - pivot.y,  hz));
                    verts.Add(new Vector3(pA.x - pivot.x, pA.y - pivot.y,  hz));

                    for (int k = 0; k < 4; k++) { normals.Add(nrm); tangents.Add(tan); }
                    uvs.Add(new Vector2(uA, 0f));
                    uvs.Add(new Vector2(uB, 0f));
                    uvs.Add(new Vector2(uB, 1f));
                    uvs.Add(new Vector2(uA, 1f));

                    rimTris.Add(baseIdx + 0); rimTris.Add(baseIdx + 1); rimTris.Add(baseIdx + 2);
                    rimTris.Add(baseIdx + 0); rimTris.Add(baseIdx + 2); rimTris.Add(baseIdx + 3);
                }
            }

            mesh.indexFormat = verts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(frontTris, 0);
            mesh.SetTriangles(backTris, 1);
            mesh.SetTriangles(rimTris, 2);
            mesh.RecalculateBounds();
        }

        /// <summary>둥근 사각형 외곽선을 XY 평면에 CCW 로 만든다.</summary>
        public static List<Vector2> RoundedRect(float halfWidth, float halfHeight, float radius, int segments)
        {
            var pts = new List<Vector2>();

            if (radius <= 1e-5f)
            {
                pts.Add(new Vector2( halfWidth, -halfHeight));
                pts.Add(new Vector2( halfWidth,  halfHeight));
                pts.Add(new Vector2(-halfWidth,  halfHeight));
                pts.Add(new Vector2(-halfWidth, -halfHeight));
                return pts;
            }

            float ix = halfWidth - radius;
            float iy = halfHeight - radius;

            // 코너 중심을 CCW 로: 우하 → 우상 → 좌상 → 좌하
            Vector2[] centers = { new Vector2(ix, -iy), new Vector2(ix, iy), new Vector2(-ix, iy), new Vector2(-ix, -iy) };
            float[] startAngle = { -90f, 0f, 90f, 180f };

            for (int c = 0; c < 4; c++)
            {
                for (int s = 0; s <= segments; s++)
                {
                    float a = (startAngle[c] + 90f * s / segments) * Mathf.Deg2Rad;
                    pts.Add(centers[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                }
            }
            return pts;
        }

        /// <summary>코너 두 곳만 둥근 사각형. 찢어진 이음매가 있는 조각에 쓴다.</summary>
        public static void AppendArc(List<Vector2> into, Vector2 center, float radius,
                                     float startDegrees, float sweepDegrees, int segments)
        {
            for (int s = 0; s <= segments; s++)
            {
                float a = (startDegrees + sweepDegrees * s / segments) * Mathf.Deg2Rad;
                into.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
        }
    }
}
