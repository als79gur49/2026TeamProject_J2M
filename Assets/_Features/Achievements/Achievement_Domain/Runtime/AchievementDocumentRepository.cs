namespace Game.Product.Achievements
{
    public enum AchievementDocumentLoadStatus
    {
        Missing = 0,
        Loaded = 1,
        BackupRecovered = 2,
        SchemaInvalid = 3,
        UnsupportedVersion = 4,
        CorruptQuarantined = 5,
        CorruptNoFallback = 6,
        IoFailed = 7,
        Unauthorized = 8,
    }

    public readonly struct AchievementDocumentLoadResult
    {
        public AchievementDocumentLoadResult(
            AchievementDocumentLoadStatus status,
            ProductAchievementDocument document,
            string message)
        {
            Status = status;
            Document = document;
            Message = message ?? string.Empty;
        }

        public AchievementDocumentLoadStatus Status { get; }

        public ProductAchievementDocument Document { get; }

        public string Message { get; }

        public bool IsUsable =>
            Document != null &&
            (Status == AchievementDocumentLoadStatus.Missing ||
             Status == AchievementDocumentLoadStatus.Loaded ||
             Status == AchievementDocumentLoadStatus.BackupRecovered);
    }

    public enum AchievementDocumentSaveStatus
    {
        Saved = 0,
        SchemaInvalid = 1,
        UnsupportedVersion = 2,
        IoFailed = 3,
        Unauthorized = 4,
        Failed = 5,
    }

    public readonly struct AchievementDocumentSaveResult
    {
        public AchievementDocumentSaveResult(AchievementDocumentSaveStatus status, string message)
        {
            Status = status;
            Message = message ?? string.Empty;
        }

        public AchievementDocumentSaveStatus Status { get; }

        public string Message { get; }

        public bool IsSuccess => Status == AchievementDocumentSaveStatus.Saved;

        public static AchievementDocumentSaveResult Saved()
        {
            return new AchievementDocumentSaveResult(AchievementDocumentSaveStatus.Saved, string.Empty);
        }
    }

    public interface IAchievementDocumentRepository
    {
        AchievementDocumentLoadResult Load();

        AchievementDocumentSaveResult Save(ProductAchievementDocument document);
    }
}
