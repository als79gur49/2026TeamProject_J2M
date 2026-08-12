using System;

namespace Game.Product.Achievements.Infrastructure
{
    internal static class JsonSyntaxValidator
    {
        private const int MaxContainerDepth = 64;

        public static bool IsValid(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            var index = 0;
            if (!TrySkipValue(json, ref index, depth: 0))
            {
                return false;
            }

            SkipWhitespace(json, ref index);
            return index == json.Length;
        }

        private static bool TrySkipValue(string json, ref int index, int depth)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length)
            {
                return false;
            }

            switch (json[index])
            {
                case '"':
                    return TryReadString(json, ref index);
                case '{':
                    return TrySkipObject(json, ref index, depth);
                case '[':
                    return TrySkipArray(json, ref index, depth);
                default:
                    return TrySkipPrimitive(json, ref index);
            }
        }

        private static bool TrySkipObject(string json, ref int index, int depth)
        {
            if (depth >= MaxContainerDepth || !TryConsume(json, ref index, '{'))
            {
                return false;
            }

            SkipWhitespace(json, ref index);
            if (TryConsume(json, ref index, '}'))
            {
                return true;
            }

            while (index < json.Length)
            {
                if (!TryReadString(json, ref index))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
                if (!TryConsume(json, ref index, ':') ||
                    !TrySkipValue(json, ref index, depth + 1))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
                if (TryConsume(json, ref index, '}'))
                {
                    return true;
                }

                if (!TryConsume(json, ref index, ','))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
            }

            return false;
        }

        private static bool TrySkipArray(string json, ref int index, int depth)
        {
            if (depth >= MaxContainerDepth || !TryConsume(json, ref index, '['))
            {
                return false;
            }

            SkipWhitespace(json, ref index);
            if (TryConsume(json, ref index, ']'))
            {
                return true;
            }

            while (index < json.Length)
            {
                if (!TrySkipValue(json, ref index, depth + 1))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
                if (TryConsume(json, ref index, ']'))
                {
                    return true;
                }

                if (!TryConsume(json, ref index, ','))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
            }

            return false;
        }

        private static bool TrySkipPrimitive(string json, ref int index)
        {
            var start = index;
            while (index < json.Length)
            {
                var current = json[index];
                if (current == ',' || current == '}' || current == ']' || char.IsWhiteSpace(current))
                {
                    break;
                }

                index++;
            }

            if (index == start)
            {
                return false;
            }

            var value = json.Substring(start, index - start);
            return string.Equals(value, "true", StringComparison.Ordinal) ||
                   string.Equals(value, "false", StringComparison.Ordinal) ||
                   string.Equals(value, "null", StringComparison.Ordinal) ||
                   IsJsonNumber(value);
        }

        private static bool IsJsonNumber(string value)
        {
            var index = 0;
            if (index < value.Length && value[index] == '-')
            {
                index++;
            }

            if (index >= value.Length)
            {
                return false;
            }

            if (value[index] == '0')
            {
                index++;
            }
            else if (value[index] >= '1' && value[index] <= '9')
            {
                do
                {
                    index++;
                }
                while (index < value.Length && char.IsDigit(value[index]));
            }
            else
            {
                return false;
            }

            if (index < value.Length && value[index] == '.')
            {
                index++;
                var fractionStart = index;
                while (index < value.Length && char.IsDigit(value[index]))
                {
                    index++;
                }

                if (index == fractionStart)
                {
                    return false;
                }
            }

            if (index < value.Length && (value[index] == 'e' || value[index] == 'E'))
            {
                index++;
                if (index < value.Length && (value[index] == '+' || value[index] == '-'))
                {
                    index++;
                }

                var exponentStart = index;
                while (index < value.Length && char.IsDigit(value[index]))
                {
                    index++;
                }

                if (index == exponentStart)
                {
                    return false;
                }
            }

            return index == value.Length;
        }

        private static bool TryReadString(string json, ref int index)
        {
            if (!TryConsume(json, ref index, '"'))
            {
                return false;
            }

            while (index < json.Length)
            {
                var current = json[index];
                if (current == '"')
                {
                    index++;
                    return true;
                }

                if (current == '\\')
                {
                    index++;
                    if (index >= json.Length)
                    {
                        return false;
                    }

                    var escaped = json[index];
                    if (escaped == 'u')
                    {
                        if (index + 4 >= json.Length)
                        {
                            return false;
                        }

                        for (var i = 1; i <= 4; i++)
                        {
                            if (!Uri.IsHexDigit(json[index + i]))
                            {
                                return false;
                            }
                        }

                        index += 5;
                        continue;
                    }

                    if (escaped == '"' ||
                        escaped == '\\' ||
                        escaped == '/' ||
                        escaped == 'b' ||
                        escaped == 'f' ||
                        escaped == 'n' ||
                        escaped == 'r' ||
                        escaped == 't')
                    {
                        index++;
                        continue;
                    }

                    return false;
                }

                if (char.IsControl(current))
                {
                    return false;
                }

                index++;
            }

            return false;
        }

        private static bool TryConsume(string value, ref int index, char expected)
        {
            if (index >= value.Length || value[index] != expected)
            {
                return false;
            }

            index++;
            return true;
        }

        private static void SkipWhitespace(string value, ref int index)
        {
            while (index < value.Length && char.IsWhiteSpace(value[index]))
            {
                index++;
            }
        }
    }
}
