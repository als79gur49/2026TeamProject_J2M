using System;

namespace Game.Product.Achievements.Composition
{
    internal sealed class SwitchableAchievementPublicationSink : IAchievementPublicationSink
    {
        private readonly object _gate = new();
        private readonly IAchievementPublicationSink _unavailable =
            new UnavailableAchievementPublicationSink();

        private IAchievementPublicationSink _target;
        private object _sessionIdentity;

        internal SwitchableAchievementPublicationSink(
            IAchievementPublicationSink initialTarget = null)
        {
            _target = initialTarget ?? _unavailable;
            if (initialTarget != null)
            {
                _sessionIdentity = this;
            }
        }

        internal bool IsUnavailable
        {
            get
            {
                lock (_gate)
                {
                    return ReferenceEquals(_target, _unavailable);
                }
            }
        }

        internal bool TryAttach(
            object sessionIdentity,
            IAchievementPublicationSink target)
        {
            if (sessionIdentity == null)
            {
                throw new ArgumentNullException(nameof(sessionIdentity));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            lock (_gate)
            {
                if (_sessionIdentity != null)
                {
                    return ReferenceEquals(_sessionIdentity, sessionIdentity) &&
                           ReferenceEquals(_target, target);
                }

                _sessionIdentity = sessionIdentity;
                _target = target;
                return true;
            }
        }

        internal bool TryDetach(
            object sessionIdentity,
            IAchievementPublicationSink target)
        {
            if (sessionIdentity == null || target == null)
            {
                return false;
            }

            lock (_gate)
            {
                if (!ReferenceEquals(_sessionIdentity, sessionIdentity) ||
                    !ReferenceEquals(_target, target))
                {
                    return false;
                }

                _sessionIdentity = null;
                _target = _unavailable;
                return true;
            }
        }

        public void PublishBatch(
            AchievementPublicationBatch batch,
            Action<AchievementPublicationBatchResult> completed)
        {
            if (batch == null)
            {
                throw new ArgumentNullException(nameof(batch));
            }

            if (completed == null)
            {
                throw new ArgumentNullException(nameof(completed));
            }

            IAchievementPublicationSink snapshot;
            lock (_gate)
            {
                snapshot = _target;
            }

            snapshot.PublishBatch(batch, completed);
        }
    }
}
