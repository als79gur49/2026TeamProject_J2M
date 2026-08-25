using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Feature.Stages
{
    public enum CampaignSaveCommandStatus
    {
        Succeeded = 0,
        InvalidSlotNumber = 1,
        SlotNotFound = 2,
        InvalidRequest = 3,
        LoadFailed = 4,
        SaveFailed = 5,
        StalePrecondition = 6,
    }

    public sealed class CampaignSaveServiceResult
    {
        public CampaignSaveServiceResult(
            CampaignSaveCommandStatus status,
            CampaignProfileDocument document,
            CampaignSlotDocument slot,
            CampaignSlotDocument[] slots,
            string message,
            int? previousRemainingChances = null,
            bool hasProfileLoadStatus = false,
            CampaignProfileLoadStatus profileLoadStatus = CampaignProfileLoadStatus.Missing)
        {
            Status = status;
            Document = document;
            Slot = slot;
            Slots = slots ?? Array.Empty<CampaignSlotDocument>();
            Message = message ?? string.Empty;
            PreviousRemainingChances = previousRemainingChances.HasValue
                ? CampaignSaveSlotPolicy.RequireValidRemainingChances(
                    previousRemainingChances.Value)
                : null;
            HasProfileLoadStatus = hasProfileLoadStatus;
            ProfileLoadStatus = profileLoadStatus;
        }

        public CampaignSaveCommandStatus Status { get; }

        public bool Succeeded => Status == CampaignSaveCommandStatus.Succeeded;

        public CampaignProfileDocument Document { get; }

        public CampaignSlotDocument Slot { get; }

        public CampaignSlotDocument[] Slots { get; }

        public string Message { get; }

        public int? PreviousRemainingChances { get; }

        public bool HasProfileLoadStatus { get; }

        public CampaignProfileLoadStatus ProfileLoadStatus { get; }

        public static CampaignSaveServiceResult Success(
            CampaignProfileDocument document,
            CampaignSlotDocument slot = null,
            string message = "",
            int? previousRemainingChances = null,
            bool hasProfileLoadStatus = false,
            CampaignProfileLoadStatus profileLoadStatus = CampaignProfileLoadStatus.Missing)
        {
            return new CampaignSaveServiceResult(
                CampaignSaveCommandStatus.Succeeded,
                document,
                slot,
                document?.Slots,
                message,
                previousRemainingChances,
                hasProfileLoadStatus,
                profileLoadStatus);
        }

        public static CampaignSaveServiceResult Failure(
            CampaignSaveCommandStatus status,
            string message,
            CampaignProfileDocument document = null,
            bool hasProfileLoadStatus = false,
            CampaignProfileLoadStatus profileLoadStatus = CampaignProfileLoadStatus.Missing)
        {
            return new CampaignSaveServiceResult(
                status,
                document,
                null,
                document?.Slots,
                message,
                null,
                hasProfileLoadStatus,
                profileLoadStatus);
        }
    }

    public sealed class CampaignNewGameRequest
    {
        public int SlotNumber { get; set; }

        public string InitialStageId { get; set; }

        public string InitialLevelGroupId { get; set; }

        public string LastPlayedAtUtc { get; set; }
    }

    public sealed class CampaignSaveService
    {
        private const int SchemaVersion = CampaignProfileDocument.CurrentSchemaVersion;

        private readonly ICampaignProfileRepository _repository;
        private readonly Func<string> _utcNowProvider;
        private readonly string _profileId;
        private readonly string _productVersion;
        private CampaignProfileLoadStatus _lastProfileLoadStatus = CampaignProfileLoadStatus.Missing;

        public CampaignSaveService(
            ICampaignProfileRepository repository,
            Func<string> utcNowProvider,
            string profileId,
            string productVersion)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _utcNowProvider = utcNowProvider ?? DefaultUtcNow;
            _profileId = string.IsNullOrWhiteSpace(profileId) ? "campaign-profile" : profileId;
            _productVersion = productVersion ?? string.Empty;
        }

        public CampaignSaveService(
            ICampaignProfileRepository repository,
            Func<string> utcNowProvider = null)
            : this(repository, utcNowProvider, "campaign-profile", string.Empty)
        {
        }

        public CampaignSaveServiceResult LoadProfile()
        {
            if (!TryLoadProfile(out var document, out var failure, allowMissing: true))
            {
                return failure;
            }

            return CampaignSaveServiceResult.Success(
                document,
                message: "Campaign profile loaded.",
                hasProfileLoadStatus: true,
                profileLoadStatus: _lastProfileLoadStatus);
        }

        public CampaignSaveServiceResult GetSlots()
        {
            if (!TryLoadProfile(out var document, out var failure, allowMissing: true))
            {
                return failure;
            }

            return CampaignSaveServiceResult.Success(
                document,
                message: "Campaign slots loaded.",
                hasProfileLoadStatus: true,
                profileLoadStatus: _lastProfileLoadStatus);
        }

        public CampaignSaveServiceResult GetSlot(int slotNumber)
        {
            if (!CampaignSaveSlotPolicy.IsValidSlotNumber(slotNumber))
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidSlotNumber,
                    "Save slot number must be 1, 2, or 3.");
            }

            if (!TryLoadProfile(out var document, out var failure, allowMissing: true))
            {
                return failure;
            }

            return TryFindSlot(document, slotNumber, out var slot)
                ? CampaignSaveServiceResult.Success(document, CloneSlot(slot), message: "Campaign slot loaded.")
                : CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.SlotNotFound,
                    "Campaign slot does not exist.",
                    document);
        }

        public CampaignSaveServiceResult InitializeNewGame(
            int slotNumber,
            string initialStageId,
            string initialLevelGroupId)
        {
            return InitializeNewGame(new CampaignNewGameRequest
            {
                SlotNumber = slotNumber,
                InitialStageId = initialStageId,
                InitialLevelGroupId = initialLevelGroupId,
            });
        }

        public CampaignSaveServiceResult InitializeNewGame(CampaignNewGameRequest request)
        {
            if (request == null)
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidRequest,
                    "New game request must not be null.");
            }

            if (!CampaignSaveSlotPolicy.IsValidSlotNumber(request.SlotNumber))
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidSlotNumber,
                    "Save slot number must be 1, 2, or 3.");
            }

            if (string.IsNullOrWhiteSpace(request.InitialStageId))
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidRequest,
                    "Initial stage id must not be empty.");
            }

            if (!StageId.TryCreate(request.InitialStageId, out var initialStageId))
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidRequest,
                    "Initial stage id must be canonical.");
            }

            return Mutate(document =>
            {
                var now = Now();
                var lastPlayedAtUtc = string.IsNullOrWhiteSpace(request.LastPlayedAtUtc)
                    ? now
                    : request.LastPlayedAtUtc;
                var state = CampaignSlotStateFactory.CreateNewGame(
                    request.SlotNumber,
                    initialStageId,
                    request.InitialLevelGroupId,
                    lastPlayedAtUtc);
                var slot = CampaignSlotStateDocumentMapper.ToDocument(state);

                UpsertSlot(document, slot);
                document.LastPlayedSlotNumber = request.SlotNumber;
                TouchProfile(document, now);
                return CampaignSaveServiceResult.Success(
                    document,
                    CloneSlot(slot),
                    message: "New campaign slot initialized.");
            }, destructive: true);
        }

        public CampaignSaveServiceResult ImportSlotSeed(
            CampaignSlotSeedImportRequest request)
        {
            if (request == null)
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidRequest,
                    "Slot seed import request must not be null.");
            }

            return Mutate(document =>
            {
                var now = Now();
                var state = CampaignSlotStateFactory.CreateImportedSeed(request);
                if (string.IsNullOrWhiteSpace(state.LastPlayedAt))
                {
                    state = CampaignSlotStateFactory.WithLastPlayedAt(state, now);
                }

                var slot = CampaignSlotStateDocumentMapper.ToDocument(state);
                UpsertSlot(document, slot);
                TouchProfile(document, now);
                return CampaignSaveServiceResult.Success(
                    document,
                    CloneSlot(slot),
                    message: "Campaign slot seed imported.");
            });
        }

        public CampaignSaveServiceResult PrepareContinue(
            CampaignContinuePreparationCommand command)
        {
            if (command == null)
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidRequest,
                    "Continue preparation command must not be null.");
            }

            if (!TryLoadProfile(out var document, out var failure, allowMissing: true))
            {
                return failure;
            }

            if (!TryFindSlot(document, command.SlotNumber, out var slot))
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.SlotNotFound,
                    "Campaign slot does not exist.",
                    document);
            }

            var parsed = CampaignSlotParser.ParseEntry(command.SlotNumber, slot);
            if (!parsed.IsSuccess || parsed.Entry.IsEmpty)
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidRequest,
                    "Campaign slot does not satisfy the current runtime slot contract.",
                    document);
            }

            var preparation = CampaignContinuePreparationPolicy.Evaluate(
                parsed.Entry.State,
                command);
            if (!preparation.Succeeded)
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.StalePrecondition,
                    "Campaign slot changed after Continue was evaluated.",
                    document);
            }

            if (!preparation.LevelGroupSynchronized)
            {
                return CampaignSaveServiceResult.Success(
                    document,
                    CloneSlot(slot),
                    message: "Campaign Continue state verified.");
            }

            var replacement = CampaignSlotStateDocumentMapper.ToDocument(
                preparation.CommittedState);
            UpsertSlot(document, replacement);
            TouchProfile(document, Now());
            return Persist(
                document,
                CampaignSaveServiceResult.Success(
                    document,
                    CloneSlot(replacement),
                    message: "Campaign Continue state prepared."),
                destructive: false);
        }

        public CampaignSaveServiceResult DeleteSlot(int slotNumber)
        {
            if (!CampaignSaveSlotPolicy.IsValidSlotNumber(slotNumber))
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidSlotNumber,
                    "Save slot number must be 1, 2, or 3.");
            }

            return Mutate(document =>
            {
                if (!TryFindSlot(document, slotNumber, out _))
                {
                    return CampaignSaveServiceResult.Failure(
                        CampaignSaveCommandStatus.SlotNotFound,
                        "Campaign slot does not exist.",
                        document);
                }

                RemoveSlot(document, slotNumber);
                if (document.LastPlayedSlotNumber == slotNumber)
                {
                    document.LastPlayedSlotNumber = FindFirstSlotNumber(document);
                }

                TouchProfile(document, Now());
                return CampaignSaveServiceResult.Success(document, message: "Campaign slot deleted.");
            }, destructive: true);
        }

        public CampaignSaveServiceResult ClearAll()
        {
            return Mutate(document =>
            {
                var now = Now();
                document.SchemaVersion = SchemaVersion;
                document.ProductVersion = _productVersion;
                document.ProfileId = _profileId;
                document.Slots = Array.Empty<CampaignSlotDocument>();
                document.LastPlayedSlotNumber = 0;
                TouchProfile(document, now);
                return CampaignSaveServiceResult.Success(document, message: "Campaign profile cleared.");
            }, destructive: true);
        }

        public CampaignSaveServiceResult CommitDeath(
            int slotNumber,
            CampaignDeathTransitionPlan plan)
        {
            if (!CampaignSaveSlotPolicy.IsValidSlotNumber(slotNumber))
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidSlotNumber,
                    "Save slot number must be 1, 2, or 3.");
            }

            return Mutate(document =>
            {
                if (!TryFindSlot(document, slotNumber, out var slot))
                {
                    return CampaignSaveServiceResult.Failure(
                        CampaignSaveCommandStatus.SlotNotFound,
                        "Campaign slot does not exist.",
                        document);
                }

                var now = Now();
                var parsed = CampaignSlotParser.ParseEntry(slotNumber, slot);
                if (!parsed.IsSuccess || parsed.Entry.IsEmpty)
                {
                    return CampaignSaveServiceResult.Failure(
                        CampaignSaveCommandStatus.InvalidRequest,
                        "Campaign slot does not satisfy the current runtime slot contract.",
                        document);
                }

                var transition = CampaignSlotTransitionEngine.ApplyDeath(
                    parsed.Entry.State,
                    plan,
                    now);
                if (!transition.Succeeded)
                {
                    return MapDeathTransitionFailure(transition, document);
                }

                var replacement = CampaignSlotStateDocumentMapper.ToDocument(
                    transition.Slot);
                UpsertSlot(document, replacement);
                TouchProfile(document, now);
                return CampaignSaveServiceResult.Success(
                    document,
                    CloneSlot(replacement),
                    message: "Campaign death transition committed.");
            });
        }

        public CampaignSaveServiceResult CommitStageClear(
            int slotNumber,
            CampaignStageClearCommitRequest request)
        {
            if (!CampaignSaveSlotPolicy.IsValidSlotNumber(slotNumber))
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidSlotNumber,
                    "Save slot number must be 1, 2, or 3.");
            }

            return Mutate(document =>
            {
                if (!TryFindSlot(document, slotNumber, out var slot))
                {
                    return CampaignSaveServiceResult.Failure(
                        CampaignSaveCommandStatus.SlotNotFound,
                        "Campaign slot does not exist.",
                        document);
                }

                var now = Now();
                var parsed = CampaignSlotParser.ParseEntry(slotNumber, slot);
                if (!parsed.IsSuccess || parsed.Entry.IsEmpty)
                {
                    return CampaignSaveServiceResult.Failure(
                        CampaignSaveCommandStatus.InvalidRequest,
                        "Campaign slot does not satisfy the current runtime slot contract.",
                        document);
                }

                var transition = CampaignSlotTransitionEngine.ApplyStageClear(
                    parsed.Entry.State,
                    request,
                    now);
                if (!transition.Succeeded)
                {
                    return MapStageClearTransitionFailure(transition, document);
                }

                var replacement = CampaignSlotStateDocumentMapper.ToDocument(
                    transition.Slot);
                UpsertSlot(document, replacement);
                TouchProfile(document, now);
                return CampaignSaveServiceResult.Success(
                    document,
                    CloneSlot(replacement),
                    message: "Campaign stage clear transition committed.",
                    previousRemainingChances: transition.PreviousRemainingChances);
            });
        }

        public CampaignSaveServiceResult SetIntroComicCompleted(int slotNumber)
        {
            return SetComicCompletion(
                slotNumber,
                new CampaignComicCompletionCommand(CampaignComicCompletionKind.Intro));
        }

        public CampaignSaveServiceResult SetOutroComicCompleted(int slotNumber)
        {
            return SetComicCompletion(
                slotNumber,
                new CampaignComicCompletionCommand(CampaignComicCompletionKind.Outro));
        }

        private CampaignSaveServiceResult SetComicCompletion(
            int slotNumber,
            CampaignComicCompletionCommand command)
        {
            if (!CampaignSaveSlotPolicy.IsValidSlotNumber(slotNumber))
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidSlotNumber,
                    "Save slot number must be 1, 2, or 3.");
            }

            return Mutate(document =>
            {
                if (!TryFindSlot(document, slotNumber, out var slot))
                {
                    return CampaignSaveServiceResult.Failure(
                        CampaignSaveCommandStatus.SlotNotFound,
                        "Campaign slot does not exist.",
                        document);
                }

                var now = Now();
                var parsed = CampaignSlotParser.ParseEntry(slotNumber, slot);
                if (!parsed.IsSuccess || parsed.Entry.IsEmpty)
                {
                    return CampaignSaveServiceResult.Failure(
                        CampaignSaveCommandStatus.InvalidRequest,
                        "Campaign slot does not satisfy the current runtime slot contract.",
                        document);
                }

                var transition = CampaignSlotTransitionEngine.ApplyComicCompletion(
                    parsed.Entry.State,
                    command,
                    now);
                if (!transition.Succeeded)
                {
                    return MapComicTransitionFailure(transition, document);
                }

                var replacement = CampaignSlotStateDocumentMapper.ToDocument(
                    transition.Slot);
                UpsertSlot(document, replacement);
                TouchProfile(document, now);
                return CampaignSaveServiceResult.Success(
                    document,
                    CloneSlot(replacement),
                    message: command.CompletionKind == CampaignComicCompletionKind.Intro
                        ? "Intro comic completion saved."
                        : "Outro comic completion saved.");
            });
        }

        public CampaignSaveServiceResult SetActiveStageForDiagnostics(
            int slotNumber,
            StageId stageId,
            string levelGroupId)
        {
            if (!CampaignSaveSlotPolicy.IsValidSlotNumber(slotNumber))
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidSlotNumber,
                    "Save slot number must be 1, 2, or 3.");
            }

            if (!stageId.IsValid)
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidRequest,
                    "Diagnostic stage selection requires a valid stage id.");
            }

            return Mutate(document =>
            {
                if (!TryFindSlot(document, slotNumber, out var slot))
                {
                    return CampaignSaveServiceResult.Failure(
                        CampaignSaveCommandStatus.SlotNotFound,
                        "Campaign slot does not exist.",
                        document);
                }

                var parsed = CampaignSlotParser.ParseEntry(slotNumber, slot);
                if (!parsed.IsSuccess || parsed.Entry.IsEmpty)
                {
                    return CampaignSaveServiceResult.Failure(
                        CampaignSaveCommandStatus.InvalidRequest,
                        "Campaign slot does not satisfy the current runtime slot contract.",
                        document);
                }

                var now = Now();
                var state = CampaignSlotStateFactory.SelectStageForDiagnostics(
                    parsed.Entry.State,
                    stageId,
                    levelGroupId,
                    now);
                var replacement = CampaignSlotStateDocumentMapper.ToDocument(state);
                UpsertSlot(document, replacement);
                TouchProfile(document, now);
                return CampaignSaveServiceResult.Success(
                    document,
                    CloneSlot(replacement),
                    message: "Campaign diagnostic stage selected.");
            });
        }

        private static CampaignSaveServiceResult MapDeathTransitionFailure(
            CampaignSlotTransitionResult transition,
            CampaignProfileDocument document)
        {
            if (transition.ReasonCode ==
                CampaignSlotTransitionReasonCode.DeathCounterOverflow)
            {
                throw new ArgumentException(
                    "Campaign slot replacement does not satisfy the current runtime slot contract.",
                    "slot");
            }

            if (transition.FailureKind == CampaignSlotTransitionFailureKind.InvalidPlan)
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidRequest,
                    "Death transition plan is invalid.",
                    document);
            }

            if (transition.FailureKind ==
                CampaignSlotTransitionFailureKind.StalePrecondition)
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidRequest,
                    "Campaign slot changed after the death transition was planned.",
                    document);
            }

            return CampaignSaveServiceResult.Failure(
                CampaignSaveCommandStatus.InvalidRequest,
                "Campaign slot does not satisfy the current runtime slot contract.",
                document);
        }

        private static CampaignSaveServiceResult MapStageClearTransitionFailure(
            CampaignSlotTransitionResult transition,
            CampaignProfileDocument document)
        {
            if (transition.FailureKind == CampaignSlotTransitionFailureKind.InvalidPlan)
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidRequest,
                    "Stage clear commit request is invalid.",
                    document);
            }

            if (transition.FailureKind ==
                CampaignSlotTransitionFailureKind.StalePrecondition)
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.InvalidRequest,
                    "Campaign slot changed after the stage clear transition was planned.",
                    document);
            }

            return CampaignSaveServiceResult.Failure(
                CampaignSaveCommandStatus.InvalidRequest,
                "Campaign slot does not satisfy the current runtime slot contract.",
                document);
        }

        private static CampaignSaveServiceResult MapComicTransitionFailure(
            CampaignSlotTransitionResult transition,
            CampaignProfileDocument document)
        {
            return CampaignSaveServiceResult.Failure(
                CampaignSaveCommandStatus.InvalidRequest,
                transition.FailureKind == CampaignSlotTransitionFailureKind.InvalidPlan
                    ? "Comic completion command is invalid."
                    : "Campaign slot does not satisfy the current runtime slot contract.",
                document);
        }

        private CampaignSaveServiceResult Mutate(
            Func<CampaignProfileDocument, CampaignSaveServiceResult> mutation,
            bool destructive = false)
        {
            if (!TryLoadProfile(out var document, out var failure, allowMissing: true))
            {
                return failure;
            }

            var result = mutation(document);
            if (!result.Succeeded)
            {
                return result;
            }

            return Persist(document, result, destructive);
        }

        private CampaignSaveServiceResult Persist(
            CampaignProfileDocument document,
            CampaignSaveServiceResult result,
            bool destructive)
        {
            try
            {
                if (destructive)
                {
                    _repository.SaveDestructive(document);
                }
                else
                {
                    _repository.Save(document);
                }
            }
            catch (Exception exception)
            {
                return CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.SaveFailed,
                    exception.Message,
                    document);
            }

            return result;
        }

        private bool TryLoadProfile(
            out CampaignProfileDocument document,
            out CampaignSaveServiceResult failure,
            bool allowMissing)
        {
            document = null;
            failure = null;
            CampaignProfileLoadResult loadResult;
            try
            {
                loadResult = _repository.Load();
            }
            catch (Exception exception)
            {
                failure = CampaignSaveServiceResult.Failure(
                    CampaignSaveCommandStatus.LoadFailed,
                    exception.Message);
                return false;
            }

            if (loadResult.Status == CampaignProfileLoadStatus.Loaded ||
                loadResult.Status == CampaignProfileLoadStatus.BackupRecovered)
            {
                var validation = CampaignProfileDocumentValidator.Validate(
                    loadResult.Document);
                if (validation != CampaignProfileDocumentValidationResult.Valid)
                {
                    _lastProfileLoadStatus = validation ==
                        CampaignProfileDocumentValidationResult.UnsupportedVersion
                            ? CampaignProfileLoadStatus.UnsupportedVersion
                            : CampaignProfileLoadStatus.InvalidDocument;
                    failure = CampaignSaveServiceResult.Failure(
                        CampaignSaveCommandStatus.LoadFailed,
                        "Campaign profile repository returned a loaded document that failed current validation.",
                        hasProfileLoadStatus: true,
                        profileLoadStatus: _lastProfileLoadStatus);
                    return false;
                }

                _lastProfileLoadStatus = loadResult.Status;
                document = CloneProfile(loadResult.Document);
                return true;
            }

            if (allowMissing && loadResult.Status == CampaignProfileLoadStatus.Missing)
            {
                _lastProfileLoadStatus = loadResult.Status;
                document = CreateEmptyProfile();
                return true;
            }

            _lastProfileLoadStatus = loadResult.Status;
            failure = CampaignSaveServiceResult.Failure(
                CampaignSaveCommandStatus.LoadFailed,
                loadResult.Message,
                hasProfileLoadStatus: true,
                profileLoadStatus: loadResult.Status);
            return false;
        }

        private CampaignProfileDocument CreateEmptyProfile()
        {
            return new CampaignProfileDocument
            {
                SchemaVersion = SchemaVersion,
                ProductVersion = _productVersion,
                SavedAtUtc = string.Empty,
                ProfileId = _profileId,
                LastPlayedSlotNumber = 0,
                Slots = Array.Empty<CampaignSlotDocument>(),
            };
        }

        private static bool TryFindSlot(
            CampaignProfileDocument document,
            int slotNumber,
            out CampaignSlotDocument slot)
        {
            slot = null;
            var slots = document?.Slots ?? Array.Empty<CampaignSlotDocument>();
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].SlotNumber == slotNumber)
                {
                    slot = slots[i];
                    return true;
                }
            }

            return false;
        }

        private static void UpsertSlot(CampaignProfileDocument document, CampaignSlotDocument slot)
        {
            RemoveSlot(document, slot.SlotNumber);
            var slots = new List<CampaignSlotDocument>(document.Slots ?? Array.Empty<CampaignSlotDocument>())
            {
                slot,
            };
            slots.Sort((left, right) => left.SlotNumber.CompareTo(right.SlotNumber));
            document.Slots = slots.ToArray();
        }

        private static void RemoveSlot(CampaignProfileDocument document, int slotNumber)
        {
            var slots = new List<CampaignSlotDocument>();
            var existing = document.Slots ?? Array.Empty<CampaignSlotDocument>();
            for (var i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null && existing[i].SlotNumber != slotNumber)
                {
                    slots.Add(existing[i]);
                }
            }

            document.Slots = slots.ToArray();
        }

        private static int FindFirstSlotNumber(CampaignProfileDocument document)
        {
            var slots = document.Slots ?? Array.Empty<CampaignSlotDocument>();
            Array.Sort(slots, (left, right) => left.SlotNumber.CompareTo(right.SlotNumber));
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && CampaignSaveSlotPolicy.IsValidSlotNumber(slots[i].SlotNumber))
                {
                    return slots[i].SlotNumber;
                }
            }

            return 0;
        }

        private static void TouchProfile(CampaignProfileDocument document, string now)
        {
            document.SavedAtUtc = now ?? string.Empty;
        }

        private string Now()
        {
            return _utcNowProvider();
        }

        private static string DefaultUtcNow()
        {
            return DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        }

        private static CampaignProfileDocument CloneProfile(CampaignProfileDocument document)
        {
            if (document == null)
            {
                return null;
            }

            CampaignSlotDocument[] clonedSlots = null;
            if (document.Slots != null)
            {
                clonedSlots = new CampaignSlotDocument[document.Slots.Length];
                for (var i = 0; i < document.Slots.Length; i++)
                {
                    clonedSlots[i] = CloneSlot(document.Slots[i]);
                }
            }

            return new CampaignProfileDocument
            {
                SchemaVersion = document.SchemaVersion,
                ProductVersion = document.ProductVersion,
                SavedAtUtc = document.SavedAtUtc,
                ProfileId = document.ProfileId,
                LastPlayedSlotNumber = document.LastPlayedSlotNumber,
                Slots = clonedSlots,
            };
        }

        private static CampaignSlotDocument CloneSlot(CampaignSlotDocument slot)
        {
            if (slot == null)
            {
                return null;
            }

            return new CampaignSlotDocument
            {
                SlotNumber = slot.SlotNumber,
                StageId = slot.StageId,
                LevelGroupId = slot.LevelGroupId,
                RemainingChances = slot.RemainingChances,
                CampaignCompleted = slot.CampaignCompleted,
                HasNormalCampaignCompletionReceipt =
                    slot.HasNormalCampaignCompletionReceipt,
                NormalCampaignCompletionReceipt = CloneReceipt(
                    slot.NormalCampaignCompletionReceipt),
                IntroComicCompleted = slot.IntroComicCompleted,
                OutroComicCompleted = slot.OutroComicCompleted,
                NormalStagePerformanceRecords = ClonePerformanceRecords(
                    slot.NormalStagePerformanceRecords),
                TotalDeaths = slot.TotalDeaths,
                LastPlayedAtUtc = slot.LastPlayedAtUtc,
                StageClearProfileSnapshot = CloneStageClearProfile(slot.StageClearProfileSnapshot),
            };
        }

        private static CampaignStageClearProfileDocument CloneStageClearProfile(
            CampaignStageClearProfileDocument profile)
        {
            if (profile == null)
            {
                return null;
            }

            PlayerStageClearRecordDocument[] clonedRecords = null;
            if (profile.Records != null)
            {
                clonedRecords = new PlayerStageClearRecordDocument[profile.Records.Length];
                for (var i = 0; i < profile.Records.Length; i++)
                {
                    clonedRecords[i] = CloneRecord(profile.Records[i]);
                }
            }

            return new CampaignStageClearProfileDocument
            {
                Version = profile.Version,
                Records = clonedRecords,
                ProcessedStageRunIds = CloneArray(profile.ProcessedStageRunIds),
                ProcessedClearAttemptIds = CloneArray(profile.ProcessedClearAttemptIds),
            };
        }

        private static NormalCampaignCompletionReceiptDocument CloneReceipt(
            NormalCampaignCompletionReceiptDocument receipt)
        {
            if (receipt == null)
            {
                return null;
            }

            return new NormalCampaignCompletionReceiptDocument
            {
                Version = receipt.Version,
                CompletedStageId = receipt.CompletedStageId,
                StageRunId = receipt.StageRunId,
                ClearSource = receipt.ClearSource,
            };
        }

        private static NormalStagePerformanceRecordDocument[] ClonePerformanceRecords(
            NormalStagePerformanceRecordDocument[] records)
        {
            if (records == null)
            {
                return null;
            }

            var cloned = new NormalStagePerformanceRecordDocument[records.Length];
            for (var i = 0; i < records.Length; i++)
            {
                var record = records[i];
                cloned[i] = record == null
                    ? null
                    : new NormalStagePerformanceRecordDocument
                    {
                        Version = record.Version,
                        StageId = record.StageId,
                        BestCombinedPushFlipUses = record.BestCombinedPushFlipUses,
                    };
            }

            return cloned;
        }

        private static PlayerStageClearRecordDocument CloneRecord(PlayerStageClearRecordDocument record)
        {
            if (record == null)
            {
                return null;
            }

            return new PlayerStageClearRecordDocument
            {
                StageId = record.StageId,
                HasAttempted = record.HasAttempted,
                HasCleared = record.HasCleared,
                ClearCount = record.ClearCount,
                ProcessedStageRunIds = CloneArray(record.ProcessedStageRunIds),
            };
        }

        private static string[] CloneArray(string[] values)
        {
            return values == null ? null : (string[])values.Clone();
        }
    }
}
