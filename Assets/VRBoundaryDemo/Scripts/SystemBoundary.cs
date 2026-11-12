using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace VRBoundaryDemo
{
    /// <summary>
    /// Odczyt granicy systemowej (Guardian) przez OVRManager.boundary (klasa OVRBoundary):
    ///  - GetConfigured()            - czy system ma poprawne dane granicy,
    ///  - GetGeometry(PlayArea)      - narożniki obszaru gry (prostokąt) w przestrzeni śledzenia, na podłodze,
    ///  - GetDimensions(PlayArea)    - szerokość i głębokość (wysokość zawsze 0).
    /// Używa tylko API niewycofanego w backendzie OpenXR (OuterBoundary, TestNode/TestPoint,
    /// GetVisible/SetVisible są przestarzałe).
    /// Dane są odczytywane ponownie: co kilka sekund, po założeniu gogli, po odzyskaniu śledzenia,
    /// po wznowieniu aplikacji, po wyśrodkowaniu widoku i po zmianie układu odniesienia.
    ///
    /// OGRANICZENIA OpenXR na Quest:
    ///  - aplikacja dostaje tylko PROSTOKĄT obszaru gry (granice przestrzeni STAGE: szerokość × głębokość),
    ///    a nie kształt narysowany w ustawieniach gogli; granica stacjonarna nie jest udostępniana wcale,
    ///  - prostokąt jest wyśrodkowany w początku przestrzeni STAGE - pokrywa się z pokojem tylko wtedy,
    ///    gdy rig używa układu Stage (dlatego forceStageOrigin = true),
    ///  - dla porównania odczytywane jest też drugie źródło: XRInputSubsystem.TryGetBoundaryPoints (Unity XR),
    ///    wraz ze zdarzeniem boundaryChanged.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SystemBoundary : MonoBehaviour
    {
        public static SystemBoundary Instance { get; private set; }

        [Tooltip("Co ile sekund ponownie odczytywać granicę (0 = tylko przy zdarzeniach).")]
        public float autoRefreshInterval = 2f;

        [Tooltip("Przy starcie ustaw w OVRManager układ Stage - tylko w nim prostokąt obszaru gry leży w prawdziwym miejscu pokoju.")]
        public bool forceStageOrigin = true;

        public BoundaryShape Shape { get; } = new BoundaryShape();
        public bool Configured { get; private set; }
        public Vector3 Dimensions { get; private set; }
        /// <summary>Zwiększa się przy każdej zmianie kształtu granicy.</summary>
        public int Version { get; private set; }
        public string Status { get; private set; } = "jeszcze nie odczytano";
        public float LastRefreshTime { get; private set; } = -1f;

        /// <summary>Wynik drugiego źródła (Unity XRInputSubsystem) - do porównania na panelu.</summary>
        public bool XrHasPoints { get; private set; }
        public int XrPointCount { get; private set; }
        public Vector2 XrSize { get; private set; }
        public string XrStatus { get; private set; } = "-";
        /// <summary>Źródło punktów aktualnie rysowanych na niebiesko.</summary>
        public string ShapeSource { get; private set; } = "-";

        /// <summary>Czy prostokąt leży w prawdziwym miejscu pokoju (tylko w układzie Stage).</summary>
        public static bool IsStageOrigin =>
            OVRManager.instance != null && OVRManager.instance.trackingOriginType == OVRManager.TrackingOrigin.Stage;

        readonly List<XRInputSubsystem> xrSubsystems = new List<XRInputSubsystem>();
        readonly List<Vector3> xrPoints = new List<Vector3>();
        XRInputSubsystem subscribedXr;

        /// <summary>Do kiedy spodziewamy się skoku układu śledzenia (wyśrodkowanie, zmiana origin).</summary>
        public static float OriginChangeWindowUntil { get; private set; }

        public event Action Changed;

        float nextAuto;
        float delayedRefreshAt = -1f;
        OVRDisplay subscribedDisplay;

        void Awake()
        {
            Instance = this;
            // Wizualizacja na tym samym obiekcie rysuje ten sam wielokąt (zmiany wykrywa sama).
            var visual = GetComponent<BoundaryVisual>();
            if (visual != null) visual.SetShape(Shape);
        }

        void OnEnable()
        {
            OVRManager.HMDMounted += OnHmdMounted;
            OVRManager.TrackingAcquired += OnTrackingAcquired;
        }

        void OnDisable()
        {
            OVRManager.HMDMounted -= OnHmdMounted;
            OVRManager.TrackingAcquired -= OnTrackingAcquired;
            if (subscribedDisplay != null) subscribedDisplay.RecenteredPose -= OnRecentered;
            subscribedDisplay = null;
            if (subscribedXr != null) subscribedXr.boundaryChanged -= OnXrBoundaryChanged;
            subscribedXr = null;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            var mgr = OVRManager.instance;
            if (forceStageOrigin && mgr != null && mgr.trackingOriginType != OVRManager.TrackingOrigin.Stage)
            {
                ExpectOriginChange();
                mgr.trackingOriginType = OVRManager.TrackingOrigin.Stage;
                DemoLog.Add("Układ odniesienia ustawiony na Stage (środek = środek obszaru gry)");
                RefreshSoon(null);
            }
            Refresh("start aplikacji");
        }

        void Update()
        {
            // OVRManager.display powstaje razem z OVRManager - podpinamy się, gdy będzie dostępny.
            if (subscribedDisplay == null && OVRManager.display != null)
            {
                subscribedDisplay = OVRManager.display;
                subscribedDisplay.RecenteredPose += OnRecentered;
            }
            if (subscribedXr == null)
            {
                subscribedXr = GetXrInput();
                if (subscribedXr != null) subscribedXr.boundaryChanged += OnXrBoundaryChanged;
            }

            if (delayedRefreshAt > 0f && Time.unscaledTime >= delayedRefreshAt)
            {
                delayedRefreshAt = -1f;
                Refresh("po zmianie układu");
            }
            else if (autoRefreshInterval > 0f && Time.unscaledTime >= nextAuto)
            {
                Refresh(null); // cicho - wpis w dzienniku tylko, gdy granica się zmieniła
            }
        }

        void OnApplicationPause(bool paused)
        {
            // Zalecenie Meta: po wznowieniu sprawdź granice ponownie (użytkownik mógł zmienić obszar gry).
            if (!paused) RefreshSoon("wznowienie aplikacji");
        }

        void OnHmdMounted() => RefreshSoon("założono gogle");
        void OnXrBoundaryChanged(XRInputSubsystem _) => RefreshSoon("zdarzenie XRInputSubsystem.boundaryChanged");
        void OnTrackingAcquired() => RefreshSoon("odzyskano śledzenie");

        void OnRecentered()
        {
            DemoLog.Add("System: wyśrodkowano widok (recenter) - układ śledzenia przesunięty");
            RefreshSoon(null);
        }

        /// <summary>Wywołaj przed zmianą układu odniesienia - granica zostanie odczytana ponownie za chwilę.</summary>
        public void RefreshSoon(string reason)
        {
            ExpectOriginChange();
            if (!string.IsNullOrEmpty(reason)) DemoLog.Add("Granica: odświeżenie - " + reason);
            delayedRefreshAt = Time.unscaledTime + 0.3f;
        }

        public static void ExpectOriginChange() => OriginChangeWindowUntil = Time.unscaledTime + 2f;

        /// <summary>Odczytuje granicę z systemu. reason = null: wpis do dziennika tylko przy zmianie.</summary>
        public void Refresh(string reason)
        {
            nextAuto = Time.unscaledTime + Mathf.Max(0.5f, autoRefreshInterval);
            LastRefreshTime = Time.unscaledTime;
            ReadXrSubsystem();

            OVRBoundary boundary = OVRManager.boundary;
            if (boundary == null)
            {
                SetStatus(false, null, Vector3.zero, "brak OVRManager (dodaj blok Camera Rig)", reason);
                return;
            }

            bool configured = false;
            Vector3[] geometry = null;
            Vector3 dims = Vector3.zero;
            try
            {
                configured = boundary.GetConfigured();
                if (configured)
                {
                    geometry = boundary.GetGeometry(OVRBoundary.BoundaryType.PlayArea);
                    dims = boundary.GetDimensions(OVRBoundary.BoundaryType.PlayArea);
                }
            }
            catch (Exception e)
            {
                SetStatus(false, null, Vector3.zero, "błąd odczytu: " + e.Message, reason);
                return;
            }

            // Gdy OVRBoundary nic nie dało, a Unity XR ma punkty - rysujemy te z Unity XR.
            ShapeSource = "OVRBoundary";
            if ((geometry == null || geometry.Length < 3) && XrHasPoints)
            {
                geometry = xrPoints.ToArray();
                Vector2 sz = XrSize;
                dims = new Vector3(sz.x, 0f, sz.y);
                configured = true;
                ShapeSource = "XRInputSubsystem";
            }

            string status;
            if (!configured) status = Application.isEditor ? "brak danych (edytor bez gogli)" : "brak obszaru gry (granica stacjonarna lub wyłączona?)";
            else if (geometry == null || geometry.Length < 3) status = "brak prostokąta obszaru gry (granica stacjonarna?)";
            else status = "OK";
            SetStatus(configured, geometry, dims, status, reason);
        }

        void SetStatus(bool configured, Vector3[] geometry, Vector3 dims, string status, string reason)
        {
            bool shapeChanged = HasChanged(geometry);
            bool stateChanged = configured != Configured || status != Status;

            Configured = configured;
            Dimensions = dims;
            Status = status;

            if (shapeChanged)
            {
                Shape.Set(geometry);
                Version++;
            }

            if (shapeChanged || stateChanged || reason != null)
            {
                string desc = Shape.IsValid
                    ? $"Play Area {dims.x:0.00} × {dims.z:0.00} m, {Shape.Count} pkt"
                    : status;
                DemoLog.Add("Granica systemowa: " + desc + (reason != null ? $" ({reason})" : ""));
            }
            if (shapeChanged) Changed?.Invoke();
        }

        void ReadXrSubsystem()
        {
            XrHasPoints = false;
            XrPointCount = 0;
            XrSize = Vector2.zero;
            var input = GetXrInput();
            if (input == null) { XrStatus = "brak podsystemu XR"; return; }

            xrPoints.Clear();
            bool ok = false;
            try { ok = input.TryGetBoundaryPoints(xrPoints); }
            catch (Exception e) { XrStatus = "błąd: " + e.Message; return; }

            XrPointCount = xrPoints.Count;
            XrHasPoints = ok && xrPoints.Count >= 3;
            if (XrHasPoints)
            {
                var tmp = new BoundaryShape();
                tmp.Set(xrPoints);
                XrSize = tmp.BoundsSize();
            }
            XrStatus = !ok ? "niedostępne" : XrHasPoints ? "OK" : "0 punktów";
        }

        XRInputSubsystem GetXrInput()
        {
            xrSubsystems.Clear();
            SubsystemManager.GetSubsystems(xrSubsystems);
            foreach (var s in xrSubsystems)
                if (s != null && s.running) return s;
            return null;
        }

        bool HasChanged(Vector3[] geometry)
        {
            int n = geometry == null ? 0 : geometry.Length;
            if (n != Shape.Count) return true;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = geometry[i], b = Shape[i];
                if (Mathf.Abs(a.x - b.x) > 0.005f || Mathf.Abs(a.z - b.z) > 0.005f) return true;
            }
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => OriginChangeWindowUntil = 0f;
    }
}
