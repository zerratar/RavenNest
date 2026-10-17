using RavenNest.Models;
using System;
using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Net
{
    /// <summary>
    /// The game events waiting to go to one streamer's client. There is one queue per streamer, not one per
    /// connection: a client that reconnects gets what was waiting for it, events made while it was disconnected wait
    /// here instead of being parked where nothing reads them, and an event is never copied to two connections (which
    /// gave the client the same item twice). An event leaves the queue only once a send of it has been accepted.
    /// </summary>
    /// <remarks>
    /// Bounded, so a client that never comes back cannot grow it without end: past <see cref="MaxEvents"/> the oldest
    /// go, and an event that has waited longer than <see cref="MaxAge"/> is dropped rather than delivered late.
    /// </remarks>
    public sealed class PendingGameEvents
    {
        public const int MaxEvents = 10_000;
        public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(10);

        private readonly Queue<(GameEvent Event, DateTime Queued, long Sequence)> queue = new Queue<(GameEvent, DateTime, long)>();
        private long nextSequence;
        private readonly object sync = new object();

        /// <summary>How many were dropped, for the logs: too old, or over the cap.</summary>
        public long Dropped { get; private set; }

        public int Count
        {
            get { lock (sync) return queue.Count; }
        }

        public void Add(GameEvent gameEvent)
        {
            if (gameEvent == null)
            {
                return;
            }

            lock (sync)
            {
                while (queue.Count >= MaxEvents)
                {
                    queue.Dequeue();
                    Dropped++;
                }
                queue.Enqueue((gameEvent, DateTime.UtcNow, ++nextSequence));
            }
        }

        /// <summary>
        /// Up to <paramref name="max"/> of the oldest events, left in the queue until <see cref="RemoveThrough"/> with
        /// <paramref name="last"/> (the last one's place in the queue, so a removal never takes newer, unsent ones).
        /// </summary>
        public List<GameEvent> Peek(int max, out long last)
        {
            var result = new List<GameEvent>();
            last = 0;
            lock (sync)
            {
                var now = DateTime.UtcNow;
                while (queue.Count > 0 && now - queue.Peek().Queued > MaxAge)
                {
                    queue.Dequeue();
                    Dropped++;
                }

                foreach (var (gameEvent, _, sequence) in queue)
                {
                    if (result.Count >= max)
                    {
                        break;
                    }
                    result.Add(gameEvent);
                    last = sequence;
                }
            }
            return result;
        }

        /// <summary>Takes away every event up to and including <paramref name="last"/>, once they have been sent.</summary>
        public void RemoveThrough(long last)
        {
            lock (sync)
            {
                while (queue.Count > 0 && queue.Peek().Sequence <= last)
                {
                    queue.Dequeue();
                }
            }
        }
    }
}
