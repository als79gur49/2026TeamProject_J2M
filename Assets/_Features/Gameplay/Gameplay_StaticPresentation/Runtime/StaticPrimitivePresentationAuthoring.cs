using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameplayEntityView))]
    public sealed class StaticPrimitivePresentationAuthoring : MonoBehaviour
    {
        private const string VisualObjectName = "Visual";

        [SerializeField] private Color color = new(0.72f, 0.5f, 0.24f, 1f);
        [SerializeField] private Vector3 modelLocalPosition = new(0f, 0f, -0.32f);
        [SerializeField] private Vector3 modelLocalScale = new(0.92f, 0.92f, 0.42f);

        private void Awake()
        {
            EnsureVisual();
        }

        private void EnsureVisual()
        {
            if (!TryGetComponent<GameplayEntityView>(out var view))
            {
                return;
            }

            view.ConfigureModelRoot(modelLocalPosition, Quaternion.identity);

            var existingVisual = view.ModelRoot.Find(VisualObjectName);
            if (existingVisual != null)
            {
                ConfigureVisual(existingVisual.gameObject);
                return;
            }

            UnityEngine.Debug.LogWarning(
                $"{nameof(StaticPrimitivePresentationAuthoring)} on '{name}' requires a child visual named '{VisualObjectName}' under '{view.ModelRoot.name}'.",
                this);
        }

        private void ConfigureVisual(GameObject visual)
        {
            if (visual == null)
            {
                return;
            }

            visual.transform.localScale = modelLocalScale;

            var collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }

            if (!visual.TryGetComponent<MeshRenderer>(out var renderer))
            {
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Standard");
            if (shader != null)
            {
                var material = new Material(shader)
                {
                    color = color,
                };
                renderer.sharedMaterial = material;
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
