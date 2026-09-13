using System;
using Game.Feature.Gameplay.UIAccess.Contracts;
#if VECTORQUAKE_CAPTURE_BUILD
using Game.Shared.Diagnostics;
#endif

namespace Game.Feature.UI.Application
{
    internal sealed class HudQueryCache<T>
    {
        private readonly IGameplayHudRevisionedQuery<T> _revisioned;
        private readonly Func<T> _read;
        internal object Query { get; }
        private GameplayHudQueryRead<T> _cached;
        private bool _valid;
        private long _readEpoch;
#if VECTORQUAKE_CAPTURE_BUILD
        internal UiCallbackSection CaptureSection;
#endif
        internal HudQueryCache(object query, Func<T> read)
        { Query = query; _revisioned = query as IGameplayHudRevisionedQuery<T>; _read = read; }
        internal void Invalidate() { _valid = false; _readEpoch++; }
        internal GameplayHudQueryRead<T> Read(bool force = false)
        {
            GameplayHudQueryStamp stamp = default;
            bool supported;
            try { supported = _revisioned != null && _revisioned.TryGetRevision(out stamp); }
            catch { Invalidate(); throw; }
            if (!force && supported && _valid && _cached.Stamp.Equals(stamp))
            {
                return _cached;
            }
            var epoch = ++_readEpoch;
            _valid = false;
#if VECTORQUAKE_CAPTURE_BUILD
            using var capture = UiCallbackCapture.Measure(CaptureSection);
#endif
            var result = supported ? _revisioned.ReadWithRevision() :
                new GameplayHudQueryRead<T>(_read(), default, false);
            if (epoch == _readEpoch && result.CanReuse &&
                _revisioned.TryGetRevision(out var after) && result.Stamp.Equals(after))
            {
                _cached = result;
                _valid = true;
                return result;
            }
            return new GameplayHudQueryRead<T>(result.Value, result.Stamp, false);
        }
    }
}
