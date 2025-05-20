using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic
{
    public interface IMessageBusSubscription : IDisposable
    {
        void Send(object item);
        void Unsubscribe();
    }

    public interface IAsyncMessageBusSubscription : IMessageBusSubscription
    {
    }

    public interface IMessageBus
    {
        IMessageBusSubscription Subscribe(string key, Action handler);
        IMessageBusSubscription Subscribe<T>(string key, Action<T> handler);
        IAsyncMessageBusSubscription SubscribeAsync<T>(string key, Func<T, Task> handler);

        void Unsubscribe(IMessageBusSubscription subscription);
        void UnsubscribeAll();

        void Send(string key);
        void Send<T>(string key, T message);
        void DelayedSend<T>(string key, T message, int delayMilliseconds, CancellationToken token);
    }

    public class MessageBus : IMessageBus
    {
        private readonly ConcurrentDictionary<string, List<MessageBusSubscription>> subscriptions = new ConcurrentDictionary<string, List<MessageBusSubscription>>();

        public static MessageBus Shared { get; } = new MessageBus();

        public MessageBus()
        {
        }

        public async void DelayedSend<T>(string key, T message, int delayMilliseconds, CancellationToken token)
        {
            try
            {
                await Task.Delay(delayMilliseconds, token);
                if (token.IsCancellationRequested)
                {
                    return;
                }
            }
            catch (TaskCanceledException)
            {
                return;
            }

            Send(key, message);
        }

        public IAsyncMessageBusSubscription SubscribeAsync<T>(string key, Func<T, Task> handler)
        {
            var sub = new AsyncMessageBusSubscription<T>(this, handler);
            if (!subscriptions.TryGetValue(key, out var subs))
            {
                subscriptions[key] = subs = new List<MessageBusSubscription>();
            }
            subs.Add(sub);
            return sub;
        }

        public IMessageBusSubscription Subscribe<T>(string key, Action<T> handler)
        {
            var sub = new MessageBusSubscription<T>(this, handler);
            if (!subscriptions.TryGetValue(key, out var subs))
            {
                subscriptions[key] = subs = new List<MessageBusSubscription>();
            }
            subs.Add(sub);
            return sub;
        }

        public IMessageBusSubscription Subscribe(string key, Action handler)
        {
            var sub = new ActionMessageBusSubscription(this, handler);
            if (!subscriptions.TryGetValue(key, out var subs))
            {
                subscriptions[key] = subs = new List<MessageBusSubscription>();
            }
            subs.Add(sub);
            return sub;
        }

        public void Unsubscribe(IMessageBusSubscription subscription)
        {
            if (subscription is not MessageBusSubscription s)
            {
                return;
            }

            foreach (var sub in subscriptions.Values)
            {
                sub.Remove(s);
            }
        }

        public void Send(string key)
        {
            if (subscriptions.TryGetValue(key, out var subs))
            {
                foreach (var sub in subs)
                {
                    sub.Send(null);
                }
            }
        }

        public void Send<T>(string key, T message)
        {
            if (subscriptions.TryGetValue(key, out var subs))
            {
                foreach (var sub in subs)
                {
                    sub.Send(message);
                }
            }
        }

        public void UnsubscribeAll()
        {
            foreach (var sub in subscriptions.Values)
            {
                sub.Clear();
            }
        }

        private abstract class MessageBusSubscription : IMessageBusSubscription
        {
            public abstract void Unsubscribe();

            public abstract void Send(object item);

            public void Dispose()
            {
                Unsubscribe();
            }
        }

        private class AsyncMessageBusSubscription<T> : MessageBusSubscription,
            IAsyncMessageBusSubscription
        {
            private readonly MessageBus messageBus;
            private Func<T, Task> handler;

            public AsyncMessageBusSubscription(MessageBus messageBus, Func<T, Task> handler)
            {
                this.messageBus = messageBus;
                this.handler = handler;
            }

            public override async void Send(object item)
            {
                if (item is T t)
                    await handler(t);
            }

            public override void Unsubscribe()
            {
                messageBus.Unsubscribe(this);
            }
        }

        private class MessageBusSubscription<T> : MessageBusSubscription
        {
            private readonly MessageBus messageBus;
            private Action<T> handler;

            public MessageBusSubscription(MessageBus messageBus, Action<T> handler)
            {
                this.messageBus = messageBus;
                this.handler = handler;
            }

            public override void Send(object item)
            {
                if (item is T t)
                    handler?.Invoke(t);
            }

            public override void Unsubscribe()
            {
                messageBus.Unsubscribe(this);
            }
        }

        private class ActionMessageBusSubscription : MessageBusSubscription
        {
            private readonly MessageBus messageBus;
            private Action handler;

            public ActionMessageBusSubscription(MessageBus messageBus, Action handler)
            {
                this.messageBus = messageBus;
                this.handler = handler;
            }

            public override void Send(object item)
            {
                handler?.Invoke();
            }

            public override void Unsubscribe()
            {
                messageBus.Unsubscribe(this);
            }
        }
    }
}
