using UnityEngine;

namespace VRBoundaryDemo
{
    /// <summary>
    /// Wibracje kontrolerów przez OVRInput.SetControllerVibration(częstotliwość, amplituda, kontroler).
    /// Łączy dwa źródła: ciągły poziom (np. bliskość granicy) i krótkie impulsy (np. potwierdzenie przycisku).
    /// Wibracja w OVRInput wygasa sama po ~2 s, dlatego ciągły poziom jest co chwilę odświeżany.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class Haptics : MonoBehaviour
    {
        public static Haptics Instance { get; private set; }

        [Range(0f, 1f)] public float proximityFrequency = 0.25f;
        [Range(0f, 1f)] public float pulseFrequency = 0.7f;
        [Tooltip("Mnożnik wszystkich wibracji (0 = wyłączone).")]
        [Range(0f, 1f)] public float masterAmplitude = 1f;

        readonly float[] level = new float[2];
        readonly float[] pulseAmp = new float[2];
        readonly float[] pulseEnd = new float[2];
        readonly float[] sentAmp = { -1f, -1f };
        readonly float[] sentFreq = { -1f, -1f };
        readonly float[] nextRefresh = new float[2];

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnDisable()
        {
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
        }

        /// <summary>Ciągła wibracja 0..1 (ustawiana co klatkę, np. przez BoundaryMonitor).</summary>
        public void SetLevel(HandSide side, float amplitude) => level[(int)side] = Mathf.Clamp01(amplitude);

        /// <summary>Krótki impuls - ma pierwszeństwo przed poziomem ciągłym.</summary>
        public void Pulse(HandSide side, float amplitude = 0.6f, float duration = 0.06f)
        {
            int i = (int)side;
            pulseAmp[i] = Mathf.Clamp01(amplitude);
            pulseEnd[i] = Time.unscaledTime + Mathf.Max(0.02f, duration);
        }

        public static void PulseStatic(HandSide side, float amplitude = 0.6f, float duration = 0.06f)
        {
            if (Instance != null) Instance.Pulse(side, amplitude, duration);
        }

        void LateUpdate()
        {
            for (int i = 0; i < 2; i++)
            {
                bool pulsing = Time.unscaledTime < pulseEnd[i];
                float amp = (pulsing ? Mathf.Max(pulseAmp[i], level[i]) : level[i]) * masterAmplitude;
                float freq = pulsing ? pulseFrequency : proximityFrequency;
                if (amp < 0.02f) { amp = 0f; freq = 0f; }

                bool changed = Mathf.Abs(amp - sentAmp[i]) > 0.03f || !Mathf.Approximately(freq, sentFreq[i]);
                bool refresh = amp > 0f && Time.unscaledTime >= nextRefresh[i];
                if (!changed && !refresh) continue;

                OVRInput.SetControllerVibration(freq, amp, DemoRig.Mask((HandSide)i));
                sentAmp[i] = amp;
                sentFreq[i] = freq;
                nextRefresh[i] = Time.unscaledTime + 0.5f;
            }
        }
    }
}
