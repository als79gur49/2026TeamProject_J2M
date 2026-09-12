using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Game.Platform.Steam;

namespace Game.Exhibition.Integration
{
    /// <summary>One owned reset attempt. All external operations are injectable; no native calls live here.</summary>
    public sealed class SteamExhibitionResetProtocol
    {
        private readonly ISteamAchievementApi _api;
        private readonly Func<string, bool> _clear;
        private readonly Action _validateIdentity;
        private readonly string[] _names;
        private readonly uint _appId;
        private readonly Action _markFailed;
        private readonly Action _release;
        private readonly TimeSpan _timeout;
        private readonly TaskCompletionSource<SteamCallbackResult> _completion = new TaskCompletionSource<SteamCallbackResult>();
        private int _started;
        private bool _awaitingStore;

        public SteamExhibitionResetProtocol(ISteamAchievementApi api, Func<string, bool> clear,
            Action validateIdentity, string[] names, uint appId, Action markFailed,
            Action release, TimeSpan timeout)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _clear = clear ?? throw new ArgumentNullException(nameof(clear));
            _validateIdentity = validateIdentity ?? throw new ArgumentNullException(nameof(validateIdentity));
            _names = names == null ? throw new ArgumentNullException(nameof(names)) : (string[])names.Clone();
            if (_names.Length == 0) throw new ArgumentException("At least one mapped achievement is required.", nameof(names));
            _appId = appId;
            _markFailed = markFailed ?? throw new ArgumentNullException(nameof(markFailed));
            _release = release ?? throw new ArgumentNullException(nameof(release));
            if (timeout < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
            _timeout = timeout;
        }

        public void ObserveStatsStored(SteamStatsStoredObservation observation)
        {
            if (_awaitingStore && observation.AppId == _appId)
                _completion.TrySetResult(observation.Result);
        }

        public async Task RunAsync()
        {
            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
                throw new InvalidOperationException("A Steam reset session can only run once.");
            try
            {
                _validateIdentity();
                var available = new HashSet<string>(StringComparer.Ordinal);
                for (uint i = 0, count = _api.GetNumAchievements(); i < count; i++)
                    available.Add(_api.GetAchievementName(i));
                // Validate the entire mapping before the first destructive call.
                foreach (var name in _names)
                    if (string.IsNullOrWhiteSpace(name) || !available.Contains(name) || !_api.GetAchievement(name, out _))
                        throw new InvalidOperationException("Steam 업적 스키마를 읽을 수 없습니다: " + name);
                foreach (var name in _names)
                {
                    _validateIdentity();
                    if (!_clear(name)) throw new InvalidOperationException("Steam 업적 초기화에 실패했습니다.");
                }
                _validateIdentity();
                // Arm before StoreStats: native callbacks may arrive before the call returns.
                _awaitingStore = true;
                if (!_api.StoreStats()) throw new InvalidOperationException("Steam 업적 저장에 실패했습니다.");
                var timer = Stopwatch.StartNew();
                while (!_completion.Task.IsCompleted)
                {
                    _validateIdentity();
                    if (timer.Elapsed >= _timeout)
                        throw new TimeoutException("Steam 업적 저장을 확인할 수 없습니다. 연결 확인 후 재시작해 주세요.");
                    // Unity's canonical host pumps callbacks; this continuation keeps its captured context.
                    await Task.Delay(50);
                }
                _validateIdentity();
                if (await _completion.Task != SteamCallbackResult.Ok)
                    throw new InvalidOperationException("Steam에서 업적 초기화 저장을 확인할 수 없습니다.");
                foreach (var name in _names)
                    if (!_api.GetAchievement(name, out var achieved) || achieved)
                        throw new InvalidOperationException("Steam 업적 초기화 확인에 실패했습니다.");
            }
            catch
            {
                _markFailed();
                throw;
            }
            finally
            {
                _awaitingStore = false;
                _release();
            }
        }
    }
}
