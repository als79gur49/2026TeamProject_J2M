using System;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.DemoStageControl
{
    public interface IDemoGameplayOverrideCommandPort
    {
        bool PlayerInvincible { get; }

        DemoStageControlResult SetPlayerInvincible(bool enabled);

        DemoStageControlResult TogglePlayerInvincible();

        DemoGameplayOverrideSnapshot GetSnapshot();

        DemoGameplayOverrideStatus GetOverrideStatus();
    }

    public readonly struct DemoGameplayOverrideStatus
    {
        public DemoGameplayOverrideStatus(bool playerInvincible, string lastOverrideMessage)
        {
            PlayerInvincible = playerInvincible;
            LastOverrideMessage = lastOverrideMessage ?? string.Empty;
        }

        public bool PlayerInvincible { get; }

        public string LastOverrideMessage { get; }
    }

    public sealed class DemoGameplayOverrideRuntime :
        IDemoGameplayOverrideCommandPort,
        IDemoGameplayOverrideSnapshotSource
    {
        private readonly DemoStageControlSettings _settings;
        private string _lastOverrideMessage = string.Empty;

        public DemoGameplayOverrideRuntime(DemoStageControlSettings settings)
        {
            _settings = settings ?? DemoStageControlSettings.EnabledByDefault();
        }

        public bool PlayerInvincible { get; private set; }

        public DemoStageControlResult SetPlayerInvincible(bool enabled)
        {
            if (!_settings.Enabled)
            {
                return Remember(DemoStageControlResult.Fail("Demo Stage Control is disabled."));
            }

            PlayerInvincible = enabled;
            return Remember(DemoStageControlResult.Ok(enabled
                ? "Player Invincible ON"
                : "Player Invincible OFF"));
        }

        public DemoStageControlResult TogglePlayerInvincible()
        {
            return SetPlayerInvincible(!PlayerInvincible);
        }

        public DemoGameplayOverrideSnapshot GetSnapshot()
        {
            return new DemoGameplayOverrideSnapshot(_settings.Enabled && PlayerInvincible);
        }

        public DemoGameplayOverrideStatus GetOverrideStatus()
        {
            return new DemoGameplayOverrideStatus(
                _settings.Enabled && PlayerInvincible,
                _lastOverrideMessage);
        }

        private DemoStageControlResult Remember(DemoStageControlResult result)
        {
            _lastOverrideMessage = result.Message;
            return result;
        }
    }
}
