using System;

namespace LuminUI
{
    /// <summary>
    /// A lifecycle-owned reactive subscription. Disposing it stops listening early;
    /// an active handle is disposed automatically when its Reaction detaches.
    /// </summary>
    public readonly struct SubscriptionHandle : IDisposable
    {
        private readonly LuminReaction? _owner;
        private readonly int _id;

        internal SubscriptionHandle(LuminReaction owner, int id)
        {
            _owner = owner;
            _id = id;
        }

        public bool IsActive => _owner != null && _owner.__IsSubscriptionActive(_id);

        public void Dispose()
        {
            if (_owner != null) _owner.__Unsubscribe(_id);
        }
    }
}
