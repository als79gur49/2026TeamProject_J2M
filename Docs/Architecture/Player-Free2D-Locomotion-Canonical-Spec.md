# Player Free2D Locomotion Canonical Spec

Player ordinary locomotion is Free2D-only.

Input starts as a `Vector2` from the Input System, passes through the move deadzone and orthogonal direction resolution, and enters simulation as a `PlayerTickCommand` with a cardinal `Direction`. Player ordinary movement does not accept unrestricted diagonal movement or arbitrary analog directions.

The simulation advances at the fixed tick cadence accumulated from `Update`. Unity `FixedUpdate`, `Rigidbody2D`, `Transform`, Animator, and root motion do not own authoritative player movement. `WorldState.EntityState.position` remains the anchor-cell truth. Continuous local offset and velocity live in the locomotion state, and authoritative writes are committed through the tick finalization/batch paths.

Player ordinary `Move` is planned by the Free2D local locomotion path, then resolved through collision, sliding, topology transition, and finalization before reaching `WorldState`, `TickResult.PresentationData`, and presentation.

Push and Flip are explicit player actions, not ordinary Move modes. Their input buttons, windup, execute, recovery, item priority, box sliding, impact reservation, action audio, action animation, and blocked/impact presentation remain outside the ordinary locomotion selector concept.

Topology transition presentation lock remains an input admission boundary. While topology presentation is locked, additional tick input is not admitted.

Allowed uses of Kinematic terminology are non-player ordinary locomotion scopes: shared fixed-point motion state, enemy ordinary/charge/glide locomotion, and presentation-only helpers. Player ordinary locomotion must not add a Kinematic fallback, Discrete fallback, locomotion mode enum, or runtime feature flag.
