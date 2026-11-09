using UnityEngine;

namespace VRBoundaryDemo
{
    /// <summary>
    /// Własna granica aplikacji rysowana prawym kontrolerem: promień z kontrolera trafia w podłogę,
    /// trigger dodaje narożnik wielokąta (obsługa przycisków w DemoActions).
    /// Punkty są zapisywane w przestrzeni śledzenia - tak jak granica systemowa - więc wielokąt
    /// "przykleja się" do fizycznego pokoju, a nie do wirtualnej sceny.
    /// Po wyśrodkowaniu widoku albo zmianie układu odniesienia (Floor Level / Stage) układ śledzenia
    /// przeskakuje względem pokoju; skrypt wykrywa skok pozy głowy i przelicza punkty, by zostały na miejscu.
    /// </summary>
    [RequireComponent(typeof(BoundaryVisual))]
    public class CustomBoundary : MonoBehaviour
    {
        public static CustomBoundary Instance { get; private set; }

        [Header("Rysowanie")]
        public HandSide drawingHand = HandSide.Right;
        public float maxRayLength = 8f;
        [Tooltip("Minimalny odstęp między punktami (m).")]
        public float minPointSpacing = 0.15f;
        public int maxPoints = 32;
        [Tooltip("Pokazuj promień i celownik (tylko gdy kontroler jest aktywny).")]
        public bool showPointer = true;

        [Header("Kolory promienia")]
        public Color rayColor = new Color(1f, 0.65f, 0.2f);
        public Color rayNoHitColor = new Color(0.7f, 0.7f, 0.7f);

        public BoundaryShape Shape { get; } = new BoundaryShape();
        public BoundaryVisual Visual { get; private set; }
        public bool PointerHit { get; private set; }
        /// <summary>Punkt trafienia w podłogę (przestrzeń śledzenia).</summary>
        public Vector3 PointerLocal { get; private set; }

        Transform ray, reticle;
        ColorTint rayTint;
        Vector3 lastHeadPos;
        float lastHeadYaw;
        bool hasLastHead;

        void Awake()
        {
            Instance = this;
            Visual = GetComponent<BoundaryVisual>();
            Visual.SetShape(Shape);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (ray != null) Destroy(ray.gameObject);
            if (reticle != null) Destroy(reticle.gameObject);
        }

        void Update()
        {
            CompensateOriginJump();
            UpdatePointer();
        }

        // ---------------- PROMIEŃ NA PODŁOGĘ ----------------

        void UpdatePointer()
        {
            Transform space = DemoRig.TrackingSpace;
            Transform ctrl = DemoRig.IsConnected(drawingHand) ? DemoRig.Controller(drawingHand) : null;
            PointerHit = false;

            if (space != null && ctrl != null)
            {
                // Płaszczyzna podłogi = płaszczyzna y = 0 przestrzeni śledzenia (Floor Level / Stage).
                var floor = new Plane(space.up, space.position);
                var r = new Ray(ctrl.position, ctrl.forward);
                if (floor.Raycast(r, out float dist) && dist <= maxRayLength)
                {
                    PointerHit = true;
                    Vector3 local = space.InverseTransformPoint(r.GetPoint(dist));
                    local.y = 0f;
                    PointerLocal = local;
                }
                Visual.previewActive = PointerHit && Shape.Count > 0 && Shape.Count < maxPoints;
                Visual.previewEnd = PointerLocal;

                if (showPointer) DrawPointer(ctrl, space, PointerHit ? dist : maxRayLength * 0.4f);
            }
            else
            {
                Visual.previewActive = false;
                if (ray != null) ray.gameObject.SetActive(false);
                if (reticle != null) reticle.gameObject.SetActive(false);
            }
        }

        void DrawPointer(Transform ctrl, Transform space, float length)
        {
            if (ray == null)
            {
                // Promień i celownik żyją w świecie (bez rodzica), pozycja liczona co klatkę.
                var go = DemoUI.CreateSegment("DrawRay", null, rayColor, false);
                ray = go.transform;
                rayTint = go.GetComponent<ColorTint>();
                var ret = DemoUI.CreatePrimitive(PrimitiveType.Cylinder, "Reticle", null, Vector3.zero,
                    new Vector3(0.08f, 0.002f, 0.08f), rayColor, false);
                DemoUI.DisableShadows(ret);
                reticle = ret.transform;
            }

            ray.gameObject.SetActive(true);
            Vector3 a = ctrl.position + ctrl.forward * 0.03f;
            Vector3 b = ctrl.position + ctrl.forward * length;
            Vector3 d = b - a;
            ray.SetPositionAndRotation((a + b) * 0.5f, Quaternion.LookRotation(d.normalized, Vector3.up));
            ray.localScale = new Vector3(0.004f, 0.004f, d.magnitude);
            rayTint.SetBaseColor(PointerHit ? rayColor : rayNoHitColor);

            reticle.gameObject.SetActive(PointerHit);
            if (PointerHit)
            {
                reticle.SetPositionAndRotation(space.TransformPoint(PointerLocal + Vector3.up * 0.008f), space.rotation);
            }
        }

        // ---------------- EDYCJA ----------------

        /// <summary>Dodaje narożnik w miejscu celownika. Zwraca opis wyniku do dziennika.</summary>
        public bool AddPointAtPointer(out string message)
        {
            if (!PointerHit)
            {
                message = "promień nie trafia w podłogę - celuj niżej";
                return false;
            }
            if (Shape.Count >= maxPoints)
            {
                message = $"osiągnięto limit {maxPoints} punktów";
                return false;
            }
            if (Shape.Count > 0 && Vector3.Distance(Shape[Shape.Count - 1], PointerLocal) < minPointSpacing)
            {
                message = "za blisko poprzedniego punktu";
                return false;
            }
            if (Shape.Count >= 3 && Vector3.Distance(Shape[0], PointerLocal) < minPointSpacing)
            {
                message = "kliknięto pierwszy punkt - wielokąt i tak jest zamknięty";
                return false;
            }
            Shape.Add(PointerLocal);
            Visual.MarkDirty();
            message = Shape.Count < 3
                ? $"punkt {Shape.Count} (potrzeba min. 3)"
                : $"punkt {Shape.Count}, pole {Shape.Area():0.0} m²";
            return true;
        }

        public bool Undo()
        {
            if (Shape.Count == 0) return false;
            Shape.RemoveLast();
            Visual.MarkDirty();
            return true;
        }

        public void Clear()
        {
            Shape.Clear();
            Visual.MarkDirty();
        }

        // ---------------- KOMPENSACJA SKOKU UKŁADU ŚLEDZENIA ----------------

        /// <summary>
        /// Głowa fizycznie nie może przeskoczyć o 25 cm lub 20° w jednej klatce. Jeśli tak się dzieje tuż po
        /// wyśrodkowaniu / zmianie origin, to przesunął się układ śledzenia - przeliczamy punkty tym samym
        /// przekształceniem (obrót wokół osi Y + przesunięcie), żeby wielokąt został w tym samym miejscu pokoju.
        /// </summary>
        void CompensateOriginJump()
        {
            Transform head = DemoRig.Head;
            Transform space = DemoRig.TrackingSpace;
            if (head == null || space == null)
            {
                hasLastHead = false;
                return;
            }

            Vector3 pos = space.InverseTransformPoint(head.position);
            Vector3 fwd = space.InverseTransformDirection(head.forward);
            float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;

            if (hasLastHead && Time.unscaledTime < SystemBoundary.OriginChangeWindowUntil)
            {
                Vector3 flatDelta = new Vector3(pos.x - lastHeadPos.x, 0f, pos.z - lastHeadPos.z);
                float dYaw = Mathf.DeltaAngle(lastHeadYaw, yaw);
                if (flatDelta.magnitude > 0.25f || Mathf.Abs(dYaw) > 20f)
                {
                    if (Shape.Count > 0)
                    {
                        Quaternion rot = Quaternion.Euler(0f, dYaw, 0f);
                        Vector3 oldHead = new Vector3(lastHeadPos.x, 0f, lastHeadPos.z);
                        Vector3 newHead = new Vector3(pos.x, 0f, pos.z);
                        for (int i = 0; i < Shape.Count; i++)
                            Shape[i] = newHead + rot * (Shape[i] - oldHead);
                        Visual.MarkDirty();
                        DemoLog.Add($"Własna granica przeliczona po skoku układu ({flatDelta.magnitude:0.00} m, {dYaw:0}°)");
                    }
                }
            }

            lastHeadPos = pos;
            lastHeadYaw = yaw;
            hasLastHead = true;
        }
    }
}
