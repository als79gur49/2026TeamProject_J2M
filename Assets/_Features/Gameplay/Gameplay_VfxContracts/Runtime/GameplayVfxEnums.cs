namespace Game.Feature.Gameplay.Vfx
{
    public enum GameplayVfxFamily
    {
        None = 0,
        Player = 1,
        Box = 2,
        Enemy = 3,
        TileFeature = 4,
        Environment = 5,
        Projectile = 6,
        ObjectiveStage = 7,
        GravityField = 8,
    }

    public enum GameplayVfxCleanupReason
    {
        Unknown = 0,
        HostDefaultMapReconfigured = 1,
        EnemyProfileFirstConfigure = 2,
        EnemyProfileChanged = 3,
        TopologyTransitionStarted = 4,
        SessionReset = 5,
        RuntimeDispose = 6,
        ManualHardCleanup = 7,
        FamilyProfilesReconfigured = 8,
        AllGameplayVfxDisabled = 9,
        ProjectorOrStateStoreChanged = 10,
        StageTerminal = 11,
    }

    public enum GameplayVfxCleanupScope
    {
        None = 0,
        AllFamilies = 1,
        EnemyFamily = 2,
    }

    public enum PlayerVfxCue
    {
        Damage = 1,
        Death = 2,
        PushWindup = 3,
        PushExecute = 4,
        FlipWindup = 5,
        FlipExecute = 6,
        RecoveryDust = 7,
    }

    public enum BoxVfxCue
    {
        SlideStartDust = 1,
        SlideLoopDust = 2,
        SlideStopDust = 3,
        FlipArcTrail = 4,
        FlipImpactBurst = 5,
        DestroySmoke = 6,
        ItemConsume = 7,
        FlipDestroySelfMotion = 8,
        DestroyShrink = 10,
        ImpactTransientBreak = 11,
        OutOfBoundsExit = 12,
        FlipImpactStayTrail = 13,
        BoxSlideSolidStop = 14,
        BoxSlideFollowLoop = 15,
    }

    public enum EnemyVfxCue
    {
        Damage = 1,
        Death = 2,
        Spawn = 3,
        MeleeWindup = 4,
        ChargeWindup = 5,
        ChargeTrail = 6,
        JumpLanding = 7,
        PhaseBlink = 8,
        ShieldBlock = 9,
        SummonWindupWarning = 10,
        JumperLandingTarget = 11,
        JumperLandingDust = 12,
        RetiredEnemyCue13 = 13,
        RetiredEnemyCue14 = 14,
        DeathMotion = 15,
        RetiredEnemyCue16 = 16,
        OutOfBoundsExit = 17,
        GlideWindTrail = 18,
        ChargeBoosterTrail = 19,
        SummonedEnemySpawn = 20,
        JumperJumpStart = 21,
        JumperWindupLoop = 22,
        GlideWindupLoop = 23,
        GlideRecoverLoop = 24,
        UtilityCooldownAura = 26,
        GravityFieldAuraWindupArea = 27,
        GravityFieldAuraActiveArea = 28,
        GravityFieldAuraActiveStarted = 29,
        GlideMagicBlastFollow = 30,
    }

    public enum TileFeatureVfxCue
    {
        TrapArmed = 1,
        TrapTriggered = 2,
        TrapConsumed = 3,
        HazardPulse = 4,
        BuffApplied = 5,
        TileExpired = 6,
        ButtonActivated = 7,
        DestroyTileTriggered = 8,
        SlideTileRedirectedUp = 9,
        SlideTileRedirectedRight = 10,
        SlideTileRedirectedDown = 11,
        SlideTileRedirectedLeft = 12,
        BarricadeBlockedUp = 13,
        BarricadeBlockedRight = 14,
        BarricadeBlockedDown = 15,
        BarricadeBlockedLeft = 16,
        BarricadeCrushed = 17,
        ExitOpened = 18,
        ExitEntered = 19,
        MoonBlockGenerated = 20,
        DestroyTileLaserActive = 21,
        BarricadeActiveLoop = 22,
        ButtonActiveLoop = 23,
        ExitObjectiveCleared = 24,
        ExitOpenLoop = 25,
        ButtonVisibleLoop = 26,
        EntranceSpawn = 27,
    }

    public enum GravityFieldVfxCue
    {
        Activated = 1,
        Expired = 2,
        ChargingArea = 3,
        ActiveArea = 4,
        LockedTarget = 5,
        ChargeStarted = 6,
        ActiveStarted = 7,
    }

    public enum EnvironmentVfxCue
    {
        EnvironmentChanged = 1,
        SurfaceCrack = 2,
        SurfaceRestore = 3,
    }

    public enum ProjectileVfxCue
    {
        Spawn = 1,
        Trail = 2,
        Hit = 3,
        Expired = 4,
        ForwardCellDangerMarker = 5,
        ForwardCellProjectileFlight = 6,
        ForwardCellImpact = 7,
        ForwardCellProjectileActive = 8,
        ForwardCellProjectileFlightFollow = 9,
        ForwardCellAttackCooldownFollow = 10,
    }

    public enum ObjectiveStageVfxCue
    {
        ObjectiveUpdated = 1,
        ObjectiveCompleted = 2,
        StageClear = 3,
        StageFailed = 4,
    }

    public enum VfxAnchorKind
    {
        None = 0,
        Cell = 1,
        Entity = 2,
        EntitySlot = 3,
        MotionTrack = 4,
        CellToEntity = 5,
        EntityToCell = 6,
        BoardLocal = 7,
        Screen = 8,
    }

    public enum VfxAnchorSlot
    {
        None = 0,
        CellFloor = 1,
        CellCenter = 2,
        CellAboveOccupant = 3,
        EntityFeet = 4,
        EntityCenter = 5,
        EntityHead = 6,
        EntityFront = 7,
        EntityBack = 8,
        HitPoint = 9,
        MotionPath = 10,
    }

    public enum VfxTimingKind
    {
        ImmediateOnTickPresentation = 0,
        AtMotionStart = 1,
        DuringMotion = 2,
        AtMotionContact = 3,
        AtMotionEnd = 4,
        OnStateEnter = 5,
        OnStateExit = 6,
        Delayed = 7,
        QueuedUntilTopologyTransitionEnd = 8,
    }

    public enum VfxPlaybackMode
    {
        OneShot = 0,
        Loop = 1,
        Follow = 2,
        MotionTrack = 3,
        Decal = 4,
    }

    public enum VfxVisualSourceMode
    {
        // The authored prefab is the visual body. A null prefab is invalid and source view lookup is not required.
        PrefabOnly = 0,
        // A source view clone may be the primary visual path, while the authored prefab is a real fallback visual.
        // ExplicitPrefabRequired keeps source-view and prefab diagnostics separate.
        PrefabWithSourceClone = 1,
        // The source view clone is the visual body. The cue prefab is optional when a common empty host is available.
        SourceCloneMotion = 2,
    }

    public enum GameplayVfxHostRequirement
    {
        ExplicitPrefabRequired = 0,
        CommonHostAllowed = 1,
    }

    public enum VfxStopPolicy
    {
        NaturalCompletion = 0,
        AuthoredDuration = 1,
        StopEmittingThenRelease = 2,
        DetachThenStopEmittingThenRelease = 3,
        ManualStopRequired = 4,
        HardCleanupOnly = 5,
    }

    public enum VfxMissingAnchorPolicy
    {
        SkipOptional = 0,
        UseFallbackCell = 1,
        ReportDiagnostic = 2,
        FailFast = 3,
    }
}
