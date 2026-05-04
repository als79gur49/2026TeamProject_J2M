using System.Collections.Generic;

namespace Game.Feature.Gameplay.Vfx.Authoring
{
    public sealed class GameplayVfxBindingCompositionResult
    {
        public GameplayVfxBindingCompositionResult(
            CompositeVfxBindingResolver resolver,
            VfxCueMap hostDefaultMap,
            IReadOnlyDictionary<GameplayVfxFamily, VfxProfile> familyProfiles,
            VfxAuthoringValidationResult validation)
        {
            Validation = validation ?? VfxAuthoringValidationResult.Success;
            Succeeded = !Validation.HasErrors;
            Resolver = Succeeded ? resolver : null;
            HostDefaultMap = Succeeded ? hostDefaultMap : null;
            FamilyProfiles = Succeeded
                ? familyProfiles ?? new Dictionary<GameplayVfxFamily, VfxProfile>()
                : new Dictionary<GameplayVfxFamily, VfxProfile>();
        }

        public bool Succeeded { get; }

        public CompositeVfxBindingResolver Resolver { get; }

        public VfxCueMap HostDefaultMap { get; }

        public IReadOnlyDictionary<GameplayVfxFamily, VfxProfile> FamilyProfiles { get; }

        public VfxAuthoringValidationResult Validation { get; }

        public void ThrowIfErrors()
        {
            Validation.ThrowIfErrors();
        }
    }
}
