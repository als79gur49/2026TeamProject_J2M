using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public sealed class WindowsDistributionStagingRequest
{
    public string SourceBuildRoot { get; set; }
    public string DistributionTargetId { get; set; }
    public string OutputRoot { get; set; }
    public string RepositoryRoot { get; set; }
    public string SourceSha { get; set; }
    public string SourceTree { get; set; }
    public string ArtifactId { get; set; }
    public string RunId { get; set; }
    public string ScriptingBackend { get; set; }
    public Action PrePromotionValidation { get; set; }
}

public sealed class WindowsDistributionStagingResult
{
    public string OutputRoot { get; internal set; }
    public string PayloadRoot { get; internal set; }
    public string EvidenceRoot { get; internal set; }
    public string ManifestPath { get; internal set; }
    public string SuccessPath { get; internal set; }
    public string ManifestSha256 { get; internal set; }
    public string DistributionTargetId { get; internal set; }
    public string ExpectedProviderId { get; internal set; }
    public int FileCount { get; internal set; }
    public long TotalBytes { get; internal set; }
    public int DeniedArtifactCount { get; internal set; }
    public int SteamNativeCount { get; internal set; }
    public int SteamManagedCount { get; internal set; }
    public int SteamAppIdCount { get; internal set; }
}

public sealed class WindowsDistributionManifestFile
{
    public string RelativePath { get; set; }
    public long Size { get; set; }
    public string Sha256 { get; set; }
}

public sealed class WindowsDistributionPromotedValidationRequest
{
    public string PromotedRoot { get; set; }
    public string DistributionTargetId { get; set; }
    public string ScriptingBackend { get; set; }
    public WindowsDistributionManifestFile[] ManifestFiles { get; set; }
    public int ManifestDeniedArtifactCount { get; set; }
    public int ManifestFileCount { get; set; }
    public long ManifestTotalBytes { get; set; }
}

public sealed class WindowsDistributionPromotedValidationResult
{
    public string PromotedRoot { get; internal set; }
    public string PayloadRoot { get; internal set; }
    public string EvidenceRoot { get; internal set; }
    public string ManifestPath { get; internal set; }
    public string SuccessPath { get; internal set; }
    public string ManifestSha256 { get; internal set; }
    public string DistributionTargetId { get; internal set; }
    public int FileCount { get; internal set; }
    public long TotalBytes { get; internal set; }
    public int DeniedArtifactCount { get; internal set; }
    public int SteamNativeCount { get; internal set; }
    public int SteamManagedCount { get; internal set; }
    public int SteamAppIdCount { get; internal set; }
}

public sealed class WindowsDistributionStagingException : Exception
{
    public WindowsDistributionStagingException(string code, string message)
        : base(code + ": " + message)
    {
        Code = code;
    }

    public string Code { get; private set; }
}

public static class WindowsDistributionStager
{
    public const string ManifestFileName = "distribution-manifest.json";
    public const string SuccessFileName = "SUCCESS.json";
    private const string MonoScriptingBackend = "Mono2x";
    private const string Il2CppScriptingBackend = "IL2CPP";

    public static WindowsDistributionStagingResult Stage(
        WindowsDistributionStagingRequest request)
    {
        if (request == null)
        {
            throw Failure("STAGING_INVALID_ARGUMENT", "Staging request is required.");
        }

        WindowsDistributionTargetConfiguration target;
        if (!WindowsDistributionTargetPolicy.TryResolve(
                request.DistributionTargetId, out target))
        {
            throw Failure(
                "STAGING_UNKNOWN_DISTRIBUTION_TARGET",
                "Missing or unsupported distribution target: " +
                (request.DistributionTargetId ?? "<null>"));
        }

        var contractFailure =
            WindowsDistributionTargetPolicy.ValidateConfiguration(target);
        if (contractFailure != WindowsDistributionValidationFailure.None)
        {
            throw Failure(
                "STAGING_DISTRIBUTION_CONTRACT_INVALID",
                contractFailure.ToString());
        }
        ValidateScriptingBackend(request.ScriptingBackend);

        var sourceRoot = ResolveInputRoot(
            request.SourceBuildRoot, "SourceBuildRoot", mustExist: true);
        var outputRoot = ResolveInputRoot(
            request.OutputRoot, "OutputRoot", mustExist: false);
        var repositoryRoot = ResolveInputRoot(
            request.RepositoryRoot, "RepositoryRoot", mustExist: true);

        ValidateDisjointPaths(sourceRoot, outputRoot, repositoryRoot);
        RejectReparsePoint(sourceRoot, "SourceBuildRoot");
        RejectReparseAncestors(outputRoot);

        if (IoDirectoryExists(outputRoot) || IoFileExists(outputRoot))
        {
            throw Failure(
                "STAGING_OUTPUT_COLLISION",
                "OutputRoot must be a fresh path: " + outputRoot);
        }

        var sourceBefore = InventorySource(sourceRoot);
        ValidateSourceRuntimeCompleteness(
            sourceBefore, request.ScriptingBackend);

        var parent = Path.GetDirectoryName(outputRoot);
        if (string.IsNullOrEmpty(parent))
        {
            throw Failure("STAGING_PATH_ESCAPE_DETECTED", "OutputRoot has no parent.");
        }

        IoCreateDirectory(parent);
        var runId = string.IsNullOrWhiteSpace(request.RunId)
            ? Guid.NewGuid().ToString("N")
            : request.RunId.Trim();
        var temporaryRoot = Path.Combine(
            parent,
            ".staging-" + Path.GetFileName(outputRoot) + "-" +
            SanitizePathToken(runId));

        if (IoDirectoryExists(temporaryRoot) || IoFileExists(temporaryRoot))
        {
            throw Failure(
                "STAGING_OUTPUT_COLLISION",
                "Temporary staging path already exists: " + temporaryRoot);
        }

        try
        {
            var payloadRoot = Path.Combine(temporaryRoot, "payload");
            var evidenceRoot = Path.Combine(temporaryRoot, "evidence");
            IoCreateDirectory(payloadRoot);
            IoCreateDirectory(evidenceRoot);

            CopyIncludedFiles(sourceRoot, payloadRoot, target, sourceBefore);

            var destinationInventory = InventoryDestination(payloadRoot);
            ValidateDestinationRuntimeCompleteness(
                payloadRoot, destinationInventory, request.ScriptingBackend);

            var denied = destinationInventory
                .Where(file => SteamPipeStagingSanitizerPolicy.IsDeniedContent(
                    file.RelativePath))
                .ToArray();
            if (denied.Length != 0)
            {
                throw Failure(
                    "STAGING_FORBIDDEN_ARTIFACT_PRESENT",
                    string.Join(", ", denied.Select(file => file.RelativePath).ToArray()));
            }

            contractFailure =
                WindowsDistributionTargetPolicy.ValidatePromotedArtifactInventory(
                    target,
                    destinationInventory.Select(file => file.RelativePath));
            if (contractFailure != WindowsDistributionValidationFailure.None)
            {
                throw Failure(
                    "STAGING_PROMOTED_ARTIFACT_CONTRACT_FAILED",
                    contractFailure.ToString());
            }

            AssertSourceUnchanged(sourceRoot, sourceBefore);
            var manifestPath = Path.Combine(evidenceRoot, ManifestFileName);
            WriteManifest(
                manifestPath,
                request,
                target,
                runId,
                destinationInventory,
                denied.Length);
            var manifestHash = GetSha256(manifestPath);
            var totalBytes = destinationInventory.Sum(file => file.Size);
            var successPath = Path.Combine(evidenceRoot, SuccessFileName);
            WriteSuccess(
                successPath,
                request,
                target,
                manifestHash,
                destinationInventory.Count,
                totalBytes);

            if (request.PrePromotionValidation != null)
            {
                request.PrePromotionValidation();
            }

            Directory.Move(ToIoPath(temporaryRoot), ToIoPath(outputRoot));

            var finalPayloadRoot = Path.Combine(outputRoot, "payload");
            var finalEvidenceRoot = Path.Combine(outputRoot, "evidence");
            return new WindowsDistributionStagingResult
            {
                OutputRoot = outputRoot,
                PayloadRoot = finalPayloadRoot,
                EvidenceRoot = finalEvidenceRoot,
                ManifestPath = Path.Combine(finalEvidenceRoot, ManifestFileName),
                SuccessPath = Path.Combine(finalEvidenceRoot, SuccessFileName),
                ManifestSha256 = manifestHash,
                DistributionTargetId = target.TargetId,
                ExpectedProviderId = target.ExpectedProviderId,
                FileCount = destinationInventory.Count,
                TotalBytes = totalBytes,
                DeniedArtifactCount = denied.Length,
                SteamNativeCount = CountFileName(
                    destinationInventory,
                    WindowsDistributionTargetPolicy.SteamNativeArtifact),
                SteamManagedCount = CountFileName(
                    destinationInventory,
                    WindowsDistributionTargetPolicy.SteamManagedBindingArtifact),
                SteamAppIdCount = CountFileName(
                    destinationInventory,
                    WindowsDistributionTargetPolicy.SteamAppIdArtifact),
            };
        }
        catch
        {
            TryDeleteDirectory(temporaryRoot);
            throw;
        }
    }

    public static WindowsDistributionPromotedValidationResult ValidatePromotedArtifact(
        WindowsDistributionPromotedValidationRequest request)
    {
        if (request == null)
        {
            throw Failure(
                "STAGING_INVALID_ARGUMENT",
                "Promoted validation request is required.");
        }

        WindowsDistributionTargetConfiguration target;
        if (!WindowsDistributionTargetPolicy.TryResolve(
                request.DistributionTargetId, out target))
        {
            throw Failure(
                "STAGING_UNKNOWN_DISTRIBUTION_TARGET",
                "Missing or unsupported distribution target: " +
                (request.DistributionTargetId ?? "<null>"));
        }

        var contractFailure =
            WindowsDistributionTargetPolicy.ValidateConfiguration(target);
        if (contractFailure != WindowsDistributionValidationFailure.None)
        {
            throw Failure(
                "STAGING_DISTRIBUTION_CONTRACT_INVALID",
                contractFailure.ToString());
        }
        ValidateScriptingBackend(request.ScriptingBackend);

        var promotedRoot = ResolveInputRoot(
            request.PromotedRoot, "PromotedRoot", mustExist: true);
        RejectReparseAncestors(promotedRoot);
        RejectReparsePoint(promotedRoot, "PromotedRoot");
        var payloadRoot = ResolveContainedPath(promotedRoot, "payload");
        var evidenceRoot = ResolveContainedPath(promotedRoot, "evidence");
        if (!IoDirectoryExists(payloadRoot) || !IoDirectoryExists(evidenceRoot))
        {
            throw Failure(
                "STAGING_PROMOTED_STRUCTURE_INVALID",
                "PromotedRoot must contain payload/ and evidence/ directories.");
        }

        RejectReparsePoint(payloadRoot, "payload");
        RejectReparsePoint(evidenceRoot, "evidence");
        var manifestPath = ResolveContainedPath(evidenceRoot, ManifestFileName);
        var successPath = ResolveContainedPath(evidenceRoot, SuccessFileName);
        if (!IoFileExists(manifestPath) || !IoFileExists(successPath))
        {
            throw Failure(
                "STAGING_PROMOTED_STRUCTURE_INVALID",
                "Promoted evidence is incomplete.");
        }

        Inventory(evidenceRoot, rejectReparsePoints: true);
        var inventory = InventoryDestination(payloadRoot);
        ValidateManifestInventory(request, inventory);
        ValidateDestinationRuntimeCompleteness(
            payloadRoot, inventory, request.ScriptingBackend);

        var denied = inventory
            .Where(file => SteamPipeStagingSanitizerPolicy.IsDeniedContent(
                file.RelativePath))
            .ToArray();
        if (request.ManifestDeniedArtifactCount != 0 || denied.Length != 0)
        {
            throw Failure(
                "STAGING_FORBIDDEN_ARTIFACT_PRESENT",
                string.Join(", ", denied.Select(file => file.RelativePath).ToArray()));
        }

        contractFailure =
            WindowsDistributionTargetPolicy.ValidatePromotedArtifactInventory(
                target,
                inventory.Select(file => file.RelativePath));
        if (contractFailure != WindowsDistributionValidationFailure.None)
        {
            throw Failure(
                "STAGING_PROMOTED_ARTIFACT_CONTRACT_FAILED",
                contractFailure.ToString());
        }

        var steamNativeCount = CountFileName(
            inventory,
            WindowsDistributionTargetPolicy.SteamNativeArtifact);
        var steamManagedCount = CountFileName(
            inventory,
            WindowsDistributionTargetPolicy.SteamManagedBindingArtifact);
        var steamAppIdCount = CountFileName(
            inventory,
            WindowsDistributionTargetPolicy.SteamAppIdArtifact);
        if (string.Equals(
                target.TargetId,
                WindowsDistributionTargetPolicy.SteamWindowsTargetId,
                StringComparison.Ordinal) &&
            (steamNativeCount != 1 || steamManagedCount != 1 || steamAppIdCount != 0))
        {
            throw Failure(
                "STAGING_PROMOTED_ARTIFACT_CONTRACT_FAILED",
                "SteamWindows requires exactly one native binding, exactly one " +
                "managed binding, and no steam_appid.txt.");
        }

        return new WindowsDistributionPromotedValidationResult
        {
            PromotedRoot = promotedRoot,
            PayloadRoot = payloadRoot,
            EvidenceRoot = evidenceRoot,
            ManifestPath = manifestPath,
            SuccessPath = successPath,
            ManifestSha256 = GetSha256(manifestPath),
            DistributionTargetId = target.TargetId,
            FileCount = inventory.Count,
            TotalBytes = inventory.Sum(file => file.Size),
            DeniedArtifactCount = denied.Length,
            SteamNativeCount = steamNativeCount,
            SteamManagedCount = steamManagedCount,
            SteamAppIdCount = steamAppIdCount,
        };
    }

    public static bool IsRuntimeIncludeCandidate(string normalizedRelativePath)
    {
        var path = SteamPipeStagingSanitizerPolicy.Normalize(normalizedRelativePath);
        if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path) ||
            ContainsTraversalSegment(path))
        {
            return false;
        }

        if (string.Equals(
                path,
                WindowsDistributionTargetPolicy.ExecutableName,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "UnityPlayer.dll", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "UnityCrashHandler64.exe", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "GameAssembly.dll", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "baselib.dll", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.IndexOf('/') < 0 &&
            path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsPathOrChildOf(path, "VectorQuake_Data") ||
               IsPathOrChildOf(path, "MonoBleedingEdge");
    }

    private static void CopyIncludedFiles(
        string sourceRoot,
        string payloadRoot,
        WindowsDistributionTargetConfiguration target,
        IList<StagedFile> sourceInventory)
    {
        foreach (var source in sourceInventory)
        {
            if (!IsRuntimeIncludeCandidate(source.RelativePath) ||
                SteamPipeStagingSanitizerPolicy.IsDeniedContent(source.RelativePath) ||
                IsTargetForbiddenFile(target, source.RelativePath))
            {
                continue;
            }

            var destination = ResolveContainedPath(payloadRoot, source.RelativePath);
            var destinationParent = Path.GetDirectoryName(destination);
            if (string.IsNullOrEmpty(destinationParent))
            {
                throw Failure(
                    "STAGING_PATH_ESCAPE_DETECTED",
                    "Destination file has no parent: " + source.RelativePath);
            }

            IoCreateDirectory(destinationParent);
            File.Copy(ToIoPath(source.FullPath), ToIoPath(destination), overwrite: false);
            var destinationHash = GetSha256(destination);
            if (!string.Equals(
                    source.Sha256, destinationHash, StringComparison.OrdinalIgnoreCase))
            {
                throw Failure(
                    "STAGING_COPY_HASH_MISMATCH",
                    source.RelativePath);
            }
        }
    }

    private static bool IsTargetForbiddenFile(
        WindowsDistributionTargetConfiguration target,
        string relativePath)
    {
        var fileName = Path.GetFileName(relativePath.Replace('/', Path.DirectorySeparatorChar));
        return target.ForbiddenArtifacts.Any(forbidden => string.Equals(
            forbidden, fileName, StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateScriptingBackend(string scriptingBackend)
    {
        if (string.IsNullOrWhiteSpace(scriptingBackend))
        {
            throw Failure(
                "STAGING_BACKEND_MISSING",
                "build-metadata.json scriptingBackend is required.");
        }

        if (!string.Equals(
                scriptingBackend, MonoScriptingBackend, StringComparison.Ordinal) &&
            !string.Equals(
                scriptingBackend, Il2CppScriptingBackend, StringComparison.Ordinal))
        {
            throw Failure(
                "STAGING_BACKEND_UNSUPPORTED",
                "Unsupported scriptingBackend: " + scriptingBackend);
        }
    }

    private static void ValidateSourceRuntimeCompleteness(
        IList<StagedFile> sourceInventory,
        string scriptingBackend)
    {
        RequireExactFile(sourceInventory, WindowsDistributionTargetPolicy.ExecutableName);
        RequireExactFile(sourceInventory, "UnityPlayer.dll");
        RequireDirectoryContent(sourceInventory, "VectorQuake_Data");

        if (string.Equals(
                scriptingBackend, MonoScriptingBackend, StringComparison.Ordinal))
        {
            RequireDirectoryContent(sourceInventory, "MonoBleedingEdge");
            return;
        }

        RequireExactFile(sourceInventory, "GameAssembly.dll");
        RequireExactFile(sourceInventory, "baselib.dll");
    }

    private static void ValidateDestinationRuntimeCompleteness(
        string payloadRoot,
        IList<StagedFile> destinationInventory,
        string scriptingBackend)
    {
        RequireExactFile(destinationInventory, WindowsDistributionTargetPolicy.ExecutableName);
        RequireExactFile(destinationInventory, "UnityPlayer.dll");
        RequireDirectoryContent(destinationInventory, "VectorQuake_Data");

        if (string.Equals(
                scriptingBackend, MonoScriptingBackend, StringComparison.Ordinal))
        {
            RequireDirectoryContent(destinationInventory, "MonoBleedingEdge");
            if (!IoDirectoryExists(Path.Combine(payloadRoot, "MonoBleedingEdge")))
            {
                throw Failure(
                    "STAGING_REQUIRED_RUNTIME_MISSING",
                    "MonoBleedingEdge directory is missing from payload.");
            }
            return;
        }

        RequireExactFile(destinationInventory, "GameAssembly.dll");
        RequireExactFile(destinationInventory, "baselib.dll");
    }

    private static void RequireExactFile(IList<StagedFile> inventory, string path)
    {
        if (!inventory.Any(file => string.Equals(
                file.RelativePath, path, StringComparison.OrdinalIgnoreCase)))
        {
            throw Failure("STAGING_REQUIRED_RUNTIME_MISSING", path);
        }
    }

    private static void RequireDirectoryContent(
        IList<StagedFile> inventory,
        string directory)
    {
        if (!inventory.Any(file => IsPathOrChildOf(file.RelativePath, directory) &&
                                   file.RelativePath.Length > directory.Length))
        {
            throw Failure(
                "STAGING_REQUIRED_RUNTIME_MISSING",
                directory + "/");
        }
    }

    private static IList<StagedFile> InventorySource(string sourceRoot)
    {
        return Inventory(sourceRoot, rejectReparsePoints: true);
    }

    private static IList<StagedFile> InventoryDestination(string payloadRoot)
    {
        return Inventory(payloadRoot, rejectReparsePoints: true);
    }

    private static IList<StagedFile> Inventory(string root, bool rejectReparsePoints)
    {
        var paths = new List<string>();
        CollectFiles(root, root, paths, rejectReparsePoints);
        paths.Sort(StringComparer.Ordinal);

        var result = new List<StagedFile>(paths.Count);
        foreach (var path in paths)
        {
            var info = new FileInfo(ToIoPath(path));
            result.Add(new StagedFile
            {
                FullPath = path,
                RelativePath = GetNormalizedRelativePath(root, path),
                Size = info.Length,
                Sha256 = GetSha256(path),
            });
        }

        result.Sort((first, second) =>
            StringComparer.Ordinal.Compare(first.RelativePath, second.RelativePath));
        return result;
    }

    private static void CollectFiles(
        string root,
        string directory,
        IList<string> files,
        bool rejectReparsePoints)
    {
        var entries = Directory.GetFileSystemEntries(ToIoPath(directory))
            .Select(FromIoPath)
            .ToArray();
        Array.Sort(entries, StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var full = Path.GetFullPath(entry);
            EnsureContained(root, full);
            var attributes = File.GetAttributes(ToIoPath(full));
            if (rejectReparsePoints &&
                (attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw Failure(
                    "STAGING_REPARSE_POINT_REJECTED",
                    GetNormalizedRelativePath(root, full));
            }

            if ((attributes & FileAttributes.Directory) != 0)
            {
                CollectFiles(root, full, files, rejectReparsePoints);
            }
            else
            {
                files.Add(full);
            }
        }
    }

    private static void AssertSourceUnchanged(
        string sourceRoot,
        IList<StagedFile> before)
    {
        var after = InventorySource(sourceRoot);
        if (before.Count != after.Count)
        {
            throw Failure(
                "STAGING_SOURCE_MUTATED",
                "Source file count changed during staging.");
        }

        for (var index = 0; index < before.Count; index++)
        {
            if (!string.Equals(
                    before[index].RelativePath,
                    after[index].RelativePath,
                    StringComparison.Ordinal) ||
                before[index].Size != after[index].Size ||
                !string.Equals(
                    before[index].Sha256,
                    after[index].Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw Failure(
                    "STAGING_SOURCE_MUTATED",
                    before[index].RelativePath);
            }
        }
    }

    private static void ValidateManifestInventory(
        WindowsDistributionPromotedValidationRequest request,
        IList<StagedFile> inventory)
    {
        var manifestFiles = request.ManifestFiles ??
            Array.Empty<WindowsDistributionManifestFile>();
        if (request.ManifestFileCount != manifestFiles.Length ||
            request.ManifestFileCount != inventory.Count ||
            request.ManifestTotalBytes != inventory.Sum(file => file.Size))
        {
            throw Failure(
                "STAGING_PROMOTED_MANIFEST_MISMATCH",
                "Manifest totals do not match payload inventory.");
        }

        var expected = manifestFiles
            .OrderBy(file => file == null ? string.Empty : file.RelativePath,
                StringComparer.Ordinal)
            .ToArray();
        var manifestPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var manifestFile in expected)
        {
            if (manifestFile == null ||
                string.IsNullOrWhiteSpace(manifestFile.RelativePath) ||
                Path.IsPathRooted(manifestFile.RelativePath) ||
                ContainsTraversalSegment(manifestFile.RelativePath) ||
                manifestFile.RelativePath.IndexOf('\\') >= 0 ||
                string.IsNullOrWhiteSpace(manifestFile.Sha256))
            {
                throw Failure(
                    "STAGING_PROMOTED_MANIFEST_MISMATCH",
                    "Manifest contains an invalid file entry.");
            }

            if (!manifestPaths.Add(manifestFile.RelativePath))
            {
                throw Failure(
                    "STAGING_PROMOTED_MANIFEST_MISMATCH",
                    "Manifest contains a duplicate path: " + manifestFile.RelativePath);
            }
        }

        for (var index = 0; index < expected.Length; index++)
        {
            var manifestFile = expected[index];
            var actual = inventory[index];
            if (!string.Equals(
                    manifestFile.RelativePath,
                    actual.RelativePath,
                    StringComparison.Ordinal) ||
                manifestFile.Size != actual.Size ||
                !string.Equals(
                    manifestFile.Sha256,
                    actual.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw Failure(
                    "STAGING_PROMOTED_MANIFEST_MISMATCH",
                    manifestFile.RelativePath);
            }
        }
    }

    private static void WriteManifest(
        string path,
        WindowsDistributionStagingRequest request,
        WindowsDistributionTargetConfiguration target,
        string runId,
        IList<StagedFile> files,
        int deniedArtifactCount)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        AppendJsonProperty(builder, "schemaVersion", "1.0", true);
        AppendJsonProperty(builder, "distributionTargetId", target.TargetId, true);
        AppendJsonProperty(builder, "sourceSha", request.SourceSha ?? string.Empty, true);
        AppendJsonProperty(builder, "sourceTree", request.SourceTree ?? string.Empty, true);
        AppendJsonProperty(builder, "artifactId", request.ArtifactId ?? string.Empty, true);
        AppendJsonProperty(builder, "runId", runId, true);
        AppendJsonProperty(builder, "scriptingBackend", request.ScriptingBackend, true);
        AppendJsonProperty(builder, "expectedProviderId", target.ExpectedProviderId, true);
        AppendJsonArray(
            builder,
            "expectedLaunchArguments",
            target.ExpectedLaunchArguments,
            true);
        builder.Append("  \"deniedArtifactCount\": ")
            .Append(deniedArtifactCount.ToString(CultureInfo.InvariantCulture))
            .AppendLine(",");
        builder.Append("  \"fileCount\": ")
            .Append(files.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine(",");
        builder.Append("  \"totalBytes\": ")
            .Append(files.Sum(file => file.Size).ToString(CultureInfo.InvariantCulture))
            .AppendLine(",");
        builder.AppendLine("  \"files\": [");
        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            builder.AppendLine("    {");
            builder.Append("      \"relativePath\": \"")
                .Append(JsonEscape(file.RelativePath)).AppendLine("\",");
            builder.Append("      \"size\": ")
                .Append(file.Size.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("      \"sha256\": \"")
                .Append(file.Sha256).AppendLine("\"");
            builder.Append("    }");
            builder.AppendLine(index + 1 == files.Count ? string.Empty : ",");
        }

        builder.AppendLine("  ]");
        builder.AppendLine("}");
        WriteUtf8WithoutBom(path, builder.ToString());
    }

    private static void WriteSuccess(
        string path,
        WindowsDistributionStagingRequest request,
        WindowsDistributionTargetConfiguration target,
        string manifestHash,
        int fileCount,
        long totalBytes)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        AppendJsonProperty(builder, "status", "SUCCESS", true);
        AppendJsonProperty(builder, "distributionTarget", target.TargetId, true);
        AppendJsonProperty(builder, "sourceSha", request.SourceSha ?? string.Empty, true);
        AppendJsonProperty(builder, "sourceTree", request.SourceTree ?? string.Empty, true);
        AppendJsonProperty(builder, "scriptingBackend", request.ScriptingBackend, true);
        builder.Append("  \"fileCount\": ")
            .Append(fileCount.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
        builder.Append("  \"totalBytes\": ")
            .Append(totalBytes.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
        AppendJsonProperty(builder, "manifestSha256", manifestHash, true);
        builder.AppendLine("  \"sourceBuildImmutable\": true,");
        builder.AppendLine("  \"copyByteIdentity\": true,");
        builder.AppendLine("  \"promotedArtifactContractPassed\": true,");
        builder.AppendLine("  \"deniedArtifactCount\": 0");
        builder.AppendLine("}");
        WriteUtf8WithoutBom(path, builder.ToString());
    }

    private static void AppendJsonProperty(
        StringBuilder builder,
        string name,
        string value,
        bool trailingComma)
    {
        builder.Append("  \"").Append(JsonEscape(name)).Append("\": \"")
            .Append(JsonEscape(value ?? string.Empty)).Append("\"");
        builder.AppendLine(trailingComma ? "," : string.Empty);
    }

    private static void AppendJsonArray(
        StringBuilder builder,
        string name,
        IEnumerable<string> values,
        bool trailingComma)
    {
        builder.Append("  \"").Append(JsonEscape(name)).Append("\": [");
        var items = (values ?? Enumerable.Empty<string>()).ToArray();
        for (var index = 0; index < items.Length; index++)
        {
            if (index != 0)
            {
                builder.Append(", ");
            }

            builder.Append("\"").Append(JsonEscape(items[index])).Append("\"");
        }

        builder.Append("]");
        builder.AppendLine(trailingComma ? "," : string.Empty);
    }

    private static string JsonEscape(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value ?? string.Empty)
        {
            switch (character)
            {
                case '\\': builder.Append("\\\\"); break;
                case '"': builder.Append("\\\""); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (character < 0x20)
                    {
                        builder.Append("\\u")
                            .Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }
                    break;
            }
        }

        return builder.ToString();
    }

    private static void WriteUtf8WithoutBom(string path, string content)
    {
        File.WriteAllText(ToIoPath(path), content, new UTF8Encoding(false));
    }

    private static string GetSha256(string path)
    {
        using (var stream = File.OpenRead(ToIoPath(path)))
        using (var hash = SHA256.Create())
        {
            return string.Concat(hash.ComputeHash(stream)
                .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }

    private static int CountFileName(IList<StagedFile> files, string fileName)
    {
        return files.Count(file => string.Equals(
            Path.GetFileName(file.RelativePath.Replace('/', Path.DirectorySeparatorChar)),
            fileName,
            StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveInputRoot(
        string value,
        string argumentName,
        bool mustExist)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value) ||
            ContainsTraversalSegment(value.Replace('\\', '/')))
        {
            throw Failure(
                "STAGING_PATH_ESCAPE_DETECTED",
                argumentName + " must be an absolute normalized path.");
        }

        var full = Path.GetFullPath(value).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        if (mustExist && !IoDirectoryExists(full))
        {
            throw Failure(
                "STAGING_INVALID_ARGUMENT",
                argumentName + " does not exist: " + full);
        }

        return full;
    }

    private static void ValidateDisjointPaths(
        string sourceRoot,
        string outputRoot,
        string repositoryRoot)
    {
        if (PathsEqual(sourceRoot, outputRoot) ||
            IsContainedPath(sourceRoot, outputRoot) ||
            IsContainedPath(outputRoot, sourceRoot))
        {
            throw Failure(
                "STAGING_PATH_ESCAPE_DETECTED",
                "SourceBuildRoot and OutputRoot must be disjoint.");
        }

        if (PathsEqual(repositoryRoot, outputRoot) ||
            IsContainedPath(repositoryRoot, outputRoot))
        {
            throw Failure(
                "STAGING_REPOSITORY_OUTPUT_REJECTED",
                "OutputRoot must be outside RepositoryRoot.");
        }
    }

    private static string ResolveContainedPath(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath) ||
            ContainsTraversalSegment(relativePath))
        {
            throw Failure(
                "STAGING_PATH_ESCAPE_DETECTED",
                "Invalid relative path: " + relativePath);
        }

        var full = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        EnsureContained(root, full);
        return full;
    }

    private static string GetNormalizedRelativePath(string root, string path)
    {
        var rootFull = Path.GetFullPath(root).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var pathFull = Path.GetFullPath(path);
        EnsureContained(rootFull, pathFull);
        return pathFull.Substring(rootFull.Length + 1).Replace('\\', '/');
    }

    private static void EnsureContained(string root, string path)
    {
        if (!IsContainedPath(root, path))
        {
            throw Failure(
                "STAGING_PATH_ESCAPE_DETECTED",
                path + " is outside " + root);
        }
    }

    private static bool IsContainedPath(string root, string candidate)
    {
        var rootWithSeparator = Path.GetFullPath(root).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidateFull = Path.GetFullPath(candidate);
        return candidateFull.StartsWith(
            rootWithSeparator,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string first, string second)
    {
        return string.Equals(
            Path.GetFullPath(first).TrimEnd('\\', '/'),
            Path.GetFullPath(second).TrimEnd('\\', '/'),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsTraversalSegment(string path)
    {
        return path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => string.Equals(segment, "..", StringComparison.Ordinal));
    }

    private static bool IsPathOrChildOf(string value, string root)
    {
        return string.Equals(value, root, StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static void RejectReparsePoint(string path, string name)
    {
        if ((File.GetAttributes(ToIoPath(path)) & FileAttributes.ReparsePoint) != 0)
        {
            throw Failure(
                "STAGING_REPARSE_POINT_REJECTED",
                name + " is a reparse point: " + path);
        }
    }

    private static void RejectReparseAncestors(string outputRoot)
    {
        var current = Path.GetDirectoryName(outputRoot);
        while (!string.IsNullOrEmpty(current))
        {
            if (IoDirectoryExists(current))
            {
                RejectReparsePoint(current, "OutputRoot ancestor");
            }

            var parent = Path.GetDirectoryName(current.TrimEnd('\\', '/'));
            if (string.IsNullOrEmpty(parent) || PathsEqual(parent, current))
            {
                break;
            }

            current = parent;
        }
    }

    private static string SanitizePathToken(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var characters = value.Select(character =>
            invalid.Contains(character) ? '_' : character).ToArray();
        var result = new string(characters);
        return string.IsNullOrWhiteSpace(result) ? Guid.NewGuid().ToString("N") : result;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (IoDirectoryExists(path))
            {
                Directory.Delete(ToIoPath(path), recursive: true);
            }
        }
        catch
        {
            // Failure cleanup is best effort. A final SUCCESS marker is never created.
        }
    }

    private static WindowsDistributionStagingException Failure(
        string code,
        string message)
    {
        return new WindowsDistributionStagingException(code, message);
    }

    private static bool IoFileExists(string path)
    {
        return File.Exists(ToIoPath(path));
    }

    private static bool IoDirectoryExists(string path)
    {
        return Directory.Exists(ToIoPath(path));
    }

    private static void IoCreateDirectory(string path)
    {
        Directory.CreateDirectory(ToIoPath(path));
    }

    private static string ToIoPath(string path)
    {
        var full = Path.GetFullPath(path);
        if (full.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            return full;
        }

        if (full.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return @"\\?\UNC\" + full.Substring(2);
        }

        return @"\\?\" + full;
    }

    private static string FromIoPath(string path)
    {
        if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            return @"\\" + path.Substring(8);
        }

        return path.StartsWith(@"\\?\", StringComparison.Ordinal)
            ? path.Substring(4)
            : path;
    }

    private sealed class StagedFile
    {
        public string FullPath { get; set; }
        public string RelativePath { get; set; }
        public long Size { get; set; }
        public string Sha256 { get; set; }
    }
}
