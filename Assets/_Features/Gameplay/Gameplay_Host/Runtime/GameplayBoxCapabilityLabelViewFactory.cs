using System.Text;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayBoxCapabilityLabelViewFactory : IGameplayEntityViewFactory, IPlayerViewPrefabSource
    {
        private const string CapabilityLabelObjectName = "CapabilityLabel";
        private const int LabelFontSize = 80;

        private static readonly Color LabelColor = new(0.1f, 0.14f, 0.2f);

        private readonly float _cellSize;
        private readonly DefaultGameplayEntityViewFactory _defaultFactory;

        public GameplayBoxCapabilityLabelViewFactory(
            Transform parent,
            float cellSize,
            int playerEntityId,
            GameplayEntityView playerViewPrefab = null,
            IReadOnlyDictionary<int, GameplayEntityView> enemyViewPrefabsByEntityId = null,
            IReadOnlyDictionary<int, GameplayEntityView> staticViewPrefabsByEntityId = null)
        {
            _cellSize = cellSize;
            _defaultFactory = new DefaultGameplayEntityViewFactory(
                parent,
                cellSize,
                playerEntityId,
                playerViewPrefab,
                enemyViewPrefabsByEntityId: enemyViewPrefabsByEntityId,
                staticViewPrefabsByEntityId: staticViewPrefabsByEntityId);
        }

        public GameplayEntityView CreateView(in EntityState entity)
        {
            var view = _defaultFactory.CreateView(entity);
            if (view == null || entity.type != EntityType.Box)
            {
                return view;
            }

            AttachCapabilityLabel(view, entity.boxCapabilities);
            return view;
        }

        public GameplayEntityView PlayerViewPrefab => _defaultFactory.PlayerViewPrefab;

        private void AttachCapabilityLabel(GameplayEntityView view, BoxCapabilities capabilities)
        {
            var existingLabel = view.transform.Find(CapabilityLabelObjectName);
            if (existingLabel != null)
            {
                return;
            }

            var labelObject = new GameObject(CapabilityLabelObjectName);
            labelObject.transform.SetParent(view.transform, worldPositionStays: false);
            labelObject.transform.localPosition = Vector3.zero;
            labelObject.transform.localRotation = Quaternion.identity;
            labelObject.transform.localScale = Vector3.one;

            var textMesh = labelObject.AddComponent<TextMesh>();
            textMesh.text = FormatCapabilities(capabilities);
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = LabelFontSize;
            textMesh.characterSize = Mathf.Max(0.02f, _cellSize * 0.045f);
            textMesh.lineSpacing = 0.9f;
            textMesh.color = LabelColor;

            var renderer = labelObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            var billboard = labelObject.AddComponent<GameplayFloatingTextBillboard>();
            billboard.Initialize(view.transform, _cellSize * 0.9f);
        }

        private static string FormatCapabilities(BoxCapabilities capabilities)
        {
            if (capabilities == BoxCapabilities.None)
            {
                return "None";
            }

            var builder = new StringBuilder();
            AppendCapabilityLine(builder, capabilities, BoxCapabilities.Push, "Push");
            AppendCapabilityLine(builder, capabilities, BoxCapabilities.Flip, "Flip");
            AppendCapabilityLine(builder, capabilities, BoxCapabilities.Item, "Item");
            AppendCapabilityLine(builder, capabilities, BoxCapabilities.Destroy, "Destroy");
            return builder.ToString();
        }

        private static void AppendCapabilityLine(
            StringBuilder builder,
            BoxCapabilities capabilities,
            BoxCapabilities capability,
            string label)
        {
            if ((capabilities & capability) != capability)
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(label);
        }
    }
}
