using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace VRBoundaryDemo
{
    /// <summary>Panel na żywo: dane granicy systemowej, stan passthrough i wyniki testów bliskości.</summary>
    public class BoundaryInfoPanel : MonoBehaviour
    {
        public Text text;
        public float refreshInterval = 0.1f;

        readonly StringBuilder sb = new StringBuilder();
        float next;

        void Update()
        {
            if (text == null || Time.unscaledTime < next) return;
            next = Time.unscaledTime + refreshInterval;

            var sys = SystemBoundary.Instance;
            var custom = CustomBoundary.Instance;
            var mon = BoundaryMonitor.Instance;
            var pt = PassthroughBoundaryControl.Instance;
            var mgr = OVRManager.instance;

            sb.Clear();
            sb.Append("<size=34><b>GRANICE</b></size>\n\n");

            sb.Append("<b><color=#7FD8FF>Granica systemowa</color></b>  (OVRManager.boundary)\n");
            if (sys != null)
            {
                sb.Append("Skonfigurowana: ").Append(DemoUI.YesNo(sys.Configured)).Append("   status: ").Append(sys.Status).Append('\n');
                if (sys.Shape.IsValid)
                {
                    sb.Append($"Play Area: {sys.Dimensions.x:0.00} × {sys.Dimensions.z:0.00} m   ")
                      .Append($"pole {sys.Shape.Area():0.0} m²   punkty: {sys.Shape.Count}\n");
                }
                sb.Append($"Odczyt: {Time.unscaledTime - sys.LastRefreshTime:0.0} s temu\n");
            }
            sb.Append("Układ odniesienia: <b>").Append(mgr != null ? mgr.trackingOriginType.ToString() : "-").Append("</b>\n");

            sb.Append("\n<b><color=#C8A0FF>Passthrough i widoczność granicy</color></b>\n");
            if (pt != null && pt.Layer != null)
            {
                sb.Append("Passthrough: ").Append(DemoUI.YesNo(pt.PassthroughOn))
                  .Append("   zainicjalizowany: ").Append(DemoUI.YesNo(OVRManager.IsInsightPassthroughInitialized())).Append('\n');
                sb.Append("Prośba o ukrycie: ").Append(DemoUI.YesNo(pt.SuppressionRequested))
                  .Append("   ukryta przez system: ").Append(DemoUI.YesNo(pt.SuppressionActive)).Append('\n');
                sb.Append("Ostatnie zdarzenie: ").Append(pt.LastSystemVisibility).Append('\n');
            }
            else
            {
                sb.Append("<color=#A0A0A0>brak bloku Passthrough</color>\n");
            }

            sb.Append("\n<b><color=#FFD27F>Własna granica</color></b>\n");
            if (custom != null)
            {
                var s = custom.Shape;
                sb.Append($"Punkty: {s.Count}");
                if (s.IsValid) sb.Append($"   obwód {s.Perimeter():0.0} m   pole {s.Area():0.0} m²");
                else if (s.Count > 0) sb.Append("   (potrzeba min. 3)");
                sb.Append('\n');
                if (custom.IsLocked)
                    sb.Append("Edycja: <color=#FFD27F>ZABLOKOWANA</color> (krótki prawy grip = odblokuj)\n");
                else if (custom.SecondsToLock >= 0f)
                    sb.Append($"Edycja: aktywna – blokada za {custom.SecondsToLock:0} s bez zmian\n");
                else
                    sb.Append("Edycja: aktywna\n");
            }

            if (mon != null)
            {
                sb.Append("\n<b>Test bliskości</b> – aktywna: <b>").Append(mon.SourceName).Append("</b>\n");
                var shape = mon.ActiveShape;
                if (shape == null || !shape.IsValid)
                {
                    sb.Append("<color=#A0A0A0>brak wielokąta do testów</color>\n");
                }
                else
                {
                    foreach (var st in mon.States)
                    {
                        sb.Append(DemoRig.Name(st.point)).Append(": ");
                        if (!st.available) { sb.Append("<color=#A0A0A0>niedostępny</color>\n"); continue; }
                        sb.Append(ZoneText(st.zone)).Append($"   {st.distance:0.00} m do krawędzi\n");
                    }
                }
            }
            text.text = sb.ToString();
        }

        static string ZoneText(BoundaryZone z)
        {
            switch (z)
            {
                case BoundaryZone.Safe: return "<color=#7CFC7C>bezpiecznie</color>";
                case BoundaryZone.Warning: return "<color=#FFD27F>blisko krawędzi</color>";
                case BoundaryZone.Outside: return "<color=#FF7070>POZA</color>";
                default: return "<color=#A0A0A0>?</color>";
            }
        }
    }
}
