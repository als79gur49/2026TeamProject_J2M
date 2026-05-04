using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Authoring
{
    public enum VfxAuthoringValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    public readonly struct VfxAuthoringValidationMessage
    {
        public VfxAuthoringValidationMessage(
            VfxAuthoringValidationSeverity severity,
            string code,
            string message,
            UnityEngine.Object context = null)
        {
            Severity = severity;
            Code = string.IsNullOrWhiteSpace(code) ? "VFX_AUTHORING" : code;
            Message = message ?? string.Empty;
            Context = context;
        }

        public VfxAuthoringValidationSeverity Severity { get; }

        public string Code { get; }

        public string Message { get; }

        public UnityEngine.Object Context { get; }

        public override string ToString()
        {
            return $"[{Severity}] {Code}: {Message}";
        }
    }

    public sealed class VfxAuthoringValidationResult
    {
        public static readonly VfxAuthoringValidationResult Success =
            new VfxAuthoringValidationResult(Array.Empty<VfxAuthoringValidationMessage>());

        public VfxAuthoringValidationResult(IEnumerable<VfxAuthoringValidationMessage> messages)
        {
            Messages = (messages ?? Array.Empty<VfxAuthoringValidationMessage>()).ToArray();
            HasErrors = Messages.Any(message => message.Severity == VfxAuthoringValidationSeverity.Error);
            HasWarnings = Messages.Any(message => message.Severity == VfxAuthoringValidationSeverity.Warning);
        }

        public IReadOnlyList<VfxAuthoringValidationMessage> Messages { get; }

        public bool HasErrors { get; }

        public bool HasWarnings { get; }

        public void ThrowIfErrors()
        {
            if (!HasErrors)
            {
                return;
            }

            var firstError = Messages.First(message => message.Severity == VfxAuthoringValidationSeverity.Error);
            throw new InvalidOperationException(firstError.Message);
        }

        public static VfxAuthoringValidationResult FromMessages(
            IEnumerable<VfxAuthoringValidationMessage> messages)
        {
            var result = new VfxAuthoringValidationResult(messages);
            return result.Messages.Count == 0 ? Success : result;
        }

        public static VfxAuthoringValidationMessage Info(
            string code,
            string message,
            UnityEngine.Object context = null)
        {
            return new VfxAuthoringValidationMessage(
                VfxAuthoringValidationSeverity.Info,
                code,
                message,
                context);
        }

        public static VfxAuthoringValidationMessage Warning(
            string code,
            string message,
            UnityEngine.Object context = null)
        {
            return new VfxAuthoringValidationMessage(
                VfxAuthoringValidationSeverity.Warning,
                code,
                message,
                context);
        }

        public static VfxAuthoringValidationMessage Error(
            string code,
            string message,
            UnityEngine.Object context = null)
        {
            return new VfxAuthoringValidationMessage(
                VfxAuthoringValidationSeverity.Error,
                code,
                message,
                context);
        }
    }
}
