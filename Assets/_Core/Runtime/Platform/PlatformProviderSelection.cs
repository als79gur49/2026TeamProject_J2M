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

            var prefix = ProviderSelectionArgument + "=";
            var selectionCount = 0;
            for (var index = 0; index < arguments.Count; index++)
            {
                var argument = arguments[index] ?? string.Empty;
                if (string.Equals(argument, ProviderSelectionArgument, StringComparison.Ordinal) ||
                    argument.StartsWith(prefix, StringComparison.Ordinal))
                {
                    selectionCount++;
                }
            }

            if (selectionCount > 1)
            {
                return PlatformProviderSelectionRequest.Conflicting(
                    CommandLineSource,
                    "Multiple explicit platform provider selections were supplied.");
            }

            if (selectionCount == 0)
            {
                return PlatformProviderSelectionRequest.None(CommandLineSource);
            }

            string candidate = null;
            for (var index = 0; index < arguments.Count; index++)
            {
                var argument = arguments[index] ?? string.Empty;
                if (string.Equals(argument, ProviderSelectionArgument, StringComparison.Ordinal))
                {
                    if (index + 1 >= arguments.Count ||
                        string.IsNullOrWhiteSpace(arguments[index + 1]) ||
                        IsCommandLineOptionToken(arguments[index + 1]))
                    {
                        return PlatformProviderSelectionRequest.Invalid(
                            CommandLineSource,
                            ProviderSelectionArgument + " requires a provider ID value.");
                    }

                    candidate = arguments[index + 1];
                    break;
                }

                if (!argument.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                candidate = argument.Substring(prefix.Length);
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    return PlatformProviderSelectionRequest.Invalid(
                        CommandLineSource,
                        ProviderSelectionArgument + " requires a provider ID value.");
                }

                break;
            }

            var normalizedCandidate = candidate.Trim().ToLowerInvariant();
            PlatformProviderId requestedProviderId;
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

            return PlatformProviderSelectionRequest.Explicit(
                requestedProviderId,
                CommandLineSource);
        }

        public static bool HasExactOptInFlag(
            IReadOnlyList<string> arguments,
            string flag)
        {
            if (string.IsNullOrWhiteSpace(flag) ||
                !flag.StartsWith("-", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Command-line opt-in flag must be a non-empty option token.",
                    nameof(flag));
            }

            if (arguments == null)
            {
                return false;
            }

            for (var index = 0; index < arguments.Count; index++)
            {
                if (string.Equals(arguments[index], flag, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        internal static void ResetFromArguments(IReadOnlyList<string> arguments)
        {
            currentRequest = ParseArguments(arguments);
        }

        private static bool IsCommandLineOptionToken(string token)
        {
            return !string.IsNullOrEmpty(token) &&
                token.StartsWith("-", StringComparison.Ordinal);
        }
    }
}
