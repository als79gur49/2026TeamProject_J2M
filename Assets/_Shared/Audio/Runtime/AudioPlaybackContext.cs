namespace Game.Shared.Audio
{
    public readonly struct AudioPlaybackContext
    {
        public AudioPlaybackContext(
            float volumeMultiplier = 1f,
            float pitchMultiplier = 1f,
            int? ownerEntityId = null,
            string debugTag = null)
        {
            VolumeMultiplier = volumeMultiplier;
            PitchMultiplier = pitchMultiplier;
            OwnerEntityId = ownerEntityId;
            DebugTag = debugTag ?? string.Empty;
        }

        public float VolumeMultiplier { get; }

        public float PitchMultiplier { get; }

        public int? OwnerEntityId { get; }

        public string DebugTag { get; }
    }
}
