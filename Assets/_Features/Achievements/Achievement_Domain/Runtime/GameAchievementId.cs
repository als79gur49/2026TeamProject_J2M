using System;

namespace Game.Product.Achievements
{
    public readonly struct GameAchievementId : IEquatable<GameAchievementId>
    {
        private readonly string _value;

        private GameAchievementId(string value)
        {
            _value = value;
        }

        public string Value => _value ?? string.Empty;

        public bool IsValid => IsValidToken(_value);

        public static bool TryCreate(string value, out GameAchievementId achievementId)
        {
            if (!IsValidToken(value))
            {
                achievementId = default;
                return false;
            }

            achievementId = new GameAchievementId(value);
            return true;
        }

        public static GameAchievementId Require(string value)
        {
            if (!TryCreate(value, out var achievementId))
            {
                throw new ArgumentException("A valid product achievement ID is required.", nameof(value));
            }

            return achievementId;
        }

        public bool Equals(GameAchievementId other)
        {
            return string.Equals(_value, other._value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is GameAchievementId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(GameAchievementId left, GameAchievementId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GameAchievementId left, GameAchievementId right)
        {
            return !left.Equals(right);
        }

        private static bool IsValidToken(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                var isLowercaseAscii = character >= 'a' && character <= 'z';
                var isDigit = character >= '0' && character <= '9';
                if (!isLowercaseAscii &&
                    !isDigit &&
                    character != '.' &&
                    character != '_' &&
                    character != '-')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
