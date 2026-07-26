using System;
using System.Collections.Generic;
using LuminUI.Event;
using LuminUI.Internal;

namespace LuminUI
{
    /// <summary>
    /// Lifecycle and subscription owner used by generated Reaction wiring.
    /// Hide keeps the Reaction attached; close, pooling, and unmount detach it.
    /// </summary>
    public abstract class LuminReaction
    {
        private List<SubscriptionEntry>? _subscriptions;
        private List<(object handler, Action<object> unsubscribe)>? _eventSubscriptions;
        private int _nextSubscriptionId;

        public bool IsAttached { get; private set; }

        protected virtual void OnBind() { }
        protected virtual void OnUnbind() { }

        public void __Attach(LuminView view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (IsAttached)
                throw new InvalidOperationException("[LuminUI] Reaction is already attached.");

            __SetView(view);
            IsAttached = true;
            try
            {
                OnBind();
            }
            catch
            {
                FlushSubscriptions();
                IsAttached = false;
                __ClearView();
                throw;
            }
        }

        public void __Detach()
        {
            if (!IsAttached) return;
            try
            {
                OnUnbind();
            }
            finally
            {
                FlushSubscriptions();
                IsAttached = false;
                __ClearView();
            }
        }

        internal abstract void __SetView(LuminView view);
        internal abstract void __ClearView();

        protected SubscriptionHandle Subscribe<T>(
            IReadOnlyReactiveProperty<T> property, Action<T> onChanged, bool pushCurrent = true)
        {
            if (property == null) throw new ArgumentNullException(nameof(property));
            if (onChanged == null) throw new ArgumentNullException(nameof(onChanged));
            EnsureAttached();

            int id = TrackSubscription(property, onChanged, PropHelper<T>.Do);
            try
            {
                var observer = property as IReactivePropertyObserver<T>
                    ?? throw new ArgumentException(
                        "Reactive property does not support LuminUI subscriptions.", nameof(property));
                if (pushCurrent) observer.Subscribe(onChanged);
                else observer.SubscribeNoPush(onChanged);
            }
            catch
            {
                __Unsubscribe(id);
                throw;
            }
            return new SubscriptionHandle(this, id);
        }

        protected SubscriptionHandle Subscribe<T>(
            IReadOnlyReactiveCollection<T> collection,
            Action<int, T> added,
            Action<int, T> removed,
            Action<int, T, T> replaced,
            Action<int, int, T> moved,
            Action cleared)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (added == null) throw new ArgumentNullException(nameof(added));
            if (removed == null) throw new ArgumentNullException(nameof(removed));
            if (replaced == null) throw new ArgumentNullException(nameof(replaced));
            if (moved == null) throw new ArgumentNullException(nameof(moved));
            if (cleared == null) throw new ArgumentNullException(nameof(cleared));
            EnsureAttached();
            if (collection is not IReactiveCollectionObserver<T> observer)
                throw new ArgumentException(
                    "Reactive collection does not support LuminUI subscriptions.", nameof(collection));

            var subscription = new CollectionSubscription<T>(
                observer, added, removed, replaced, moved, cleared);
            int id = TrackSubscription(subscription, subscription, CollectionSubHelper<T>.Do);
            try { subscription.Start(); }
            catch
            {
                __Unsubscribe(id);
                throw;
            }
            return new SubscriptionHandle(this, id);
        }

        protected SubscriptionHandle Subscribe<TKey, TValue>(
            IReadOnlyReactiveDictionary<TKey, TValue> dictionary,
            Action<TKey, TValue> added,
            Action<TKey, TValue> removed,
            Action<TKey, TValue, TValue> replaced,
            Action cleared) where TKey : notnull
        {
            if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));
            if (added == null) throw new ArgumentNullException(nameof(added));
            if (removed == null) throw new ArgumentNullException(nameof(removed));
            if (replaced == null) throw new ArgumentNullException(nameof(replaced));
            if (cleared == null) throw new ArgumentNullException(nameof(cleared));
            EnsureAttached();
            if (dictionary is not IReactiveDictionaryObserver<TKey, TValue> observer)
                throw new ArgumentException(
                    "Reactive dictionary does not support LuminUI subscriptions.", nameof(dictionary));

            var subscription = new DictionarySubscription<TKey, TValue>(
                observer, added, removed, replaced, cleared);
            int id = TrackSubscription(subscription, subscription,
                DictionarySubHelper<TKey, TValue>.Do);
            try { subscription.Start(); }
            catch
            {
                __Unsubscribe(id);
                throw;
            }
            return new SubscriptionHandle(this, id);
        }

        protected void Unsubscribe(SubscriptionHandle handle) => handle.Dispose();

        protected void Unsubscribe(ref SubscriptionHandle handle)
        {
            handle.Dispose();
            handle = default;
        }

        protected void Listen<T>(Action<T> handler) where T : struct
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            EnsureAttached();
            _eventSubscriptions ??= new List<(object, Action<object>)>();
            _eventSubscriptions.Add((handler, UnsubHelper<T>.Do));
            EventBus.Subscribe(handler);
        }

        internal bool __IsSubscriptionActive(int id)
        {
            if (id == 0 || _subscriptions == null) return false;
            for (int i = 0; i < _subscriptions.Count; i++)
            {
                var subscription = _subscriptions[i];
                if (subscription.Id == id) return subscription.IsActive;
            }
            return false;
        }

        internal bool __Unsubscribe(int id)
        {
            if (id == 0 || _subscriptions == null) return false;
            for (int i = 0; i < _subscriptions.Count; i++)
            {
                var subscription = _subscriptions[i];
                if (subscription.Id != id || !subscription.IsActive) continue;
                _subscriptions[i] = default;
                subscription.Unsubscribe!(subscription.Source!, subscription.Handler!);
                return true;
            }
            return false;
        }

        private void EnsureAttached()
        {
            if (!IsAttached)
                throw new InvalidOperationException(
                    "[LuminUI] Subscriptions require an attached Reaction.");
        }

        private int NextSubscriptionId()
        {
            unchecked { _nextSubscriptionId++; }
            if (_nextSubscriptionId == 0) _nextSubscriptionId = 1;
            return _nextSubscriptionId;
        }

        private int TrackSubscription(
            object source, object handler, Action<object, object> unsubscribe)
        {
            _subscriptions ??= new List<SubscriptionEntry>();
            int id = NextSubscriptionId();
            var entry = new SubscriptionEntry
            {
                Id = id,
                Source = source,
                Handler = handler,
                Unsubscribe = unsubscribe
            };

            for (int i = 0; i < _subscriptions.Count; i++)
            {
                if (_subscriptions[i].IsActive) continue;
                _subscriptions[i] = entry;
                return id;
            }
            _subscriptions.Add(entry);
            return id;
        }

        private void FlushSubscriptions()
        {
            if (_subscriptions != null)
            {
                for (int i = 0; i < _subscriptions.Count; i++)
                {
                    var subscription = _subscriptions[i];
                    if (!subscription.IsActive) continue;
                    subscription.Unsubscribe!(subscription.Source!, subscription.Handler!);
                }
                _subscriptions.Clear();
            }

            if (_eventSubscriptions != null)
            {
                for (int i = 0; i < _eventSubscriptions.Count; i++)
                {
                    var subscription = _eventSubscriptions[i];
                    subscription.unsubscribe(subscription.handler);
                }
                _eventSubscriptions.Clear();
            }
        }

        private struct SubscriptionEntry
        {
            public int Id;
            public object? Source;
            public object? Handler;
            public Action<object, object>? Unsubscribe;
            public bool IsActive => Source != null;
        }
    }

    /// <summary>A strongly typed Reaction associated with one generated View.</summary>
    public abstract class LuminReaction<TView> : LuminReaction where TView : LuminView
    {
        protected TView View { get; private set; } = null!;

        internal override void __SetView(LuminView view)
            => View = (TView)view;

        internal override void __ClearView()
            => View = null!;
    }
}
