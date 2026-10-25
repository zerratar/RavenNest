using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Protocol;

namespace RavenNest.BusinessLogic.Net.V2
{
    /// <summary>
    /// Sends every change to a character's inventory to the game the character plays in, whichever code made it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Inventories were kept in step one command at a time: a command's result listed what it changed and dropped
    /// items came with their event, but anything else (the website, the marketplace, raid rewards, gifts from the
    /// old paths, ...) never reached the game, and its own sums drifted from the server's ("Phantom Ore 0/1" until
    /// the player left and joined again, October 2026). Patching each path in turn would never end.
    /// </para>
    /// <para>
    /// So this follows the inventory table itself (<see cref="GameData.InventoryItemChanged"/>): any stack added,
    /// changed or removed is noted, and twice a second each character with changes gets one
    /// <see cref="InventoryUpdate"/> event holding only those stacks, as they are now, with the next revision and the
    /// whole inventory's <see cref="InventoryChecksum"/>. Many changes to a stack in that time cost one stack in one
    /// message, so a busy player with thousands of items sends no more than the few stacks that moved. The events go
    /// through the session's event log, so they arrive in order and are replayed after a reconnect.
    /// </para>
    /// <para>
    /// The checksums let the game tell, without the inventory, whether its copy still matches: with every change,
    /// and now and then without one (<see cref="ChecksFor"/>, sent with the save acks). Only when it does not does the
    /// game fetch the whole inventory once (<see cref="Commands.InventoryGet"/>, <see cref="Snapshot"/>).
    /// </para>
    /// </remarks>
    public sealed class InventoryFeed : IDisposable
    {
        /// <summary>The event type of an inventory change with no game event; the game applies only its inventory.</summary>
        public const int ChangeEventType = -1;

        private const int FlushIntervalMs = 500;
        private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Where this server run starts numbering inventory revisions: from the clock, so a run always numbers above the
        /// last one. Events kept on disk from before a restart carry the old run's numbers; starting each run at 1
        /// made those look newer than the new run's, so the game skipped real changes. The game takes a jump of more
        /// than a million as a new server run rather than as missed changes.
        /// </summary>
        private static readonly long revisionBase = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;

        private readonly ConcurrentDictionary<Guid, long> revisions = new ConcurrentDictionary<Guid, long>();
        private readonly ConcurrentDictionary<Guid, Pending> pending = new ConcurrentDictionary<Guid, Pending>();
        private readonly ConcurrentDictionary<Guid, DateTime> lastCheck = new ConcurrentDictionary<Guid, DateTime>();
        private readonly GameData gameData;
        private readonly Func<Guid, GameSessionActor> sessionOfUser;
        private readonly ILogger logger;
        private readonly Timer timer;
        private int flushing;

        /// <summary>The stacks of one character changed since the last flush. Taken once the flush has it.</summary>
        private sealed class Pending
        {
            public readonly HashSet<Guid> Stacks = new HashSet<Guid>();
            public bool Taken;
        }

        public InventoryFeed(GameData gameData, Func<Guid, GameSessionActor> sessionOfUser, ILogger logger)
        {
            this.gameData = gameData;
            this.sessionOfUser = sessionOfUser;
            this.logger = logger;
            gameData.InventoryItemChanged += OnChanged;
            timer = new Timer(_ => Flush(), null, FlushIntervalMs, FlushIntervalMs);
        }

        /// <summary>Updates sent, and the stacks in them, since the server started.</summary>
        public long UpdatesSent;
        public long StacksSent;

        /// <summary>The revision a character's inventory has reached: the last one sent.</summary>
        public long Current(Guid characterId) => revisions.TryGetValue(characterId, out var revision) ? revision : revisionBase;

        /// <summary>The next revision of a character's inventory.</summary>
        public long Next(Guid characterId) => revisions.AddOrUpdate(characterId, revisionBase + 1, (_, r) => r + 1);

        // on the thread that changed the stack: only noted here
        private void OnChanged(InventoryItem item)
        {
            if (item == null || item.CharacterId == Guid.Empty)
            {
                return;
            }

            while (true)
            {
                var changes = pending.GetOrAdd(item.CharacterId, _ => new Pending());
                lock (changes)
                {
                    if (changes.Taken)
                    {
                        continue; // the flush has this one already; the next goes into a new one
                    }
                    changes.Stacks.Add(item.Id);
                    return;
                }
            }
        }

        private void Flush()
        {
            // one flush at a time, so each character's revisions go to its session in order
            if (Interlocked.Exchange(ref flushing, 1) == 1)
            {
                return;
            }

            try
            {
                foreach (var characterId in pending.Keys)
                {
                    if (!pending.TryGetValue(characterId, out var changes))
                    {
                        continue;
                    }

                    Guid[] stacks;
                    lock (changes)
                    {
                        changes.Taken = true;
                        stacks = new Guid[changes.Stacks.Count];
                        changes.Stacks.CopyTo(stacks);
                    }
                    pending.TryRemove(new KeyValuePair<Guid, Pending>(characterId, changes));

                    // a character in no v2 session has no game to tell; it gets the whole inventory when it joins one
                    var session = SessionOf(characterId);
                    if (session == null)
                    {
                        continue;
                    }

                    var update = Describe(characterId, stacks);
                    session.AddEvent(ChangeEventType, null, characterId, update);
                    UpdatesSent++;
                    StacksSent += update.Stacks.Length;
                }
            }
            catch (Exception exc)
            {
                logger.LogError("Inventory feed flush failed: " + exc);
            }
            finally
            {
                Volatile.Write(ref flushing, 0);
            }
        }

        private GameSessionActor SessionOf(Guid characterId)
        {
            var character = gameData.GetCharacter(characterId);
            if (character?.UserIdLock == null)
            {
                return null;
            }

            var session = sessionOfUser(character.UserIdLock.Value);
            return session == null || session.Ended ? null : session;
        }

        /// <summary>The stacks as they are now (gone ones with amount 0), with the next revision and the checksum.</summary>
        private InventoryUpdate Describe(Guid characterId, Guid[] stackIds)
        {
            var stacks = new InventoryStack[stackIds.Length];
            for (var i = 0; i < stackIds.Length; ++i)
            {
                var item = gameData.GetInventoryItem(stackIds[i]);
                stacks[i] = item == null || item.CharacterId != characterId || item.Amount.GetValueOrDefault() <= 0
                    ? new InventoryStack { Id = stackIds[i], ItemId = item?.ItemId ?? Guid.Empty, Amount = 0 }
                    : ToStack(item);
            }

            return new InventoryUpdate
            {
                CharacterId = characterId,
                Revision = Next(characterId),
                Stacks = stacks,
                Checksum = ChecksumOf(characterId)
            };
        }

        /// <summary>
        /// The character's whole inventory, at least as new as the revision it carries (changes not yet sent are in
        /// it too; they follow again in the next update, which does no harm).
        /// </summary>
        public InventoryUpdate Snapshot(Guid characterId)
        {
            var revision = Current(characterId);
            var items = gameData.GetAllPlayerItems(characterId);
            var stacks = new List<InventoryStack>(items.Count);
            ulong checksum = 0;
            foreach (var item in items)
            {
                if (item.Amount.GetValueOrDefault() <= 0)
                {
                    continue;
                }
                stacks.Add(ToStack(item));
                checksum += InventoryChecksum.Of(item.Id, item.ItemId, item.Amount.GetValueOrDefault());
            }

            return new InventoryUpdate { CharacterId = characterId, Revision = revision, Full = true, Stacks = stacks.ToArray(), Checksum = checksum };
        }

        /// <summary>A character's whole inventory for the game it plays in; null for a character of another session.</summary>
        public InventoryUpdate SnapshotFor(RavenNest.Models.SessionToken token, Guid characterId)
        {
            var character = gameData.GetCharacter(characterId);
            return character != null && token != null && character.UserIdLock == token.UserId ? Snapshot(characterId) : null;
        }

        /// <summary>
        /// Checks for the characters that have not had one for <see cref="CheckEvery"/> and have no changes waiting
        /// (whose checksum would be ahead of their revision). Null when none is due.
        /// </summary>
        public InventoryCheck[] ChecksFor(IEnumerable<Guid> characterIds, DateTime now)
        {
            List<InventoryCheck> checks = null;
            foreach (var characterId in characterIds)
            {
                if (characterId == Guid.Empty || pending.ContainsKey(characterId)
                    || (lastCheck.TryGetValue(characterId, out var last) && now - last < CheckEvery))
                {
                    continue;
                }

                lastCheck[characterId] = now;
                (checks ??= new List<InventoryCheck>()).Add(new InventoryCheck
                {
                    CharacterId = characterId,
                    Revision = Current(characterId),
                    Checksum = ChecksumOf(characterId)
                });
            }
            return checks?.ToArray();
        }

        private ulong ChecksumOf(Guid characterId)
        {
            ulong checksum = 0;
            foreach (var item in gameData.GetAllPlayerItems(characterId))
            {
                checksum += InventoryChecksum.Of(item.Id, item.ItemId, item.Amount.GetValueOrDefault());
            }
            return checksum;
        }

        private static InventoryStack ToStack(InventoryItem item)
        {
            return new InventoryStack
            {
                Id = item.Id,
                ItemId = item.ItemId,
                Amount = item.Amount.GetValueOrDefault(),
                Equipped = item.Equipped,
                Soulbound = item.Soulbound,
                Name = item.Name,
                Enchantment = item.Enchantment,
                Tag = item.Tag,
                TransmogrificationId = item.TransmogrificationId,
                Flags = item.Flags ?? 0
            };
        }

        public void Dispose()
        {
            timer.Dispose();
            gameData.InventoryItemChanged -= OnChanged;
        }
    }
}
