using System;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Authoring;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public enum EnemyPresentationVfxProfileStatusKind
    {
        HostDefaultFallback = 0,
        Valid = 1,
        Invalid = 2,
        WrongFamily = 3,
    }

    public readonly struct EnemyPresentationVfxProfileStatus
    {
        public EnemyPresentationVfxProfileStatus(
            VfxProfileAsset profileAsset,
            EnemyPresentationVfxProfileStatusKind kind,
            GameplayVfxFamily family,
            VfxAuthoringValidationResult validation)
        {
            ProfileAsset = profileAsset;
            Kind = kind;
            Family = family;
            Validation = validation ?? VfxAuthoringValidationResult.Success;
            Diagnostics = BuildDiagnostics(Validation);
            ProfileName = profileAsset != null ? profileAsset.name : string.Empty;
            FamilyName = family.ToString();
        }

        public VfxProfileAsset ProfileAsset { get; }

        public EnemyPresentationVfxProfileStatusKind Kind { get; }

        public GameplayVfxFamily Family { get; }

        public VfxAuthoringValidationResult Validation { get; }

        public EnemyPresentationVfxProfileDiagnostic[] Diagnostics { get; }

        public string ProfileName { get; }

        public string FamilyName { get; }

        public bool Assigned => ProfileAsset != null;

        public bool HasErrors => Kind == EnemyPresentationVfxProfileStatusKind.WrongFamily || Validation.HasErrors;

        public bool HasWarnings => Validation.HasWarnings;

        private static EnemyPresentationVfxProfileDiagnostic[] BuildDiagnostics(
            VfxAuthoringValidationResult validation)
        {
            var messages = validation?.Messages;
            if (messages == null || messages.Count == 0)
            {
                return Array.Empty<EnemyPresentationVfxProfileDiagnostic>();
            }

            var diagnostics = new EnemyPresentationVfxProfileDiagnostic[messages.Count];
            for (var i = 0; i < messages.Count; i++)
            {
                var message = messages[i];
                diagnostics[i] = new EnemyPresentationVfxProfileDiagnostic(
                    message.Severity == VfxAuthoringValidationSeverity.Error
                        ? EnemyPresentationVfxProfileDiagnosticSeverity.Error
                        : message.Severity == VfxAuthoringValidationSeverity.Warning
                            ? EnemyPresentationVfxProfileDiagnosticSeverity.Warning
                            : EnemyPresentationVfxProfileDiagnosticSeverity.Info,
                    message.Code,
                    message.Message);
            }

            return diagnostics;
        }
    }

    public enum EnemyPresentationVfxProfileDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    public readonly struct EnemyPresentationVfxProfileDiagnostic
    {
        public EnemyPresentationVfxProfileDiagnostic(
            EnemyPresentationVfxProfileDiagnosticSeverity severity,
            string code,
            string message)
        {
            Severity = severity;
            Code = string.IsNullOrWhiteSpace(code) ? "VFX_AUTHORING" : code;
            Message = message ?? string.Empty;
        }

        public EnemyPresentationVfxProfileDiagnosticSeverity Severity { get; }

        public string Code { get; }

        public string Message { get; }
    }

    public static class EnemyPresentationVfxProfileStatusResolver
    {
        public static EnemyPresentationVfxProfileStatus Resolve(in EnemyPresentationCatalogEntry entry)
        {
            return Resolve(entry.VfxProfileAsset);
        }

        public static EnemyPresentationVfxProfileStatus Resolve(VfxProfileAsset profileAsset)
        {
            if (profileAsset == null)
            {
                return new EnemyPresentationVfxProfileStatus(
                    null,
                    EnemyPresentationVfxProfileStatusKind.HostDefaultFallback,
                    GameplayVfxFamily.None,
                    VfxAuthoringValidationResult.Success);
            }

            var family = profileAsset.Family;
            if (family != GameplayVfxFamily.Enemy)
            {
                return new EnemyPresentationVfxProfileStatus(
                    profileAsset,
                    EnemyPresentationVfxProfileStatusKind.WrongFamily,
                    family,
                    VfxAuthoringValidationResult.Success);
            }

            var validation = profileAsset.ValidateAuthoring();
            return new EnemyPresentationVfxProfileStatus(
                profileAsset,
                validation.HasErrors
                    ? EnemyPresentationVfxProfileStatusKind.Invalid
                    : EnemyPresentationVfxProfileStatusKind.Valid,
                family,
                validation);
        }

        public static string ToPreviewLabel(in EnemyPresentationVfxProfileStatus status)
        {
            return status.Kind switch
            {
                EnemyPresentationVfxProfileStatusKind.HostDefaultFallback => "None / Host default fallback",
                EnemyPresentationVfxProfileStatusKind.Valid => "Assigned / Valid",
                EnemyPresentationVfxProfileStatusKind.Invalid => "Assigned / Invalid",
                EnemyPresentationVfxProfileStatusKind.WrongFamily => "Wrong family",
                _ => throw new ArgumentOutOfRangeException(nameof(status)),
            };
        }
    }
}
