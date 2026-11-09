using UnityEngine;

namespace VRBoundaryDemo
{
    /// <summary>
    /// Passthrough i widoczność granicy systemowej (tryb "contextual boundaryless", Boundary API):
    ///  - OVRPassthroughLayer.enabled          - obraz z kamer gogli (blok Passthrough),
    ///  - OVRManager.shouldBoundaryVisibilityBeSuppressed - prośba o ukrycie granicy systemowej,
    ///  - OVRManager.isBoundaryVisibilitySuppressed       - faktyczny stan (decyduje system),
    ///  - OVRManager.BoundaryVisibilityChanged            - zdarzenie zmiany widoczności.
    /// System ukryje granicę TYLKO wtedy, gdy aktywny jest passthrough - dlatego przy wyłączaniu
    /// passthrough prośba o ukrycie jest automatycznie cofana (zalecenie Meta: trzymać oba stany w zgodzie).
    /// </summary>
    public class PassthroughBoundaryControl : MonoBehaviour
    {
        public static PassthroughBoundaryControl Instance { get; private set; }

        [Tooltip("Obiekty wirtualnego otoczenia (podłoga, siatka) ukrywane przy włączonym passthrough.")]
        public GameObject environmentRoot;
        public bool startWithPassthrough;
        public Color vrBackground = new Color(0.08f, 0.09f, 0.12f, 1f);

        public OVRPassthroughLayer Layer { get; private set; }
        public bool PassthroughOn => Layer != null && Layer.enabled;
        public bool SuppressionRequested => OVRManager.instance != null && OVRManager.instance.shouldBoundaryVisibilityBeSuppressed;
        public bool SuppressionActive => OVRManager.instance != null && OVRManager.instance.isBoundaryVisibilitySuppressed;
        public string LastSystemVisibility { get; private set; } = "-";

        bool lastActive;
        float checkResultAt = -1f;

        void Awake() => Instance = this;

        void OnEnable() => OVRManager.BoundaryVisibilityChanged += OnBoundaryVisibilityChanged;

        void OnDisable() => OVRManager.BoundaryVisibilityChanged -= OnBoundaryVisibilityChanged;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            Layer = FindAnyObjectByType<OVRPassthroughLayer>(FindObjectsInactive.Include);
            if (Layer == null)
            {
                DemoLog.Add("Brak bloku Passthrough - przyciski X/Y (passthrough, ukrywanie granicy) nieaktywne");
                ApplyVisuals(false);
                return;
            }
            if (OVRManager.instance != null && !OVRManager.instance.isInsightPassthroughEnabled)
                OVRManager.instance.isInsightPassthroughEnabled = true; // warstwy passthrough działają tylko przy tej fladze
            SetPassthrough(startWithPassthrough, null);
            if (OVRManager.instance != null) OVRManager.instance.shouldBoundaryVisibilityBeSuppressed = false;
        }

        void Update()
        {
            // Polling stanu jako uzupełnienie zdarzenia (np. gdy system zmieni stan sam).
            bool active = SuppressionActive;
            if (active != lastActive)
            {
                lastActive = active;
                DemoLog.Add(active ? "System: granica UKRYTA (isBoundaryVisibilitySuppressed = true)"
                                   : "System: granica znów aktywna (isBoundaryVisibilitySuppressed = false)");
            }

            if (checkResultAt > 0f && Time.unscaledTime >= checkResultAt)
            {
                checkResultAt = -1f;
                if (SuppressionRequested && !SuppressionActive)
                    DemoLog.Add("<color=#FFD27F>System nie ukrył granicy</color> - wymaga aktywnego passthrough, " +
                                "Passthrough Support ≠ None i aktualnego Horizon OS");
            }
        }

        void OnBoundaryVisibilityChanged(OVRPlugin.BoundaryVisibility visibility)
        {
            LastSystemVisibility = visibility.ToString();
            DemoLog.Add("Zdarzenie BoundaryVisibilityChanged: " + visibility);
        }

        /// <summary>Włącza/wyłącza passthrough (przycisk X).</summary>
        public void TogglePassthrough(string reason) => SetPassthrough(!PassthroughOn, reason);

        public void SetPassthrough(bool on, string reason)
        {
            if (Layer == null)
            {
                if (reason != null) DemoLog.Add(reason + ": brak bloku Passthrough w scenie");
                return;
            }
            Layer.enabled = on;
            ApplyVisuals(on);

            // Bez passthrough system i tak nie ukryje granicy - cofamy prośbę, żeby stany były zgodne.
            if (!on && SuppressionRequested)
            {
                OVRManager.instance.shouldBoundaryVisibilityBeSuppressed = false;
                DemoLog.Add("Passthrough wyłączony -> cofnięto prośbę o ukrycie granicy");
            }
            if (reason != null) DemoLog.Add($"{reason}: passthrough {(on ? "WŁĄCZONY" : "wyłączony")}");
        }

        /// <summary>Prośba o ukrycie / przywrócenie granicy systemowej (przycisk Y).</summary>
        public void ToggleSuppression(string reason)
        {
            var mgr = OVRManager.instance;
            if (mgr == null)
            {
                DemoLog.Add(reason + ": brak OVRManager (blok Camera Rig)");
                return;
            }

            bool want = !mgr.shouldBoundaryVisibilityBeSuppressed;
            if (want && !PassthroughOn)
            {
                DemoLog.Add($"{reason}: ukrycie granicy wymaga passthrough - najpierw naciśnij X");
                return;
            }
            mgr.shouldBoundaryVisibilityBeSuppressed = want;
            DemoLog.Add($"{reason}: shouldBoundaryVisibilityBeSuppressed = {want}");
            checkResultAt = Time.unscaledTime + 1.5f;
        }

        // Passthrough to warstwa POD obrazem aplikacji: tło kamery musi być przezroczyste,
        // a nieprzezroczyste otoczenie (podłoga) ukryte - inaczej zasłoni obraz z kamer.
        void ApplyVisuals(bool passthrough)
        {
            if (environmentRoot != null) environmentRoot.SetActive(!passthrough);
            var head = DemoRig.Head;
            var cam = head != null ? head.GetComponent<Camera>() : null;
            if (cam == null) return;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = passthrough ? new Color(0f, 0f, 0f, 0f) : vrBackground;
        }
    }
}
