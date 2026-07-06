using System;
using System.Collections.Generic;

namespace Game.Feature.UI.ViewShared
{
    public enum LocalizedTextRole
    {
        Title,
        Subtitle,
        Body,
        Button,
        Label,
    }

    public enum LocalizedTextWeight
    {
        Default,
        Regular,
        Bold,
    }

    public readonly struct LocalizedTextDescriptor : IEquatable<LocalizedTextDescriptor>
    {
        private readonly string _table;
        private readonly string _key;
        private readonly IReadOnlyList<object> _arguments;

        public LocalizedTextDescriptor(
            string table,
            string key,
            LocalizedTextRole role = LocalizedTextRole.Body,
            LocalizedTextWeight weight = LocalizedTextWeight.Default,
            IReadOnlyList<object> arguments = null)
        {
            _table = table ?? string.Empty;
            _key = key ?? string.Empty;
            _arguments = CopyArguments(arguments);
            Role = role;
            Weight = weight;
        }

        public string Table => _table ?? string.Empty;

        public string Key => _key ?? string.Empty;

        public LocalizedTextRole Role { get; }

        public LocalizedTextWeight Weight { get; }

        public IReadOnlyList<object> Arguments => _arguments ?? Array.Empty<object>();

        public bool Equals(LocalizedTextDescriptor other)
        {
            if (!string.Equals(Table, other.Table, StringComparison.Ordinal) ||
                !string.Equals(Key, other.Key, StringComparison.Ordinal) ||
                Role != other.Role ||
                Weight != other.Weight ||
                Arguments.Count != other.Arguments.Count)
            {
                return false;
            }

            for (var i = 0; i < Arguments.Count; i++)
            {
                if (!Equals(Arguments[i], other.Arguments[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is LocalizedTextDescriptor other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = HashCode.Combine(Table, Key, Role, Weight);
            for (var i = 0; i < Arguments.Count; i++)
            {
                hash = HashCode.Combine(hash, Arguments[i]);
            }

            return hash;
        }

        public static bool operator ==(LocalizedTextDescriptor left, LocalizedTextDescriptor right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(LocalizedTextDescriptor left, LocalizedTextDescriptor right)
        {
            return !left.Equals(right);
        }

        private static IReadOnlyList<object> CopyArguments(IReadOnlyList<object> arguments)
        {
            if (arguments == null || arguments.Count == 0)
            {
                return Array.Empty<object>();
            }

            var copy = new object[arguments.Count];
            for (var i = 0; i < arguments.Count; i++)
            {
                copy[i] = arguments[i];
            }

            return copy;
        }
    }

    public interface ILocalizedTextResolver
    {
        string CurrentLocaleCode { get; }

        event Action LocaleChanged;

        string Resolve(LocalizedTextDescriptor descriptor);
    }
}
