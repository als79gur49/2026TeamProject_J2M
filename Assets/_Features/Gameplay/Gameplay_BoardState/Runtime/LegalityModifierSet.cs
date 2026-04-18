using System;

namespace Game.Feature.Gameplay.BoardState
{
    [Flags]
    internal enum LegalityModifierId
    {
        None = 0,
        AcceptedDestroyVacatesTarget = 1 << 0,
        ExclusiveLockedPlayerExactStackAllowance = 1 << 1,
        SuppressTargetabilityParticipation = 1 << 2,
    }

    internal readonly struct LegalityModifierSet
    {
        private readonly LegalityModifierId _flags;

        public LegalityModifierSet(LegalityModifierId flags)
        {
            _flags = flags;
        }

        public static LegalityModifierSet None => new(LegalityModifierId.None);

        public bool Has(LegalityModifierId modifier)
        {
            return (_flags & modifier) == modifier;
        }

        public LegalityModifierSet With(LegalityModifierId modifier)
        {
            return new LegalityModifierSet(_flags | modifier);
        }
    }
}
