using System.Collections.Generic;
using UnityEngine;

namespace VRBoundaryDemo
{
    /// <summary>Strefa względem granicy: bezpiecznie, blisko krawędzi (ostrzeżenie), poza granicą.</summary>
    public enum BoundaryZone { Unknown, Safe, Warning, Outside }

    /// <summary>
    /// Wielokąt granicy na podłodze w PRZESTRZENI ŚLEDZENIA (tracking space) - czyli w tym samym układzie,
    /// w którym OVRBoundary.GetGeometry zwraca punkty, a OVRCameraRig ustawia głowę i kontrolery.
    /// Liczy (w płaszczyźnie XZ): czy punkt jest wewnątrz, odległość od krawędzi i najbliższy punkt krawędzi.
    /// Zastępuje przestarzałe OVRBoundary.TestNode / TestPoint (niewspierane w backendzie OpenXR).
    /// </summary>
    public class BoundaryShape
    {
        readonly List<Vector3> points = new List<Vector3>();

        public IReadOnlyList<Vector3> Points => points;
        public int Count => points.Count;

        /// <summary>Wielokąt ma sens dopiero od 3 punktów (zawsze traktowany jako zamknięty).</summary>
        public bool IsValid => points.Count >= 3;

        public void Set(IEnumerable<Vector3> source)
        {
            points.Clear();
            if (source == null) return;
            foreach (var p in source) points.Add(new Vector3(p.x, 0f, p.z));
        }

        public void Add(Vector3 p) => points.Add(new Vector3(p.x, 0f, p.z));

        public void RemoveLast()
        {
            if (points.Count > 0) points.RemoveAt(points.Count - 1);
        }

        public void Clear() => points.Clear();

        public Vector3 this[int i]
        {
            get => points[i];
            set => points[i] = new Vector3(value.x, 0f, value.z);
        }

        /// <summary>Test "ray casting" (parzystość przecięć) w płaszczyźnie XZ.</summary>
        public bool Contains(Vector3 p)
        {
            if (!IsValid) return false;
            bool inside = false;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                Vector3 a = points[i], b = points[j];
                if ((a.z > p.z) != (b.z > p.z))
                {
                    float x = (b.x - a.x) * (p.z - a.z) / (b.z - a.z) + a.x;
                    if (p.x < x) inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>
        /// Odległość (XZ) od najbliższej krawędzi wielokąta. Zwraca też najbliższy punkt (y = 0)
        /// i indeks krawędzi (krawędź i łączy punkt i z punktem i+1).
        /// </summary>
        public float DistanceToEdge(Vector3 p, out Vector3 closest, out int edge)
        {
            closest = Vector3.zero;
            edge = -1;
            if (points.Count < 2) return float.PositiveInfinity;

            float best = float.PositiveInfinity;
            Vector3 flat = new Vector3(p.x, 0f, p.z);
            int n = IsValid ? points.Count : points.Count - 1;
            for (int i = 0; i < n; i++)
            {
                Vector3 c = ClosestOnSegment(flat, points[i], points[(i + 1) % points.Count]);
                float d = (flat - c).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    closest = c;
                    edge = i;
                }
            }
            return Mathf.Sqrt(best);
        }

        /// <summary>Odległość od krawędzi o danym indeksie (do wizualizacji ścian).</summary>
        public float DistanceToEdge(Vector3 p, int edge)
        {
            Vector3 flat = new Vector3(p.x, 0f, p.z);
            Vector3 c = ClosestOnSegment(flat, points[edge], points[(edge + 1) % points.Count]);
            return (flat - c).magnitude;
        }

        /// <summary>Strefa punktu: Outside poza wielokątem, Warning bliżej krawędzi niż warnDistance, inaczej Safe.</summary>
        public BoundaryZone Classify(Vector3 p, float warnDistance, out float distance, out Vector3 closest)
        {
            distance = DistanceToEdge(p, out closest, out _);
            if (!IsValid) return BoundaryZone.Unknown;
            if (!Contains(p)) return BoundaryZone.Outside;
            return distance < warnDistance ? BoundaryZone.Warning : BoundaryZone.Safe;
        }

        public float Perimeter()
        {
            float sum = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                if (!IsValid && i == points.Count - 1) break;
                sum += Vector3.Distance(points[i], points[(i + 1) % points.Count]);
            }
            return sum;
        }

        /// <summary>Pole wielokąta (wzór "sznurowadłowy"), m².</summary>
        public float Area()
        {
            if (!IsValid) return 0f;
            float s = 0f;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
                s += points[j].x * points[i].z - points[i].x * points[j].z;
            return Mathf.Abs(s) * 0.5f;
        }

        /// <summary>Rozmiar prostokąta otaczającego (szerokość X, głębokość Z).</summary>
        public Vector2 BoundsSize()
        {
            if (points.Count == 0) return Vector2.zero;
            Vector3 min = points[0], max = points[0];
            foreach (var p in points)
            {
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            return new Vector2(max.x - min.x, max.z - min.z);
        }

        static Vector3 ClosestOnSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-8f) return a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
            return a + ab * t;
        }
    }
}
