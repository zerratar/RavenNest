using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using MessagePack;
using RavenNest.Protocol;

namespace RavenNest.BusinessLogic.Net.V2
{
    /// <summary>
    /// The events of one v2 session that the game has not confirmed yet, numbered from 1 without gaps. Kept in
    /// memory and appended to a file of its own, so a server restart can replay them to a client that reconnects.
    /// </summary>
    /// <remarks>
    /// The file is a sequence of records, <c>[length:4][kind:1][body]</c>: an event (its <see cref="SessionEvent"/>)
    /// or an acknowledgement (the sequence it confirms). Acknowledgements are appended rather than rewriting the
    /// file; when most of the file is confirmed it is rewritten with only what is left. It never goes in the
    /// database, where events once piled up as rows with no good way to clean them.
    /// Not thread safe: its session's worker is the only one that uses it.
    /// </remarks>
    public sealed class SessionEventLog : IDisposable
    {
        /// <summary>Beyond this many unconfirmed events the oldest are dropped (a client that never confirms).</summary>
        public const int MaxEvents = 20_000;

        /// <summary>An unconfirmed event older than this is dropped.</summary>
        public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(30);

        private const byte EventRecord = 1;
        private const byte AckRecord = 2;
        private const long CompactAboveBytes = 1024 * 1024;

        private readonly LinkedList<(SessionEvent Event, DateTime Added)> events = new LinkedList<(SessionEvent, DateTime)>();
        private readonly string path;
        private FileStream file;

        public SessionEventLog(string folder, Guid sessionId)
        {
            Directory.CreateDirectory(folder);
            path = Path.Combine(folder, sessionId.ToString("N") + ".events");
            Load();
            file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.None);
        }

        /// <summary>The newest event's sequence, 0 before the first.</summary>
        public long LastSequence { get; private set; }

        /// <summary>Everything up to here is confirmed by the client.</summary>
        public long AckedThrough { get; private set; }

        public int Count => events.Count;

        /// <summary>Dropped without being confirmed (too many or too old), for the logs.</summary>
        public long Dropped { get; private set; }

        /// <summary>The file path, for deleting it when the session is over.</summary>
        public string FilePath => path;

        /// <summary>Numbers the event, keeps it until confirmed, and writes it to the file.</summary>
        public SessionEvent Add(int type, byte[] data, Guid characterId, InventoryUpdate inventory = null)
        {
            var e = new SessionEvent { Sequence = ++LastSequence, Type = type, Data = data, CharacterId = characterId, Inventory = inventory };
            events.AddLast((e, DateTime.UtcNow));
            Append(EventRecord, MessagePackSerializer.Serialize(e, ProtocolSerializer.Messages));
            while (events.Count > MaxEvents)
            {
                events.RemoveFirst();
                Dropped++;
            }
            return e;
        }

        /// <summary>The client applied everything up to <paramref name="sequence"/>.</summary>
        public void Ack(long sequence)
        {
            if (sequence <= AckedThrough)
            {
                return;
            }

            AckedThrough = Math.Min(sequence, LastSequence);
            while (events.First != null && events.First.Value.Event.Sequence <= AckedThrough)
            {
                events.RemoveFirst();
            }

            var body = new byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(body, AckedThrough);
            Append(AckRecord, body);

            if (file.Length > CompactAboveBytes && events.Count < 1000)
            {
                Compact();
            }
        }

        /// <summary>The unconfirmed events after <paramref name="sequence"/>, oldest first, without those too old to send.</summary>
        public List<SessionEvent> After(long sequence, int max)
        {
            DropExpired();
            var result = new List<SessionEvent>();
            foreach (var (e, _) in events)
            {
                if (e.Sequence <= sequence) continue;
                if (result.Count >= max) break;
                result.Add(e);
            }
            return result;
        }

        private void DropExpired()
        {
            var now = DateTime.UtcNow;
            while (events.First != null && now - events.First.Value.Added > MaxAge)
            {
                events.RemoveFirst();
                Dropped++;
            }
        }

        public void Flush()
        {
            file?.Flush();
        }

        private void Append(byte kind, byte[] body)
        {
            Span<byte> header = stackalloc byte[5];
            BinaryPrimitives.WriteInt32LittleEndian(header, body.Length + 1);
            header[4] = kind;
            file.Write(header);
            file.Write(body, 0, body.Length);
        }

        private void Load()
        {
            if (!File.Exists(path))
            {
                return;
            }

            var loaded = new List<SessionEvent>();
            long acked = 0;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var header = new byte[5];
                while (ReadExact(stream, header, 5))
                {
                    var length = BinaryPrimitives.ReadInt32LittleEndian(header) - 1;
                    if (length < 0 || length > ProtocolInfo.MaxMessageBytes)
                    {
                        break; // a torn write at the end: keep what came before
                    }

                    var body = new byte[length];
                    if (!ReadExact(stream, body, length))
                    {
                        break;
                    }

                    try
                    {
                        if (header[4] == EventRecord)
                        {
                            loaded.Add(MessagePackSerializer.Deserialize<SessionEvent>(body, ProtocolSerializer.Messages));
                        }
                        else if (header[4] == AckRecord && length == 8)
                        {
                            acked = Math.Max(acked, BinaryPrimitives.ReadInt64LittleEndian(body));
                        }
                    }
                    catch (MessagePackSerializationException)
                    {
                        break;
                    }
                }
            }

            // the file's time stands in for when they were added: a log left from before a restart is fresh enough
            var added = File.GetLastWriteTimeUtc(path);
            foreach (var e in loaded)
            {
                LastSequence = Math.Max(LastSequence, e.Sequence);
                if (e.Sequence > acked)
                {
                    events.AddLast((e, added));
                }
            }
            AckedThrough = Math.Min(acked, LastSequence);

            // start the file over with only what is left
            WriteFresh();
        }

        private void Compact()
        {
            file.Dispose();
            WriteFresh();
            file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.None);
        }

        private void WriteFresh()
        {
            var temp = path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var previous = file;
                file = stream;
                try
                {
                    var ack = new byte[8];
                    BinaryPrimitives.WriteInt64LittleEndian(ack, AckedThrough);
                    // the sequence must survive even when every event is confirmed, so numbering carries on
                    var last = new SessionEvent { Sequence = LastSequence, Type = -1 };
                    if (events.Count == 0 && LastSequence > 0)
                    {
                        Append(EventRecord, MessagePackSerializer.Serialize(last, ProtocolSerializer.Messages));
                    }
                    Append(AckRecord, ack);
                    foreach (var (e, _) in events)
                    {
                        Append(EventRecord, MessagePackSerializer.Serialize(e, ProtocolSerializer.Messages));
                    }
                }
                finally
                {
                    file = previous;
                }
            }
            File.Move(temp, path, overwrite: true);
        }

        private static bool ReadExact(Stream stream, byte[] buffer, int count)
        {
            var read = 0;
            while (read < count)
            {
                var n = stream.Read(buffer, read, count - read);
                if (n <= 0) return false;
                read += n;
            }
            return true;
        }

        public void Dispose()
        {
            file?.Dispose();
            file = null;
        }

        /// <summary>Removes the session's file, once the session is over.</summary>
        public void Delete()
        {
            Dispose();
            try { File.Delete(path); } catch (IOException) { }
        }

        /// <summary>Removes logs nobody came back for: older than <paramref name="age"/> and not a live session's.</summary>
        public static int Sweep(string folder, TimeSpan age, ISet<string> liveSessionIds)
        {
            if (!Directory.Exists(folder))
            {
                return 0;
            }

            var removed = 0;
            var cutoff = DateTime.UtcNow - age;
            foreach (var f in Directory.EnumerateFiles(folder, "*.events*"))
            {
                try
                {
                    var name = Path.GetFileName(f);
                    var id = name.Substring(0, Math.Max(0, name.IndexOf('.')));
                    if (!liveSessionIds.Contains(id) && File.GetLastWriteTimeUtc(f) < cutoff)
                    {
                        File.Delete(f);
                        removed++;
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return removed;
        }
    }
}
