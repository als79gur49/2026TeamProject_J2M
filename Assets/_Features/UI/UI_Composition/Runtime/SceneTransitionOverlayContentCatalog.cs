using System;
using System.Collections.Generic;
using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    [CreateAssetMenu(
        fileName = "SceneTransitionOverlayContentCatalog",
        menuName = "Game/UI/Scene Transition Overlay Content Catalog")]
    internal sealed class SceneTransitionOverlayContentCatalog : ScriptableObject
    {
        [SerializeField] private SceneTransitionOverlayContentEntry[] _entries = Array.Empty<SceneTransitionOverlayContentEntry>();

        public IReadOnlyList<SceneTransitionOverlayContentEntry> Entries => _entries;
    }

    [Serializable]
    internal sealed class SceneTransitionOverlayContentEntry
    {
        [SerializeField] private StageTransitionKind _transitionKind = StageTransitionKind.Unknown;
        [SerializeField] private SceneTransitionOverlayContentView _contentPrefab;

        public StageTransitionKind TransitionKind => _transitionKind;

        public SceneTransitionOverlayContentView ContentPrefab => _contentPrefab;
    }
}
