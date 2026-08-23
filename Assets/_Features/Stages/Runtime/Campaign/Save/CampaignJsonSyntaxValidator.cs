namespace Game.Feature.Stages
{
    internal static class CampaignJsonSyntaxValidator
    {
        public static bool IsValid(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            var index = 0;
            if (!TrySkipValue(json, ref index))
            {
                return false;
            }

            SkipWhitespace(json, ref index);
            return index == json.Length;
        }

        private static bool TrySkipValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length)
            {
                return false;
            }

            var current = json[index];
            if (current == '"')
            {
                return TrySkipString(json, ref index);
            }

            if (current == '{')
            {
                return TrySkipObject(json, ref index);
            }

            if (current == '[')
            {
                return TrySkipArray(json, ref index);
            }

            if (current == '-' || IsDigit(current))
            {
                return TrySkipNumber(json, ref index);
            }

            return TryConsumeLiteral(json, ref index, "true") ||
                   TryConsumeLiteral(json, ref index, "false") ||
                   TryConsumeLiteral(json, ref index, "null");
        }

        private static bool TrySkipObject(string json, ref int index)
        {
            index++;
            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == '}')
            {
                index++;
                return true;
            }

            while (index < json.Length)
            {
                if (!TrySkipString(json, ref index))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] != ':')
                {
                    return false;
                }

                index++;
                if (!TrySkipValue(json, ref index))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == '}')
                {
                    index++;
                    return true;
                }

                if (index >= json.Length || json[index] != ',')
                {
                    return false;
                }

                index++;
                SkipWhitespace(json, ref index);
            }

            return false;
        }

        private static bool TrySkipArray(string json, ref int index)
        {
            index++;
            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == ']')
            {
                index++;
                return true;
            }

            while (index < json.Length)
            {
                if (!TrySkipValue(json, ref index))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ']')
                {
                    index++;
                    return true;
                }

                if (index >= json.Length || json[index] != ',')
                {
                    return false;
                }

                index++;
            }

            return false;
        }

        private static bool TrySkipString(string json, ref int index)
        {
            if (index >= json.Length || json[index] != '"')
            {
                return false;
            }

            index++;
            while (index < json.Length)
            {
                var current = json[index++];
                if (current == '"')
                {
                    return true;
                }

                if (current == '\\')
                {
                    if (index >= json.Length)
                    {
                        return false;
                    }

                    var escaped = json[index++];
                    if (escaped == 'u')
                    {
                        for (var i = 0; i < 4; i++)
                        {
                            if (index >= json.Length || !IsHex(json[index++]))
                            {
                                return false;
                            }
                        }
                        continue;
                    }

                    if (escaped != '"' &&
                        escaped != '\\' &&
                        escaped != '/' &&
                        escaped != 'b' &&
                        escaped != 'f' &&
                        escaped != 'n' &&
                        escaped != 'r' &&
                        escaped != 't')
                    {
                        return false;
                    }

                    continue;
                }

                if (current < 0x20)
                {
                    return false;
                }
            }

            return false;
        }

        private static bool TrySkipNumber(string json, ref int index)
        {
            if (index < json.Length && json[index] == '-')
            {
                index++;
            }

            if (index >= json.Length)
            {
                return false;
            }

            if (json[index] == '0')
            {
                index++;
                if (index < json.Length && IsDigit(json[index]))
                {
                    return false;
                }
            }
            else if (json[index] >= '1' && json[index] <= '9')
            {
                do
                {
                    index++;
                }
                while (index < json.Length && IsDigit(json[index]));
            }
            else
            {
                return false;
            }

            if (index < json.Length && json[index] == '.')
            {
                index++;
                var fractionStart = index;
                while (index < json.Length && IsDigit(json[index]))
                {
                    index++;
                }

                if (index == fractionStart)
                {
                    return false;
                }
            }

            if (index < json.Length && (json[index] == 'e' || json[index] == 'E'))
            {
                index++;
                if (index < json.Length && (json[index] == '+' || json[index] == '-'))
                {
                    index++;
                }

                var exponentStart = index;
                while (index < json.Length && IsDigit(json[index]))
                {
                    index++;
                }

                if (index == exponentStart)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryConsumeLiteral(string json, ref int index, string literal)
        {
            if (index + literal.Length > json.Length ||
                string.CompareOrdinal(json, index, literal, 0, literal.Length) != 0)
            {
                return false;
            }

            index += literal.Length;
            return true;
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && IsWhitespace(json[index]))
            {
                index++;
            }
        }

        private static bool IsWhitespace(char value)
        {
            return value == ' ' || value == '\t' || value == '\r' || value == '\n';
        }

        private static bool IsDigit(char value)
        {
            return value >= '0' && value <= '9';
        }

        private static bool IsHex(char value)
        {
            return (value >= '0' && value <= '9') ||
                   (value >= 'a' && value <= 'f') ||
                   (value >= 'A' && value <= 'F');
        }
    }
}
