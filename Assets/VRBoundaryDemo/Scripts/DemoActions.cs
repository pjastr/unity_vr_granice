using UnityEngine;

namespace VRBoundaryDemo
{
    /// <summary>
    /// Centralne mapowanie przycisków kontrolerów (OVRInput) na akcje demo granic.
    /// Button.One/Two z maską RTouch = A/B, z maską LTouch = X/Y.
    /// Gałki (ruch, obrót, powrót na start) obsługuje PlayerLocomotion.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class DemoActions : MonoBehaviour
    {
        [Header("Powiązania")]
        public SystemBoundary systemBoundary;
        public BoundaryVisual systemVisual;
        public CustomBoundary customBoundary;
        public BoundaryMonitor monitor;
        public PassthroughBoundaryControl passthrough;
        [Tooltip("Obiekt nadrzędny paneli (pokaż/ukryj).")]
        public GameObject panelsRoot;

        [Header("Ustawienia")]
        [Tooltip("Ile sekund trzymać prawy grip, aby wyczyścić własną granicę.")]
        public float holdToClear = 1f;

        float rightGripDownTime = -1f;
        bool clearedThisHold;
        bool revealing;

        void Start()
        {
            if (systemBoundary == null) systemBoundary = FindAnyObjectByType<SystemBoundary>();
            if (customBoundary == null) customBoundary = FindAnyObjectByType<CustomBoundary>();
            if (monitor == null) monitor = FindAnyObjectByType<BoundaryMonitor>();
            if (passthrough == null) passthrough = FindAnyObjectByType<PassthroughBoundaryControl>();

            if (DemoRig.Rig == null) DemoLog.Add("<color=#FF7070>Brak OVRCameraRig - dodaj blok Camera Rig</color>");
            DemoLog.Add("Start demo granic. Sterowanie kontrolerami - opis na panelu pomocy.");
        }

        void Update()
        {
            const OVRInput.Controller R = OVRInput.Controller.RTouch;
            const OVRInput.Controller L = OVRInput.Controller.LTouch;

            // ---------------- PRAWY KONTROLER: własna granica ----------------
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, R)) AddPoint();

            if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, R))
            {
                rightGripDownTime = Time.unscaledTime;
                clearedThisHold = false;
            }
            if (rightGripDownTime > 0f && !clearedThisHold && Time.unscaledTime - rightGripDownTime >= holdToClear)
            {
                clearedThisHold = true;
                if (customBoundary != null) customBoundary.Clear();
                Haptics.PulseStatic(HandSide.Right, 0.9f, 0.25f);
                DemoLog.Add("Prawy grip (przytrzymany): wyczyszczono własną granicę - możesz rysować nową");
            }
            if (OVRInput.GetUp(OVRInput.Button.PrimaryHandTrigger, R))
            {
                if (!clearedThisHold && customBoundary != null && customBoundary.IsLocked)
                {
                    customBoundary.Unlock();
                    Haptics.PulseStatic(HandSide.Right, 0.5f, 0.08f);
                    DemoLog.Add($"Prawy grip: odblokowano edycję własnej granicy (blokada po {customBoundary.autoLockSeconds:0} s bez zmian)");
                }
                else if (!clearedThisHold && customBoundary != null)
                {
                    bool ok = customBoundary.Undo();
                    Haptics.PulseStatic(HandSide.Right, ok ? 0.4f : 0.15f, 0.05f);
                    DemoLog.Add(ok ? $"Prawy grip: cofnięto punkt (zostało {customBoundary.Shape.Count})" : "Prawy grip: brak punktów do cofnięcia");
                }
                rightGripDownTime = -1f;
            }

            if (OVRInput.GetDown(OVRInput.Button.One, R)) // A
            {
                if (monitor != null) monitor.ToggleSource();
                Haptics.PulseStatic(HandSide.Right);
            }
            if (OVRInput.GetDown(OVRInput.Button.Two, R)) // B
            {
                if (systemVisual != null)
                {
                    systemVisual.visible = !systemVisual.visible;
                    DemoLog.Add("Przycisk B: wizualizacja Play Area " + (systemVisual.visible ? "widoczna" : "ukryta"));
                }
                Haptics.PulseStatic(HandSide.Right);
            }

            // ---------------- LEWY KONTROLER: system, passthrough, widok ----------------
            if (OVRInput.GetDown(OVRInput.Button.One, L)) // X
            {
                if (passthrough != null) passthrough.TogglePassthrough("Przycisk X");
                Haptics.PulseStatic(HandSide.Left);
            }
            if (OVRInput.GetDown(OVRInput.Button.Two, L)) // Y
            {
                if (passthrough != null) passthrough.ToggleSuppression("Przycisk Y");
                Haptics.PulseStatic(HandSide.Left);
            }
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, L))
            {
                if (systemBoundary != null) systemBoundary.Refresh("lewy trigger");
                Haptics.PulseStatic(HandSide.Left);
            }

            // Lewy grip trzymany: wszystkie ściany granic w pełni widoczne.
            bool reveal = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, L);
            if (reveal != revealing)
            {
                revealing = reveal;
                if (systemVisual != null) systemVisual.revealAll = reveal;
                if (customBoundary != null) customBoundary.Visual.revealAll = reveal;
                if (reveal) DemoLog.Add("Lewy grip: podgląd wszystkich ścian granic");
            }

            if (OVRInput.GetDown(OVRInput.Button.Start, L)) SwitchTrackingOrigin();

            if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, L) && panelsRoot != null)
            {
                panelsRoot.SetActive(!panelsRoot.activeSelf);
                DemoLog.Add("Klik lewej gałki: panele " + (panelsRoot.activeSelf ? "widoczne" : "ukryte"));
            }
        }

        void AddPoint()
        {
            if (customBoundary == null) return;
            bool ok = customBoundary.AddPointAtPointer(out string msg);
            Haptics.PulseStatic(HandSide.Right, ok ? 0.5f : 0.15f, ok ? 0.05f : 0.12f);
            DemoLog.Add("Prawy trigger: " + msg);
        }

        /// <summary>
        /// Menu (☰): przełącza układ odniesienia Floor Level ↔ Stage.
        /// Floor Level - początek układu tam, gdzie wyśrodkowano widok (podłoga na y = 0).
        /// Stage - początek w środku obszaru gry, osie równoległe do jego krawędzi; recenter go nie zmienia.
        /// </summary>
        void SwitchTrackingOrigin()
        {
            var mgr = OVRManager.instance;
            if (mgr == null) return;
            var next = mgr.trackingOriginType == OVRManager.TrackingOrigin.Stage
                ? OVRManager.TrackingOrigin.FloorLevel
                : OVRManager.TrackingOrigin.Stage;

            if (systemBoundary != null) systemBoundary.RefreshSoon(null);
            else SystemBoundary.ExpectOriginChange();
            mgr.trackingOriginType = next;
            Haptics.PulseStatic(HandSide.Left);
            DemoLog.Add("Menu: układ odniesienia -> " + next);
        }
    }
}
