using System;

namespace Game.Feature.Gameplay.BoardState
{
    [Flags]
    internal enum LegalityCapabilityId
    {
        None = 0,
        IgnoreTraversalUnitBlocker = 1 << 0,
        IgnoreTraversalSolidBlocker = 1 << 1,
    }

    internal readonly struct LegalityCapabilitySet
    {
        private readonly LegalityCapabilityId _flags;

        public LegalityCapabilitySet(LegalityCapabilityId flags)
        {
            _flags = flags;
        }

        public static LegalityCapabilitySet None => new(LegalityCapabilityId.None);

        public bool Has(LegalityCapabilityId capability)
        {
            return (_flags & capability) == capability;
        }

        public LegalityCapabilitySet With(LegalityCapabilityId capability)
        {
            return new LegalityCapabilitySet(_flags | capability);
        }
    }
}
