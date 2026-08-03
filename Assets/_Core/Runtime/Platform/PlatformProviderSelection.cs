using System;
using System.Collections.Generic;

namespace Game.Platform.Runtime
{
    public static class PlatformProviderSelection
    {
        public const string ProviderSelectionArgument = "-j2mPlatformProvider";

        internal const string CommandLineSource = "CommandLine";
        internal const string TestOverrideSource = "TestOverride";

        private static PlatformProviderSelectionRequest currentRequest =
            PlatformProviderSelectionRequest.None(CommandLineSource);

        internal static PlatformProviderSelectionRequest CurrentRequest => currentRequest;

        internal static PlatformProviderSelectionRequest ParseArguments(
            IReadOnlyList<string> arguments)
        {
            if (arguments == null || arguments.Count == 0)
            {
                return PlatformProviderSelectionRequest.None(CommandLineSource);
            }

            var selectionCount = 0;
            var requestedProviderId = default(PlatformProviderId);
            for (var index = 0; index < arguments.Count; index++)
            {
                var argument = arguments[index] ?? string.Empty;
                string candidate = null;
                if (string.Equals(argument, ProviderSelectionArgument, StringComparison.Ordinal))
                {
                    if (index + 1 >= arguments.Count ||
                        string.IsNullOrWhiteSpace(arguments[index + 1]))
                    {
                        return PlatformProviderSelectionRequest.Invalid(
                            CommandLineSource,
                            ProviderSelectionArgument + " requires a provider ID value.");
                    }

                    candidate = arguments[++index];
                }
                else
                {
                    var prefix = ProviderSelectionArgument + "=";
                    if (argument.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        candidate = argument.Substring(prefix.Length);
                        if (string.IsNullOrWhiteSpace(candidate))
                        {
                            return PlatformProviderSelectionRequest.Invalid(
                                CommandLineSource,
                                ProviderSelectionArgument + " requires a provider ID value.");
                        }
                    }
                }

                if (candidate == null)
                {
                    continue;
                }

                selectionCount++;
                if (selectionCount > 1)
                {
                    return PlatformProviderSelectionRequest.Conflicting(
                        CommandLineSource,
                        "Multiple explicit platform provider selections were supplied.");
                }

                var normalizedCandidate = candidate.Trim().ToLowerInvariant();
                try
                {
                    requestedProviderId = new PlatformProviderId(normalizedCandidate);
                }
                catch (ArgumentException)
                {
                    return PlatformProviderSelectionRequest.Invalid(
                        CommandLineSource,
                        "The explicit platform provider ID is invalid.");
                }
            }

            return selectionCount == 0
                ? PlatformProviderSelectionRequest.None(CommandLineSource)
                : PlatformProviderSelectionRequest.Explicit(
                    requestedProviderId,
                    CommandLineSource);
        }

        internal static void ResetFromArguments(IReadOnlyList<string> arguments)
        {
            currentRequest = ParseArguments(arguments);
        }
    }
}
