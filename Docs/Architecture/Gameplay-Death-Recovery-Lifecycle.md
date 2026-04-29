# Gameplay Death Recovery Lifecycle

Death recovery has one shared timing policy and two separate recovery owners.

## Lifecycle

1. Death cleanup removes the dead player through the canonical cleanup write path.
2. Death recovery hold runs for the configured player respawn delay ticks.
3. After the hold elapses, recovery continues through exactly one lane:
   - in-world respawn through `RespawnProcessor`, or
   - campaign retry / level failed navigation through `CampaignGameplayFlowController`.

## Ownership

- `RespawnProcessor` owns in-world respawn eligibility, topology / placement defer, and `WorldState` spawn writes.
- `CampaignGameplayFlowController` owns campaign retry and level failed navigation.
- Presentation death hold signals only retain the death pose / animation. They do not spawn entities or launch scenes.

## Delay vs Topology Defer

Respawn delay and topology defer are different gates.

- Respawn delay is a deterministic countdown from death cleanup to recovery eligibility.
- Topology / placement defer is evaluated only after respawn delay has elapsed.
- A topology reset cannot replace the death recovery hold.

## Campaign Retry

Campaign retry uses the same death recovery delay policy as in-world respawn, but it does not use `WorldState.SpawnEntity`.
After the hold elapses, campaign flow launches `StageNavigationRequest.Retry` or publishes level failed once.

## Save Durability

Campaign death save updates are committed on the death tick.

- Remaining chances, current stage route, current level group, and total death count are updated before the recovery hold elapses.
- If the app exits during the death recovery hold, the death is still treated as committed.
- TODO: If product policy changes, evaluate moving the save commit to the hold-elapsed transition instead of the death tick.

## Event Naming Compatibility

`PlayerRespawnDelayStarted`, `PlayerRespawnDelayTicking`, and `PlayerRespawnDelayElapsed` currently describe the shared death recovery hold.
They are emitted for in-world respawn and for campaign death recovery when player respawn is disabled.

The names are kept for v1 event compatibility.
Long term, this surface can be renamed to `PlayerDeathRecoveryDelayStarted`, `PlayerDeathRecoveryDelayTicking`, and `PlayerDeathRecoveryDelayElapsed`.
