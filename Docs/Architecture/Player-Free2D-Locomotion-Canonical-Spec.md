# Player Free2D Locomotion Canonical Spec

Player ordinary locomotion is Free2D-only.

Input starts as a `Vector2` from the Input System, passes through the move deadzone and orthogonal direction resolution, and enters simulation as a `PlayerTickCommand` with a cardinal `Direction`. Player ordinary movement does not accept unrestricted diagonal movement or arbitrary analog directions.

The simulation advances at the fixed tick cadence accumulated from `Update`. Unity `FixedUpdate`, `Rigidbody2D`, `Transform`, Animator, and root motion do not own authoritative player movement. `WorldState.EntityState.position` remains the anchor-cell truth. Continuous local offset and velocity live in the locomotion state, and authoritative writes are committed through the tick finalization/batch paths.

Player ordinary `Move` is planned by the Free2D local locomotion path, then resolved through collision, sliding, topology transition, and finalization before reaching `WorldState`, `TickResult.PresentationData`, and presentation.

Player ordinary Free2D locomotion has no generic move cooldown owner. It must not read `moveCooldownTicks`, `nextMoveAllowedTick`, `ConsumeMoveCooldown`, or the explicit action retry gate when resolving ordinary velocity, facing, collision, sliding, topology approach, or local offset. Stale external cooldown state is not allowed to stop ordinary Free2D movement.

Push and Flip are explicit player actions, not ordinary Move modes. Their input buttons, windup, execute, recovery, item priority, box sliding, impact reservation, action audio, action animation, and blocked/impact presentation remain outside the ordinary locomotion selector concept.

Canceled Push/Flip restart suppression is action-specific input policy. If the one-tick retry block is needed, it is represented by `PlayerControlState.nextExplicitActionAllowedTick` and consumed only by explicit action start admission. Item or retained grid transactions must own their own lifecycle locks and must not reuse a generic Player move cooldown.

Topology transition presentation lock remains an input admission boundary. While topology presentation is locked, additional tick input is not admitted.

`MotionMode` numeric value `5` is retired/undefined. Current user-facing save slots do not serialize `MotionMode`, `UnitKinematicState`, or `PlayerControlState`; any future runtime snapshot importer must reject undefined enum values instead of silently mapping `5` to an active mode.

Allowed uses of Kinematic terminology are non-player ordinary locomotion scopes: shared fixed-point motion state, enemy ordinary/charge/glide locomotion, and presentation-only helpers. Player ordinary locomotion must not add a Kinematic fallback, Discrete fallback, locomotion mode enum, or runtime feature flag.

Player Free2D owns its authoring values, runtime settings, defaults, validation, production serialized configuration, and host composition snapshot. `PlayerFree2DLocomotionAuthoring` serializes Player Free2D policy and compiles to `PlayerFree2DLocomotionSettings`; it must not use `UnitKinematicLocomotionTimingSettings`, `EnemyLocomotionTimingSettings`, or enemy presets as defaults or fallback sources. Equal numeric timing values between Player and non-player locomotion do not imply shared policy ownership.

Player-specific tests, source files, and public symbols use `PlayerFree2D` naming. Enemy locomotion feature presets use Enemy-owned names such as `DefaultEnemyKinematicLocomotion`; they must not look like global gameplay policy or Player locomotion flags. Shared fixed-point math may expose neutral reference values such as `KinematicFixed.ReferenceUnitsPerTick`, but must not own Player gameplay policy defaults.
