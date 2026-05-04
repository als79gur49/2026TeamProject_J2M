namespace Game.Feature.Gameplay.Vfx.Authoring
{
    public readonly struct GameplayVfxBindingCompositionOptions
    {
        private readonly bool initialized;
        private readonly bool allowMissingHostDefaultMap;
        private readonly bool allowNullProfileEntries;
        private readonly bool allowDuplicateProfileFamilies;

        public GameplayVfxBindingCompositionOptions(
            bool allowMissingHostDefaultMap = true,
            bool allowNullProfileEntries = false,
            bool allowDuplicateProfileFamilies = false)
        {
            initialized = true;
            this.allowMissingHostDefaultMap = allowMissingHostDefaultMap;
            this.allowNullProfileEntries = allowNullProfileEntries;
            this.allowDuplicateProfileFamilies = allowDuplicateProfileFamilies;
        }

        public bool AllowMissingHostDefaultMap => !initialized || allowMissingHostDefaultMap;

        public bool AllowNullProfileEntries => initialized && allowNullProfileEntries;

        public bool AllowDuplicateProfileFamilies => initialized && allowDuplicateProfileFamilies;
    }
}
