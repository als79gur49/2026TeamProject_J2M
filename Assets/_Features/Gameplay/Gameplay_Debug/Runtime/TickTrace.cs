using System;

namespace Game.Feature.Gameplay.Debug
{
    public sealed class TickTrace
    {
        public static readonly TickTrace Empty = new(string.Empty);

        public TickTrace(string text)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        public string Text { get; }

        public override string ToString()
        {
            return Text;
        }
    }
}
