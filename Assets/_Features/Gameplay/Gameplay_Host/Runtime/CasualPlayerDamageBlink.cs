using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    // Presentation only: simulation owns both HP and the damage admission boundary.
    public sealed class CasualPlayerDamageBlink : MonoBehaviour
    {
        private GameplayInputHost _input;
        private GameplayEntityViewRegistry _views;
        private Func<WorldSnapshot> _snapshot;
        private GameplayEntityView _view;
        private Renderer[] _renderers = Array.Empty<Renderer>();
        private bool[] _original = Array.Empty<bool>();
        private int _halfPeriodTicks;

        internal void Initialize(GameplayInputHost input, GameplayEntityViewRegistry views,
            Func<WorldSnapshot> snapshot, int ticksPerSecond)
        {
            _input = input;
            _views = views;
            _snapshot = snapshot;
            _halfPeriodTicks = Math.Max(1, (int)Math.Ceiling(ticksPerSecond * 0.1));
            _input.TickCompleted += Present;
        }

        private void Present(TickResult result)
        {
            if (!_views.TryGetView(_input.PlayerEntityId, out var view) || view != _view)
            {
                Restore();
                _view = view;
                _renderers = view != null ? view.ModelRoot.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
                _original = new bool[_renderers.Length];
                for (var i = 0; i < _renderers.Length; i++) _original[i] = _renderers[i].forceRenderingOff;
            }
            var snapshot = _snapshot();
            var protectedPlayer = !_input.IsCampaignRunAbandoned &&
                snapshot.TryGetEntity(_input.PlayerEntityId, out var player) && player.hp > 0 &&
                !player.markedForDeath && snapshot.TryGetPlayerDamageState(player.entityId, out var damage) &&
                result.TickIndex < damage.nextDamageAllowedTick;
            var hide = protectedPlayer && (result.TickIndex / _halfPeriodTicks) % 2 == 0;
            for (var i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].forceRenderingOff = _original[i] || hide;
        }

        private void Restore()
        {
            for (var i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].forceRenderingOff = _original[i];
        }

        private void OnDisable() => Restore();
        private void OnDestroy()
        {
            if (_input != null) _input.TickCompleted -= Present;
            Restore();
        }
    }
}
