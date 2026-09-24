# Gameplay Death Recovery Lifecycle

Player death removes the actor from the authoritative world. A retry creates a new stage host through campaign navigation; the tick pipeline does not recreate the player in the same world.

## Lifecycle

1. Accepted fatal damage or DestroyTile movement identifies the death source. Cleanup removes the player through its canonical write path.
2. The tick result emits one player death signal with the selected source and direction hint. Presentation retains the removed actor's pose for death playback. The hold is produced from the death signal without a respawn countdown.
3. `GameplayInputHost` blocks further gameplay ticks and player commands once it receives the death signal. The block ends only when a new host is initialized.
4. `CampaignGameplayFlowController` commits the chance update on the death tick. It starts retry or level failed terminal playback. The retry handoff loads a new stage host at the terminal playback milestone.

## Ownership

- `WorldState` owns the removal. The tick result carries death and presentation data but cannot spawn a replacement player.
- `CampaignGameplayFlowController` owns chance persistence and retry or level failed navigation.
- Presentation owns death animation, direction, retained pose, and hold. These effects do not mutate authoritative gameplay state.
- The MoonBlock generator separately creates its configured box after Cleanup. Its spawn and blocked facts are unrelated to player recovery.

## Save Durability

Remaining chances, the current stage route, level group, and total deaths are committed before terminal playback completes. If the app exits during playback, the death remains committed.
