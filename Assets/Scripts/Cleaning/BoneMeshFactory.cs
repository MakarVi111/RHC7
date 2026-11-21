using System.Collections.Generic;
using UnityEngine;

namespace RHC7
{
    /// <summary>
    /// Builds a low-poly long bone (femur-like) with clean cylindrical UVs.
    /// Cylindrical UVs are what the dirt-painting mask relies on.
    /// </summary>
    public static class BoneMeshFactory
    {
        public static Mesh CreateLongBone(
            float length = 0.30f,
            float radius = 0.021f,
            int lengthSegments = 72,
            int radialSegments = 36,
            int seed = 7)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            int R = Mathf.Max(8, radialSegments);
            int L = Mathf.Max(8, lengthSegments);
            float half = length * 0.5f;

            // Body rings.
            for (int i = 0; i <= L; i++)
            {
                float t = i / (float)L;
                float x = (t - 0.5f) * length;
                float end = Mathf.Pow(Mathf.Abs(t * 2f - 1f), 2.4f);
                float baseR = radius * (0.80f + 0.95f * end);
                baseR += radius * 0.28f * Mathf.Pow(Mathf.Abs(t * 2f - 1f), 7f);
                float bendZ = Mathf.Sin(t * Mathf.PI) * length * 0.020f;
                float bendY = Mathf.Sin(t * Mathf.PI) * length * 0.007f;

                for (int j = 0; j <= R; j++)
                {
                    float a = j / (float)R * Mathf.PI * 2f;
                    float lobe = 1f + 0.20f * end * Mathf.Cos(a * 2f + 0.45f);
                    float noise = (Mathf.PerlinNoise(a * 1.6f + seed * 0.71f, t * 7f + seed * 0.33f) - 0.5f) * 0.09f;
                    float r = baseR * lobe * (1f + noise);

                    verts.Add(new Vector3(x, Mathf.Cos(a) * r * 0.94f + bendY, Mathf.Sin(a) * r + bendZ));
                    uvs.Add(new Vector2(j / (float)R, t));
                }
            }

            for (int i = 0; i < L; i++)
            {
                for (int j = 0; j < R; j++)
                {
                    int a = i * (R + 1) + j;
                    int b = (i + 1) * (R + 1) + j;
                    int c = (i + 1) * (R + 1) + j + 1;
                    int d = i * (R + 1) + j + 1;
                    tris.Add(a); tris.Add(d); tris.Add(b);
                    tris.Add(d); tris.Add(c); tris.Add(b);
                }
            }

            AddRoundedCap(verts, uvs, tris, R, length, radius, seed, true, L);
            AddRoundedCap(verts, uvs, tris, R, length, radius, seed, false, L);

            var mesh = new Mesh { name = "Femur_LowPoly" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddRoundedCap(
            List<Vector3> verts,
            List<Vector2> uvs,
            List<int> tris,
            int R,
            float length,
            float radius,
            int seed,
            bool startCap,
            int L)
        {
            float half = length * 0.5f;
            float sign = startCap ? -1f : 1f;
            float t = startCap ? 0f : 1f;
            float end = 1f;
            float baseR = radius * (0.80f + 0.95f * end);
            baseR += radius * 0.28f;
            float capLen = radius * 1.15f;

            bool flip = !startCap;
            int capStart = verts.Count;

            // Three shrinking rings plus a pole, rounded like an epiphysis.
            int capRings = 3;
            for (int k = 1; k <= capRings; k++)
            {
                float ang = k / (float)(capRings + 1) * Mathf.PI * 0.5f;
                float scale = Mathf.Cos(ang);
                float x = sign * half + sign * Mathf.Sin(ang) * capLen;

                for (int j = 0; j <= R; j++)
                {
                    float a = j / (float)R * Mathf.PI * 2f;
                    float lobe = 1f + 0.20f * Mathf.Cos(a * 2f + 0.45f);
                    float noise = (Mathf.PerlinNoise(a * 1.6f + seed * 0.71f, t * 7f + seed * 0.33f) - 0.5f) * 0.06f;
                    float r = baseR * lobe * (1f + noise) * scale;
                    verts.Add(new Vector3(x, Mathf.Cos(a) * r * 0.94f, Mathf.Sin(a) * r));
                    uvs.Add(new Vector2(j / (float)R, startCap ? 0f : 1f));
                }
            }

            int poleIndex = verts.Count;
            verts.Add(new Vector3(sign * half + sign * capLen, 0f, 0f));
            uvs.Add(new Vector2(0.5f, startCap ? 0f : 1f));

            // Connect body end ring to the first cap ring.
            int bodyRing = startCap ? 0 : (R + 1) * L;
            ConnectRings(tris, bodyRing, capStart, R, flip);

            for (int k = 0; k < capRings - 1; k++)
            {
                ConnectRings(tris, capStart + k * (R + 1), capStart + (k + 1) * (R + 1), R, flip);
            }

            int lastRing = capStart + (capRings - 1) * (R + 1);
            for (int j = 0; j < R; j++)
            {
                tris.Add(lastRing + j);
                if (startCap)
                {
                    tris.Add(poleIndex);
                    tris.Add(lastRing + j + 1);
                }
                else
                {
                    tris.Add(lastRing + j + 1);
                    tris.Add(poleIndex);
                }
            }
        }

        private static void ConnectRings(List<int> tris, int ringA, int ringB, int R, bool flip)
        {
            for (int j = 0; j < R; j++)
            {
                int a = ringA + j;
                int b = ringA + j + 1;
                int c = ringB + j;
                int d = ringB + j + 1;

                if (flip)
                {
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
                else
                {
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }
        }
    }
}
