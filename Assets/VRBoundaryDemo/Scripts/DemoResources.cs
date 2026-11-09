using UnityEngine;

namespace VRBoundaryDemo
{
    /// <summary>
    /// Trzyma referencje do materiałów używanych przez obiekty tworzone w trakcie działania aplikacji
    /// (linie i ściany granic, promień, znaczniki). Dzięki referencji w scenie shadery URP
    /// (Lit oraz przezroczysty Unlit) trafiają do builda.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class DemoResources : MonoBehaviour
    {
        public Material litMaterial;
        public Material transparentMaterial;

        void Awake()
        {
            if (litMaterial != null) DemoUI.LitMaterial = litMaterial;
            if (transparentMaterial != null) DemoUI.TransparentMaterial = transparentMaterial;
        }
    }
}
