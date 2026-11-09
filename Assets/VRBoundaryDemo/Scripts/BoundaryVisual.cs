using System.Collections.Generic;
using UnityEngine;

namespace VRBoundaryDemo
{
    /// <summary>
    /// Rysuje wielokąt granicy z prymitywów: linię na podłodze, słupki w narożnikach i półprzezroczyste ściany.
    /// Obiekty są dziećmi przestrzeni śledzenia (rig.trackingSpace), więc granica zostaje w miejscu
    /// fizycznego pokoju także wtedy, gdy gracz przesuwa się gałką (przesuwany jest cały rig).
    /// Ściana pojawia się, gdy zbliża się do niej głowa lub kontroler (jak systemowy Guardian).
    /// </summary>
    public class BoundaryVisual : MonoBehaviour
    {
        [Header("Wygląd")]
        public Color color = new Color(0.2f, 0.85f, 1f);
        public float lineWidth = 0.03f;
        public float wallHeight = 2.2f;
        [Tooltip("Pionowe przesunięcie linii (żeby nie migotała z podłogą sceny).")]
        public float lineLift = 0.006f;

        [Header("Ściany")]
        public bool showWalls = true;
        [Range(0f, 1f)] public float idleWallAlpha = 0.05f;
        [Range(0f, 1f)] public float maxWallAlpha = 0.55f;
        [Tooltip("Od jakiej odległości (m) ściana zaczyna być widoczna.")]
        public float fadeDistance = 0.9f;

        [Header("Stan")]
        public bool visible = true;
        [Tooltip("Pokaż wszystkie ściany w pełni (np. przytrzymany lewy grip).")]
        public bool revealAll;

        /// <summary>Opcjonalny dodatkowy odcinek "w trakcie rysowania" (od ostatniego punktu do celownika).</summary>
        public bool previewActive;
        public Vector3 previewEnd;

        BoundaryShape shape;
        Transform root;
        readonly List<Transform> lines = new List<Transform>();
        readonly List<ColorTint> walls = new List<ColorTint>();
        readonly List<Transform> posts = new List<Transform>();
        Transform preview;
        int builtCount = -1;
        int builtHash;

        public void SetShape(BoundaryShape s)
        {
            shape = s;
            builtCount = -1; // wymuś przebudowę
        }

        /// <summary>Wywołaj po zmianie punktów wielokąta.</summary>
        public void MarkDirty() => builtCount = -1;

        void LateUpdate()
        {
            Transform space = DemoRig.TrackingSpace;
            if (space == null || shape == null) return;

            EnsureRoot(space);
            root.gameObject.SetActive(visible);
            if (!visible) return;

            int hash = Hash();
            if (builtCount != shape.Count || hash != builtHash) Rebuild();

            UpdatePreview();
            if (showWalls && shape.IsValid) UpdateWallAlpha();
        }

        void EnsureRoot(Transform space)
        {
            if (root != null && root.parent == space) return;
            if (root == null)
            {
                root = new GameObject(name + "_Visual").transform;
            }
            root.SetParent(space, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
        }

        int Hash()
        {
            unchecked
            {
                int h = 17;
                for (int i = 0; i < shape.Count; i++)
                    h = h * 31 + Mathf.RoundToInt(shape[i].x * 200f) * 7 + Mathf.RoundToInt(shape[i].z * 200f);
                return h;
            }
        }

        void Rebuild()
        {
            builtCount = shape.Count;
            builtHash = Hash();

            int n = shape.Count;
            int edges = shape.IsValid ? n : Mathf.Max(0, n - 1);

            EnsureCount(lines, edges, i => DemoUI.CreateSegment("Line_" + i, root, color, false).transform);
            EnsureCount(posts, n, i =>
            {
                var p = DemoUI.CreatePrimitive(PrimitiveType.Cylinder, "Post_" + i, root, Vector3.zero,
                    new Vector3(0.05f, 0.04f, 0.05f), color, false);
                DemoUI.DisableShadows(p);
                return p.transform;
            });

            int wallCount = showWalls && shape.IsValid ? n : 0;
            while (walls.Count < wallCount)
                walls.Add(DemoUI.CreateSegment("Wall_" + walls.Count, root, WithAlpha(idleWallAlpha), true).GetComponent<ColorTint>());
            for (int i = 0; i < walls.Count; i++) walls[i].gameObject.SetActive(i < wallCount);

            for (int i = 0; i < edges; i++)
            {
                Vector3 a = shape[i], b = shape[(i + 1) % n];
                DemoUI.PlaceSegment(lines[i], a, b, lineWidth, 0.004f, lineLift);
                if (i < wallCount) DemoUI.PlaceSegment(walls[i].transform, a, b, 0.004f, wallHeight, lineLift);
            }
            for (int i = 0; i < n; i++) posts[i].localPosition = shape[i] + Vector3.up * (0.02f + lineLift);
        }

        void UpdatePreview()
        {
            bool show = previewActive && shape.Count > 0;
            if (show && preview == null) preview = DemoUI.CreateSegment("Preview", root, Color.Lerp(color, Color.white, 0.5f), false).transform;
            if (preview == null) return;
            preview.gameObject.SetActive(show);
            if (show) DemoUI.PlaceSegment(preview, shape[shape.Count - 1], new Vector3(previewEnd.x, 0f, previewEnd.z), lineWidth * 0.6f, 0.004f, lineLift);
        }

        void UpdateWallAlpha()
        {
            for (int i = 0; i < walls.Count && i < shape.Count; i++)
            {
                if (!walls[i].gameObject.activeSelf) continue;
                float alpha = idleWallAlpha;
                if (revealAll)
                {
                    alpha = maxWallAlpha;
                }
                else
                {
                    float nearest = float.PositiveInfinity;
                    foreach (var p in DemoRig.AllPoints)
                        if (DemoRig.TryGetLocal(p, out Vector3 local))
                            nearest = Mathf.Min(nearest, shape.DistanceToEdge(local, i));
                    if (!float.IsInfinity(nearest))
                        alpha = Mathf.Lerp(maxWallAlpha, idleWallAlpha, Mathf.Clamp01(nearest / Mathf.Max(0.01f, fadeDistance)));
                }
                walls[i].SetAlpha(alpha);
            }
        }

        Color WithAlpha(float a)
        {
            Color c = color;
            c.a = a;
            return c;
        }

        static void EnsureCount<T>(List<T> list, int count, System.Func<int, T> create) where T : Component
        {
            while (list.Count < count) list.Add(create(list.Count));
            for (int i = 0; i < list.Count; i++) list[i].gameObject.SetActive(i < count);
        }

        void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
        }
    }
}
