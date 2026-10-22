using System;
using System.Collections.Generic;

namespace Tileworks.Kit.Core
{
    public delegate void MessageHandler<T>(in T message) where T : struct;

    public interface IMessageBus
    {
        void Publish<T>(in T message) where T : struct;

        IDisposable Subscribe<T>(MessageHandler<T> handler) where T : struct;
    }

    public sealed class MessageBus : IMessageBus
    {
        public const int MaxDepth = 8;

        private readonly Dictionary<Type, object> _channels = new Dictionary<Type, object>(32);
        private int _depth;

        public void Publish<T>(in T message) where T : struct
        {
            if (!_channels.TryGetValue(typeof(T), out var boxed)) return;
            var handlers = ((Channel<T>)boxed).Handlers;
            if (handlers.Length == 0) return;

            if (++_depth > MaxDepth)
            {
                _depth = 0;
                throw new InvalidOperationException(
                    $"Message recursion deeper than {MaxDepth} while publishing {typeof(T).Name}.");
            }

            try
            {
                for (var i = 0; i < handlers.Length; i++) handlers[i](in message);
            }
            finally
            {
                _depth--;
            }
        }

        public IDisposable Subscribe<T>(MessageHandler<T> handler) where T : struct
        {
            Guard.NotNull(handler, nameof(handler));
            if (!_channels.TryGetValue(typeof(T), out var boxed))
            {
                boxed = new Channel<T>();
                _channels.Add(typeof(T), boxed);
            }

            var channel = (Channel<T>)boxed;
            channel.Add(handler);
            return new Disposable(() => channel.Remove(handler));
        }

        public int SubscriberCount<T>() where T : struct =>
            _channels.TryGetValue(typeof(T), out var boxed) ? ((Channel<T>)boxed).Handlers.Length : 0;

        // Copy-on-write, so handlers may unsubscribe during Publish.
        private sealed class Channel<T> where T : struct
        {
            public MessageHandler<T>[] Handlers = Array.Empty<MessageHandler<T>>();

            public void Add(MessageHandler<T> handler)
            {
                var next = new MessageHandler<T>[Handlers.Length + 1];
                Array.Copy(Handlers, next, Handlers.Length);
                next[Handlers.Length] = handler;
                Handlers = next;
            }

            public void Remove(MessageHandler<T> handler)
            {
                var index = Array.IndexOf(Handlers, handler);
                if (index < 0) return;
                var next = new MessageHandler<T>[Handlers.Length - 1];
                Array.Copy(Handlers, 0, next, 0, index);
                Array.Copy(Handlers, index + 1, next, index, Handlers.Length - index - 1);
                Handlers = next;
            }
        }
    }
}
