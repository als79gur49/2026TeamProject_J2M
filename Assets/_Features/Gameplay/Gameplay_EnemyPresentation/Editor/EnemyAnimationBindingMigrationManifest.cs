using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.Host;

namespace Game.Feature.Gameplay.Host.EditorTools
{
    internal enum EnemyAnimationMigrationDisposition
    {
        MigratedBinding = 0,
        ApprovedNoBinding = 1,
    }

    internal readonly struct EnemyAnimationMigrationClipIdentity
    {
        internal EnemyAnimationMigrationClipIdentity(string guid, long localFileId)
        {
            Guid = guid ?? string.Empty;
            LocalFileId = localFileId;
        }

        internal string Guid { get; }
        internal long LocalFileId { get; }
        internal bool IsEmpty => string.IsNullOrEmpty(Guid) && LocalFileId == 0;
    }

    internal readonly struct EnemyAnimationMigrationBinding
    {
        internal EnemyAnimationMigrationBinding(
            EnemyAnimationCue cue,
            EnemyAnimationDispatchMode mode,
            string targetName,
            string sustainedStateName,
            float durationSeconds,
            EnemyAnimationMigrationClipIdentity clip,
            EnemyAnimationMigrationClipIdentity effectiveMotion,
            string replacementStateName = "")
        {
            Cue = cue;
            Mode = mode;
            TargetName = targetName ?? string.Empty;
            SustainedStateName = sustainedStateName ?? string.Empty;
            ReplacementStateName = replacementStateName ?? string.Empty;
            DurationSeconds = durationSeconds;
            Clip = clip;
            EffectiveMotion = effectiveMotion;
        }

        internal EnemyAnimationCue Cue { get; }
        internal EnemyAnimationDispatchMode Mode { get; }
        internal string TargetName { get; }
        internal string SustainedStateName { get; }
        internal string ReplacementStateName { get; }
        internal float DurationSeconds { get; }
        internal EnemyAnimationMigrationClipIdentity Clip { get; }
        internal EnemyAnimationMigrationClipIdentity EffectiveMotion { get; }
    }

    internal sealed class EnemyAnimationMigrationRow
    {
        internal EnemyAnimationMigrationRow(
            string name,
            string prefabPath,
            string prefabGuid,
            EnemyAnimationMigrationDisposition disposition,
            long rootLocalFileId,
            long driverLocalFileId,
            bool animatorWasExplicit,
            string animatorTransformPath,
            long animatorLocalFileId,
            string controllerGuid,
            long controllerLocalFileId,
            float crossFadeSeconds,
            params EnemyAnimationMigrationBinding[] bindings)
        {
            Name = name;
            PrefabPath = prefabPath;
            PrefabGuid = prefabGuid;
            Disposition = disposition;
            RootLocalFileId = rootLocalFileId;
            DriverLocalFileId = driverLocalFileId;
            AnimatorWasExplicit = animatorWasExplicit;
            AnimatorTransformPath = animatorTransformPath;
            AnimatorLocalFileId = animatorLocalFileId;
            ControllerGuid = controllerGuid;
            ControllerLocalFileId = controllerLocalFileId;
            CrossFadeSeconds = crossFadeSeconds;
            Bindings = new ReadOnlyCollection<EnemyAnimationMigrationBinding>(
                bindings ?? Array.Empty<EnemyAnimationMigrationBinding>());
        }

        internal string Name { get; }
        internal string PrefabPath { get; }
        internal string PrefabGuid { get; }
        internal EnemyAnimationMigrationDisposition Disposition { get; }
        internal long RootLocalFileId { get; }
        internal long DriverLocalFileId { get; }
        internal bool AnimatorWasExplicit { get; }
        internal string AnimatorTransformPath { get; }
        internal long AnimatorLocalFileId { get; }
        internal string ControllerGuid { get; }
        internal long ControllerLocalFileId { get; }
        internal float CrossFadeSeconds { get; }
        internal IReadOnlyList<EnemyAnimationMigrationBinding> Bindings { get; }
    }

    internal static class EnemyAnimationBindingMigrationManifest
    {
        internal const int SchemaVersion = 1;
        internal const string DriverScriptGuid = "2b6f35d89b0440b0a3897d9c5c63f8a4";
        internal const string TimingScriptGuid = "221aa3bf1f4e4fec8fe376442cc63a61";
        internal const string BindingScriptGuid = "ec56f2a2d2f04a14ac0a0f810100525a";
        internal const string ProductionPrefabRoot =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs";

        private static readonly IReadOnlyList<EnemyAnimationMigrationRow> ManifestRows =
            new ReadOnlyCollection<EnemyAnimationMigrationRow>(new[]
            {
                Row("BlackEye", "EnemyView_BlackEye.prefab", "d25f546e192650048aeec864272891c2",
                    EnemyAnimationMigrationDisposition.MigratedBinding,
                    1253227309742569353L, 1395465871142893710L,
                    true, "ModelRoot", 5272011485980291436L,
                    "109a9930e5ded9e468e17d6c94c253e1", 9100000L,
                    0.001f,
                    State(EnemyAnimationCue.ActionWindup, "Windup", 1f,
                        Clip("4d048268569f7314ca6e0610d38303f9", -998496362607748373L),
                        Clip("4d048268569f7314ca6e0610d38303f9", -998496362607748373L)),
                    State(EnemyAnimationCue.ActionRecovery, "Recover", 1f,
                        Clip("4d048268569f7314ca6e0610d38303f9", -3291206362154003629L),
                        Clip("4d048268569f7314ca6e0610d38303f9", -3291206362154003629L)),
                    Trigger(EnemyAnimationCue.Hit, "Hit"),
                    Trigger(EnemyAnimationCue.Death, "Death")),

                Row("Startis", "EnemyView_Startis.prefab", "d2fcbcdf8d3b4dd4b88dd02f4fb843ae",
                    EnemyAnimationMigrationDisposition.MigratedBinding,
                    1870086140720068720L, 2743689776297013704L,
                    true, "ModelRoot", 5480558589221225980L,
                    "765230bf4a8129e4a8d267d7aecade3c", 9100000L,
                    -1f,
                    Trigger(EnemyAnimationCue.Hit, "Hit"),
                    Trigger(EnemyAnimationCue.Death, "Death")),

                Row("RocketFace", "EnemyView_RocketFace.prefab", "4b046e9ae49c42b4388ff25a2353c3f7",
                    EnemyAnimationMigrationDisposition.MigratedBinding,
                    1253227309742569353L, 1395465871142893710L,
                    false, "ModelRoot/RF15(Edit)", 7156270818889048741L,
                    "2062576fffb8ec9438c6e0465cc89fbe", 9100000L,
                    0.001f,
                    State(EnemyAnimationCue.ChargeWindup, "Windup", 0.4f,
                        Clip("1716406119d8be34d841f0d4eb033a2c", 3060872287085348379L),
                        Clip("756a198ed5d59c84d8eb288b427f4916", 7400000L)),
                    State(EnemyAnimationCue.ChargeActive, "Charge", effectiveMotion:
                        Clip("7d3dc2a6481db8e4c9a28cd6ee555dd3", 7400000L)),
                    State(EnemyAnimationCue.ChargeRecovery, "Recover", 0.4f,
                        Clip("1716406119d8be34d841f0d4eb033a2c", -5059006814000262888L),
                        Clip("820152571963c8d409611f50e22d2528", 7400000L)),
                    Trigger(EnemyAnimationCue.Hit, "Hit"),
                    Trigger(EnemyAnimationCue.Death, "Death")),

                Row("Astreton", "EnemyView_Astreton.prefab", "7aae229c82bf72e49c105c163ee2d676",
                    EnemyAnimationMigrationDisposition.MigratedBinding,
                    1001000000000003001L, 1141000000000003004L,
                    true, "ModelRoot/Astra", 8501000000000003001L,
                    "6be19350b5cac6763aebb3b88b54c091", 9100000L,
                    0.001f,
                    State(EnemyAnimationCue.JumpWindup, "JumpWindup", 0.5f,
                        Clip("9870afb7c6d615c458c88e0e341207e3", 9067093048684652814L),
                        Clip("9870afb7c6d615c458c88e0e341207e3", 9067093048684652814L)),
                    State(EnemyAnimationCue.JumpAirborne, "JumpAirborne", 1.35f,
                        Clip("9870afb7c6d615c458c88e0e341207e3", -5560811472042823391L),
                        Clip("9870afb7c6d615c458c88e0e341207e3", -5560811472042823391L)),
                    State(EnemyAnimationCue.JumpLanding, "Move", effectiveMotion:
                        Clip("9870afb7c6d615c458c88e0e341207e3", 1827226128182048838L)),
                    Trigger(EnemyAnimationCue.ActionExecute, "Attack"),
                    Trigger(EnemyAnimationCue.Hit, "Hit"),
                    Trigger(EnemyAnimationCue.Death, "Death")),

                Row("DrSaturn", "EnemyView_DrSaturn.prefab", "8fa155d3aa7c4ea4bb582c5fb364e801",
                    EnemyAnimationMigrationDisposition.MigratedBinding,
                    1001000000000008001L, 1141000000000008004L,
                    true, "ModelRoot/DrS155", 8501000000000008001L,
                    "a0d4ea646ac84336b7ba46d45aac2875", 9100000L,
                    -1f,
                    Trigger(EnemyAnimationCue.UtilityWindup, "Windup", 0.5f,
                        Clip("19cc367ffcf133f42b0c772ae30ed0b1", 7400000L), replacementStateName: "Windup"),
                    Trigger(EnemyAnimationCue.UtilityRecovery, "Recover", 0.5f,
                        Clip("c0977b05374049f46a273f93a78ed85e", 7400000L), replacementStateName: "Recover"),
                    Trigger(EnemyAnimationCue.Hit, "Hit"),
                    Trigger(EnemyAnimationCue.Death, "Death", replacementStateName: "Death")),

                Row("JPeter", "EnemyView_JPeter.prefab", "7fa07cb2cf222ca4194569602c56204a",
                    EnemyAnimationMigrationDisposition.MigratedBinding,
                    1747997918927781177L, 4994410876415863292L,
                    false, "ModelRoot", 4338433049514668751L,
                    "086aec4d1610b934c84a11e74061f8ef", 9100000L,
                    -1f,
                    Trigger(EnemyAnimationCue.Hit, "Hit"),
                    Trigger(EnemyAnimationCue.Death, "Death")),

                Row("Sunwheel", "EnemyView_Sunwheel.prefab", "40031cd173e7ca9902ba3c3d971f4239",
                    EnemyAnimationMigrationDisposition.MigratedBinding,
                    1001000000000002001L, 1141000000000002004L,
                    true, "ModelRoot/Sun", 8501000000000002001L,
                    "506850279d08adc3d62015a52b2109e4", 9100000L,
                    -1f,
                    Trigger(EnemyAnimationCue.Hit, "Hit"),
                    Trigger(EnemyAnimationCue.Death, "Death")),

                Row("Kali", "EnemyView_Kali.prefab", "6eced58e0a1a4a6881cc0c15aa61f402",
                    EnemyAnimationMigrationDisposition.ApprovedNoBinding,
                    1001000000000009001L, 1141000000000009003L,
                    false, "ModelRoot", 3191573246195697018L,
                    "87dbf2bc5ae1492c944a2de44af108a0", 9100000L,
                    -1f),

                Row("SecBot", "EnemyView_SecBot.prefab", "fca7744895a64016aab93a7e38807d41",
                    EnemyAnimationMigrationDisposition.ApprovedNoBinding,
                    1001000000000010001L, 1141000000000010003L,
                    false, "ModelRoot", 1872103958095815267L,
                    "49bfa175ca8131a4a8b4f74d841dd21a", 9100000L,
                    -1f),

                Row("Nebulous", "EnemyView_Nebulous.prefab", "18e3b2aebc74a9b43b5f5cda65339d6b",
                    EnemyAnimationMigrationDisposition.MigratedBinding,
                    1870086140720068720L, 2743689776297013704L,
                    false, "ModelRoot/Neb_Edit_1", 8883044015288239240L,
                    "3a10426ae31af474ca733cdfcf2543d7", 9100000L,
                    0f,
                    State(EnemyAnimationCue.GlideWindup, "Fly_Start", 0.25f,
                        Clip("6e5d739cfb05dc642b45b8dd92c37c82", 9067093048684652814L),
                        Clip("6e5d739cfb05dc642b45b8dd92c37c82", 9067093048684652814L)),
                    State(EnemyAnimationCue.GlideActive, "Fly_Loop", effectiveMotion:
                        Clip("6e5d739cfb05dc642b45b8dd92c37c82", -8481709605947105597L)),
                    State(EnemyAnimationCue.GlideRecovery, "Fly_Done", 0.25f,
                        Clip("6e5d739cfb05dc642b45b8dd92c37c82", 1178167638873134046L),
                        Clip("6e5d739cfb05dc642b45b8dd92c37c82", 1178167638873134046L))),
            });

        internal static IReadOnlyList<EnemyAnimationMigrationRow> Rows => ManifestRows;

        private static EnemyAnimationMigrationRow Row(
            string name,
            string prefabName,
            string prefabGuid,
            EnemyAnimationMigrationDisposition disposition,
            long rootLocalFileId,
            long driverLocalFileId,
            bool animatorWasExplicit,
            string animatorTransformPath,
            long animatorLocalFileId,
            string controllerGuid,
            long controllerLocalFileId,
            float crossFadeSeconds,
            params EnemyAnimationMigrationBinding[] bindings)
        {
            return new EnemyAnimationMigrationRow(
                name,
                ProductionPrefabRoot + "/" + prefabName,
                prefabGuid,
                disposition,
                rootLocalFileId,
                driverLocalFileId,
                animatorWasExplicit,
                animatorTransformPath,
                animatorLocalFileId,
                controllerGuid,
                controllerLocalFileId,
                crossFadeSeconds,
                bindings);
        }

        private static EnemyAnimationMigrationBinding State(
            EnemyAnimationCue cue,
            string target,
            float duration = -1f,
            EnemyAnimationMigrationClipIdentity clip = default,
            EnemyAnimationMigrationClipIdentity effectiveMotion = default)
        {
            return new EnemyAnimationMigrationBinding(
                cue, EnemyAnimationDispatchMode.State, target, string.Empty, duration, clip, effectiveMotion);
        }

        private static EnemyAnimationMigrationBinding Trigger(
            EnemyAnimationCue cue,
            string target,
            float duration = -1f,
            EnemyAnimationMigrationClipIdentity clip = default,
            string replacementStateName = "")
        {
            return new EnemyAnimationMigrationBinding(
                cue, EnemyAnimationDispatchMode.Trigger, target, string.Empty, duration, clip, default, replacementStateName);
        }

        private static EnemyAnimationMigrationClipIdentity Clip(string guid, long localFileId)
        {
            return new EnemyAnimationMigrationClipIdentity(guid, localFileId);
        }

    }

    internal enum EnemyAnimationViewDisposition
    {
        MigratedBinding = 0,
        ApprovedNoBinding = 1,
        Archived = 2,
        Deleted = 3,
        LegacyBlocked = 4,
    }

    internal sealed class EnemyAnimationViewDispositionRow
    {
        internal EnemyAnimationViewDispositionRow(
            string name,
            string prefabPath,
            string prefabGuid,
            bool isProduction,
            EnemyAnimationViewDisposition disposition,
            string retirementReason,
            string replacementName,
            bool expectedAssetExists)
        {
            Name = name ?? string.Empty;
            PrefabPath = prefabPath ?? string.Empty;
            PrefabGuid = prefabGuid ?? string.Empty;
            IsProduction = isProduction;
            Disposition = disposition;
            RetirementReason = retirementReason ?? string.Empty;
            ReplacementName = replacementName ?? string.Empty;
            ExpectedAssetExists = expectedAssetExists;
        }

        internal string Name { get; }
        internal string PrefabPath { get; }
        internal string PrefabGuid { get; }
        internal bool IsProduction { get; }
        internal EnemyAnimationViewDisposition Disposition { get; }
        internal string RetirementReason { get; }
        internal string ReplacementName { get; }
        internal bool ExpectedAssetExists { get; }
    }

    internal static class EnemyAnimationViewDispositionLedger
    {
        internal const int SchemaVersion = 1;
        internal const string DeletedJumpingInactiveMaterialGuid = "1b541690b5140fe4ebe09667ed444a62";
        internal const string DeletedJumpingInactiveMaterialPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/" +
            "Materials/InactiveCompatible/EnemyView_Jumping/" +
            "EnemyView_Jumping_GameEnemyCustomEnemyLitNonAttacking_Inactive.mat";

        private const string ProductionRoot =
            EnemyAnimationBindingMigrationManifest.ProductionPrefabRoot;
        private const string LegacyRuntimeRoot =
            "Assets/_Features/Gameplay/Gameplay_Entities/Runtime";

        private static readonly IReadOnlyList<EnemyAnimationViewDispositionRow> LedgerRows =
            new ReadOnlyCollection<EnemyAnimationViewDispositionRow>(new[]
            {
                Live("BlackEye", "EnemyView_BlackEye.prefab", "d25f546e192650048aeec864272891c2",
                    EnemyAnimationViewDisposition.MigratedBinding),
                Live("Startis", "EnemyView_Startis.prefab", "d2fcbcdf8d3b4dd4b88dd02f4fb843ae",
                    EnemyAnimationViewDisposition.MigratedBinding),
                Live("RocketFace", "EnemyView_RocketFace.prefab", "4b046e9ae49c42b4388ff25a2353c3f7",
                    EnemyAnimationViewDisposition.MigratedBinding),
                Live("Astreton", "EnemyView_Astreton.prefab", "7aae229c82bf72e49c105c163ee2d676",
                    EnemyAnimationViewDisposition.MigratedBinding),
                Live("DrSaturn", "EnemyView_DrSaturn.prefab", "8fa155d3aa7c4ea4bb582c5fb364e801",
                    EnemyAnimationViewDisposition.MigratedBinding),
                Live("JPeter", "EnemyView_JPeter.prefab", "7fa07cb2cf222ca4194569602c56204a",
                    EnemyAnimationViewDisposition.MigratedBinding),
                Live("Sunwheel", "EnemyView_Sunwheel.prefab", "40031cd173e7ca9902ba3c3d971f4239",
                    EnemyAnimationViewDisposition.MigratedBinding),
                Live("Kali", "EnemyView_Kali.prefab", "6eced58e0a1a4a6881cc0c15aa61f402",
                    EnemyAnimationViewDisposition.ApprovedNoBinding),
                Live("SecBot", "EnemyView_SecBot.prefab", "fca7744895a64016aab93a7e38807d41",
                    EnemyAnimationViewDisposition.ApprovedNoBinding),
                Live("Nebulous", "EnemyView_Nebulous.prefab", "18e3b2aebc74a9b43b5f5cda65339d6b",
                    EnemyAnimationViewDisposition.MigratedBinding),
                Deleted("Attacking", LegacyRuntimeRoot + "/EnemyView_Attacking.prefab",
                    "83caa4e85bf10db439b3962f8682e4ce", "BlackEye",
                    "Retired generic test View; production catalog uses the dedicated BlackEye View."),
                Deleted("NonAttacking", LegacyRuntimeRoot + "/EnemyView_NonAttacking.prefab",
                    "46a5570e50d3d91459ff0980ae576e65", "Startis",
                    "Retired generic test View; production catalog uses the dedicated Startis View."),
                Deleted("Jumping", ProductionRoot + "/EnemyView_Jumping.prefab",
                    "6f32c68c11dbede40b0bc721539b2af5", "Astreton",
                    "Retired invalid legacy timing fixture; production catalog uses the dedicated Astreton View."),
                Deleted("PrototypeGravityFieldChaser",
                    ProductionRoot + "/EnemyView_PrototypeGravityFieldChaser.prefab",
                    "63df51ad8ec0533438680ec9b4db5298", "DrSaturn",
                    "Retired non-catalog prototype; production catalog uses the dedicated DrSaturn View."),
            });

        internal static IReadOnlyList<EnemyAnimationViewDispositionRow> Rows => LedgerRows;

        private static EnemyAnimationViewDispositionRow Live(
            string name,
            string prefabFileName,
            string prefabGuid,
            EnemyAnimationViewDisposition disposition)
        {
            return new EnemyAnimationViewDispositionRow(
                name,
                ProductionRoot + "/" + prefabFileName,
                prefabGuid,
                true,
                disposition,
                string.Empty,
                name,
                true);
        }

        private static EnemyAnimationViewDispositionRow Deleted(
            string name,
            string prefabPath,
            string prefabGuid,
            string replacementName,
            string retirementReason)
        {
            return new EnemyAnimationViewDispositionRow(
                name,
                prefabPath,
                prefabGuid,
                false,
                EnemyAnimationViewDisposition.Deleted,
                retirementReason,
                replacementName,
                false);
        }
    }
}
