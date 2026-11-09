using UnityEngine;

namespace VRBoundaryDemo
{
    public enum HandSide { Left, Right }

    /// <summary>Śledzone punkty testowane względem granicy.</summary>
    public enum TrackedPoint { Head, LeftController, RightController }

    /// <summary>
    /// Dostęp do OVRCameraRig z bloku Camera Rig (wyszukiwany automatycznie) i pozycji
    /// głowy / kontrolerów w przestrzeni śledzenia - tej samej, w której system podaje granicę.
    /// </summary>
    public static class DemoRig
    {
        static OVRCameraRig rig;
        static float nextFind;

        public static readonly TrackedPoint[] AllPoints =
            { TrackedPoint.Head, TrackedPoint.LeftController, TrackedPoint.RightController };

        public static OVRCameraRig Rig
        {
            get
            {
                if (rig == null && Time.unscaledTime >= nextFind)
                {
                    nextFind = Time.unscaledTime + 1f;
                    rig = Object.FindAnyObjectByType<OVRCameraRig>();
                }
                return rig;
            }
        }

        /// <summary>Przestrzeń śledzenia: rodzic głowy i kontrolerów. Granica systemowa jest w jej układzie.</summary>
        public static Transform TrackingSpace
        {
            get
            {
                var r = Rig;
                if (r == null) return null;
                return r.trackingSpace != null ? r.trackingSpace : r.transform;
            }
        }

        public static Transform Head => Rig != null ? rig.centerEyeAnchor : null;

        public static Transform Controller(HandSide side)
        {
            var r = Rig;
            if (r == null) return null;
            Transform t = side == HandSide.Left ? r.leftControllerAnchor : r.rightControllerAnchor;
            if (t == null) t = side == HandSide.Left ? r.leftHandAnchor : r.rightHandAnchor;
            return t;
        }

        public static OVRInput.Controller Mask(HandSide side) =>
            side == HandSide.Left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;

        public static bool IsConnected(HandSide side) => OVRInput.IsControllerConnected(Mask(side));

        public static string Name(TrackedPoint p) =>
            p == TrackedPoint.Head ? "Głowa" : p == TrackedPoint.LeftController ? "Lewy kontroler" : "Prawy kontroler";

        /// <summary>Transform śledzonego punktu albo null, gdy niedostępny (np. kontroler odłożony).</summary>
        public static Transform Get(TrackedPoint p)
        {
            switch (p)
            {
                case TrackedPoint.Head: return Head;
                case TrackedPoint.LeftController: return IsConnected(HandSide.Left) ? Controller(HandSide.Left) : null;
                default: return IsConnected(HandSide.Right) ? Controller(HandSide.Right) : null;
            }
        }

        /// <summary>Pozycja punktu w przestrzeni śledzenia (tam, gdzie leży wielokąt granicy).</summary>
        public static bool TryGetLocal(TrackedPoint p, out Vector3 local)
        {
            local = Vector3.zero;
            Transform t = Get(p);
            Transform space = TrackingSpace;
            if (t == null || space == null) return false;
            local = space.InverseTransformPoint(t.position);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            rig = null;
            nextFind = 0f;
        }
    }
}
