using UnityEngine;
using UnityEngine.UI;

namespace VRBoundaryDemo
{
    /// <summary>Która granica jest używana do testów bliskości.</summary>
    public enum BoundarySource { SystemPlayArea, Custom }

    /// <summary>
    /// Serce demo: co klatkę sprawdza głowę i oba kontrolery względem aktywnej granicy
    /// (systemowy Play Area albo własna granica narysowana kontrolerem):
    ///  - strefa: bezpiecznie / blisko krawędzi / poza granicą,
    ///  - wibracje kontrolera rosnące przy zbliżaniu się do krawędzi,
    ///  - czerwone znaczniki w najbliższym punkcie krawędzi,
    ///  - ostrzeżenie przed oczami (HUD) dla głowy,
    ///  - wpisy w dzienniku przy każdej zmianie strefy.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class BoundaryMonitor : MonoBehaviour
    {
        public static BoundaryMonitor Instance { get; private set; }

        public class PointState
        {
            public TrackedPoint point;
            public bool available;
            public BoundaryZone zone = BoundaryZone.Unknown;
            public float distance = float.PositiveInfinity;
            public Vector3 closestLocal;
            public Transform marker;
        }

        [Header("Granice")]
        public SystemBoundary systemBoundary;
        public CustomBoundary customBoundary;
        public BoundarySource source = BoundarySource.SystemPlayArea;

        [Header("Progi (m)")]
        [Tooltip("Głowa bliżej krawędzi niż ta odległość = ostrzeżenie.")]
        public float headWarnDistance = 0.4f;
        [Tooltip("Kontroler bliżej krawędzi niż ta odległość = ostrzeżenie i wibracje.")]
        public float controllerWarnDistance = 0.3f;

        [Header("Reakcje")]
        public bool hapticsEnabled = true;
        public bool showClosestPointMarkers = true;
        public bool showHeadWarning = true;

        public readonly PointState[] States =
        {
            new PointState { point = TrackedPoint.Head },
            new PointState { point = TrackedPoint.LeftController },
            new PointState { point = TrackedPoint.RightController },
        };

        public BoundaryShape ActiveShape =>
            source == BoundarySource.Custom
                ? (customBoundary != null ? customBoundary.Shape : null)
                : (systemBoundary != null ? systemBoundary.Shape : null);

        public string SourceName => source == BoundarySource.Custom ? "własna granica" : "systemowy Play Area";

        RectTransform hud;
        Text hudText;
        Image hudBackground;

        void Awake()
        {
            Instance = this;
            if (systemBoundary == null) systemBoundary = FindAnyObjectByType<SystemBoundary>();
            if (customBoundary == null) customBoundary = FindAnyObjectByType<CustomBoundary>();
            CreateHud();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (hud != null) Destroy(hud.gameObject);
            foreach (var s in States)
                if (s.marker != null) Destroy(s.marker.gameObject);
        }

        /// <summary>Przełącza źródło granicy do testów (przycisk A).</summary>
        public void ToggleSource()
        {
            source = source == BoundarySource.SystemPlayArea ? BoundarySource.Custom : BoundarySource.SystemPlayArea;
            foreach (var s in States) s.zone = BoundaryZone.Unknown; // bez fałszywych wpisów o zmianie strefy
            var shape = ActiveShape;
            string extra = shape != null && shape.IsValid ? "" : " - brak poprawnego wielokąta (min. 3 punkty)";
            DemoLog.Add("Aktywna granica do testów: " + SourceName + extra);
        }

        void Update()
        {
            BoundaryShape shape = ActiveShape;
            bool valid = shape != null && shape.IsValid;
            Transform space = DemoRig.TrackingSpace;

            foreach (var s in States)
            {
                s.available = DemoRig.TryGetLocal(s.point, out Vector3 local);
                BoundaryZone zone = BoundaryZone.Unknown;
                if (s.available && valid)
                {
                    float warn = s.point == TrackedPoint.Head ? headWarnDistance : controllerWarnDistance;
                    zone = shape.Classify(local, warn, out float d, out Vector3 closest);
                    s.distance = d;
                    s.closestLocal = closest;
                }
                else
                {
                    s.distance = float.PositiveInfinity;
                }

                if (zone != s.zone)
                {
                    if (s.zone != BoundaryZone.Unknown && zone != BoundaryZone.Unknown) LogZoneChange(s, zone);
                    if (zone == BoundaryZone.Outside && s.point != TrackedPoint.Head)
                        Haptics.PulseStatic(s.point == TrackedPoint.LeftController ? HandSide.Left : HandSide.Right, 1f, 0.15f);
                    s.zone = zone;
                }

                UpdateMarker(s, space);
            }

            UpdateHaptics();
            UpdateHud();
        }

        void LogZoneChange(PointState s, BoundaryZone zone)
        {
            string name = DemoRig.Name(s.point);
            switch (zone)
            {
                case BoundaryZone.Outside: DemoLog.Add($"<color=#FF7070>{name}: POZA granicą</color> ({SourceName})"); break;
                case BoundaryZone.Warning:
                    DemoLog.Add(s.zone == BoundaryZone.Outside
                        ? $"{name}: powrót za granicę (blisko krawędzi)"
                        : $"<color=#FFD27F>{name}: blisko krawędzi</color> ({s.distance:0.00} m)");
                    break;
                case BoundaryZone.Safe: DemoLog.Add($"<color=#9FE8A0>{name}: bezpiecznie</color>"); break;
            }
        }

        // Wibracja: 0 daleko od krawędzi, rośnie liniowo w strefie ostrzeżenia, pełna poza granicą.
        void UpdateHaptics()
        {
            var h = Haptics.Instance;
            if (h == null) return;
            for (int i = 1; i <= 2; i++)
            {
                var s = States[i];
                var side = i == 1 ? HandSide.Left : HandSide.Right;
                float level = 0f;
                if (hapticsEnabled && s.available)
                {
                    if (s.zone == BoundaryZone.Outside) level = 0.8f;
                    else if (s.zone == BoundaryZone.Warning)
                        level = Mathf.Lerp(0.6f, 0.08f, s.distance / Mathf.Max(0.01f, controllerWarnDistance));
                }
                h.SetLevel(side, level);
            }
        }

        void UpdateMarker(PointState s, Transform space)
        {
            bool show = showClosestPointMarkers && space != null && s.available &&
                        (s.zone == BoundaryZone.Warning || s.zone == BoundaryZone.Outside);
            if (show && s.marker == null)
            {
                var m = DemoUI.CreatePrimitive(PrimitiveType.Sphere, "Closest_" + s.point, null, Vector3.zero,
                    Vector3.one * 0.05f, new Color(1f, 0.25f, 0.2f), false);
                DemoUI.DisableShadows(m);
                s.marker = m.transform;
            }
            if (s.marker == null) return;
            s.marker.gameObject.SetActive(show);
            if (!show) return;

            // Znacznik na krawędzi, na wysokości śledzonego punktu (ściana) - widać, gdzie jest granica.
            DemoRig.TryGetLocal(s.point, out Vector3 local);
            Vector3 p = s.closestLocal;
            p.y = local.y;
            s.marker.position = space.TransformPoint(p);
        }

        // ---------------- HUD przed oczami ----------------

        void CreateHud()
        {
            hud = DemoUI.CreatePanel("BoundaryWarningHUD", null, Vector3.zero, Quaternion.identity, new Vector2(560f, 120f),
                new Color(0.5f, 0.3f, 0f, 0.8f));
            hudBackground = hud.GetComponentInChildren<Image>();
            hudText = DemoUI.CreateFillText(hud, "Text", 10f, 30, TextAnchor.MiddleCenter, Color.white, "");
            hud.gameObject.SetActive(false);
        }

        void UpdateHud()
        {
            var head = DemoRig.Head;
            var s = States[0];
            bool show = showHeadWarning && head != null &&
                        (s.zone == BoundaryZone.Warning || s.zone == BoundaryZone.Outside);
            bool wasHidden = !hud.gameObject.activeSelf;
            hud.gameObject.SetActive(show);
            if (!show) return;

            bool outside = s.zone == BoundaryZone.Outside;
            hudBackground.color = outside ? new Color(0.65f, 0.05f, 0.05f, 0.85f) : new Color(0.55f, 0.35f, 0f, 0.8f);
            hudText.text = outside
                ? $"<b>POZA GRANICĄ</b>\nwróć o {s.distance:0.00} m"
                : $"<b>Blisko granicy</b>\n{s.distance:0.00} m do krawędzi";

            // Panel 0,8 m przed oczami, trochę poniżej linii wzroku, zwrócony do głowy.
            Vector3 fwd = head.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 target = head.position + fwd * 0.8f + Vector3.down * 0.12f;
            hud.position = wasHidden ? target : Vector3.Lerp(hud.position, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
            hud.rotation = DemoUI.FacePoint(hud.position, head.position);
        }
    }
}
