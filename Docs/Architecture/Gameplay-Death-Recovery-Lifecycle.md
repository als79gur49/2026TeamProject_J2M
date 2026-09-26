# Gameplay Death Recovery Lifecycle

Player death removes the actor from the authoritative world. A retry creates a new stage host through campaign navigation; the tick pipeline does not recreate the player in the same world.

## Lifecycle

1. Accepted fatal damage or DestroyTile movement identifies the death source. Cleanup removes the player through its canonical write path.
2. The tick result emits one player death signal with the selected source and direction hint. Presentation retains the removed actor's pose for death playback. The hold is produced from the death signal without a respawn countdown.
3. `GameplayInputHost` blocks further gameplay ticks and player commands once it receives the death signal. The block ends only when a new host is initialized.
4. `CampaignGameplayFlowController` commits the mode-specific death update on the death tick. Casual saves HP 3 and the current level group's first stage, then starts LevelFailed playback. Hardcore saves the reduced Chance and retries the same stage when Chance remains; on the last Chance it saves Chance 3 and the campaign's first stage, then starts LevelFailed playback. A retry or explicit restart loads a new stage host at the terminal playback milestone.

## Ownership

- `WorldState` owns the removal. The tick result carries death and presentation data but cannot spawn a replacement player.
- `CampaignGameplayFlowController` owns mode-specific HP or Chance persistence and retry or LevelFailed navigation.
- Presentation owns death animation, direction, retained pose, and hold. These effects do not mutate authoritative gameplay state.
- The MoonBlock generator separately creates its configured box after Cleanup. Its spawn and blocked facts are unrelated to player recovery.

## Save Durability

The mode-specific HP or Chance, stage route, level group, and total deaths are committed before terminal playback completes. Casual death saves HP 3 and the current level group's first stage; Hardcore death saves the reduced Chance and same stage or, after the last Chance, Chance 3 and the campaign's first stage. If the app exits during playback after a successful save, the death remains committed. A synchronous save failure abandons that host instead of starting another tick.
