using UnityEngine;

namespace VRBoundaryDemo
{
    /// <summary>
    /// Ruch w wirtualnej scenie (OVRInput): lewa gałka - płynny ruch w kierunku patrzenia,
    /// prawa gałka w lewo/prawo - obrót skokowy o 45°, klik prawej gałki - powrót na start.
    /// Przesuwa cały OVRCameraRig. Granica (systemowa i własna) jest w przestrzeni śledzenia,
    /// czyli porusza się RAZEM z rigiem - zostaje w miejscu fizycznego pokoju, nie sceny.
    /// </summary>
    public class PlayerLocomotion : MonoBehaviour
    {
        public float moveSpeed = 1.5f;
        public float snapAngle = 45f;
        [Range(0.1f, 1f)] public float stickDeadZone = 0.15f;
        public bool enableThumbsticks = true;

        Vector3 startPosition;
        Quaternion startRotation;
        bool hasStart;
        bool turnArmed = true;
        bool moving;

        void Update()
        {
            var rig = DemoRig.Rig;
            if (rig == null) return;
            if (!hasStart)
            {
                startPosition = rig.transform.position;
                startRotation = rig.transform.rotation;
                hasStart = true;
            }
            if (!enableThumbsticks) return;

            Transform head = rig.centerEyeAnchor != null ? rig.centerEyeAnchor : rig.transform;

            // Lewa gałka: ruch
            Vector2 move = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
            bool nowMoving = move.magnitude > stickDeadZone;
            if (nowMoving)
            {
                Vector3 dir = Flat(head.forward) * move.y + Flat(head.right) * move.x;
                rig.transform.position += dir * (moveSpeed * Time.deltaTime);
            }
            if (nowMoving && !moving) DemoLog.Add("Lewa gałka: ruch w scenie (granica jedzie razem z Tobą)");
            moving = nowMoving;

            // Prawa gałka: obrót skokowy z "uzbrajaniem" (powrót do środka przed kolejnym obrotem)
            float x = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch).x;
            if (turnArmed && Mathf.Abs(x) > 0.75f)
            {
                rig.transform.RotateAround(head.position, Vector3.up, Mathf.Sign(x) * snapAngle);
                DemoLog.Add(x < 0 ? "Prawa gałka: obrót w lewo" : "Prawa gałka: obrót w prawo");
                turnArmed = false;
            }
            else if (Mathf.Abs(x) < 0.3f)
            {
                turnArmed = true;
            }

            if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch))
            {
                rig.transform.SetPositionAndRotation(startPosition, startRotation);
                DemoLog.Add("Klik prawej gałki: powrót na start");
            }
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized;
        }
    }
}
