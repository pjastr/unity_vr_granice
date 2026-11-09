using UnityEngine;

namespace VRBoundaryDemo
{
    /// <summary>
    /// Koloruje obiekt przez MaterialPropertyBlock (bez tworzenia kopii materiału).
    /// Ustawia zarówno _BaseColor (URP Lit/Unlit) jak i _Color (Built-in Standard).
    /// Kanał alfa jest używany przez półprzezroczyste ściany granic.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ColorTint : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] Color baseColor = Color.white;

        Renderer rend;
        MaterialPropertyBlock block;
        Color applied = new Color(-1f, -1f, -1f, -1f);

        public Color BaseColor => baseColor;

        public void SetBaseColor(Color color)
        {
            baseColor = color;
            Apply();
        }

        /// <summary>Zmienia tylko przezroczystość (0..1) koloru bazowego.</summary>
        public void SetAlpha(float alpha)
        {
            Color c = baseColor;
            c.a = Mathf.Clamp01(alpha);
            SetBaseColor(c);
        }

        void OnEnable()
        {
            applied = new Color(-1f, -1f, -1f, -1f);
            Apply();
        }

        void OnValidate() => Apply();

        void Apply()
        {
            if (rend == null) rend = GetComponent<Renderer>();
            if (rend == null) return;
            if (applied == baseColor && block != null) return; // bez zmian - nie ruszamy renderera
            if (block == null) block = new MaterialPropertyBlock();

            rend.GetPropertyBlock(block);
            block.SetColor(BaseColorId, baseColor);
            block.SetColor(ColorId, baseColor);
            rend.SetPropertyBlock(block);
            applied = baseColor;
        }
    }
}
