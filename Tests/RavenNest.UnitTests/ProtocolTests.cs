using System;
using System.Linq;
using MessagePack;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.Models;
using RavenNest.Protocol;

namespace RavenNest.UnitTests
{
    [TestClass]
    public class ProtocolTests
    {
        private static T RoundTrip<T>(T message) where T : class, IMessage
        {
            var bytes = ProtocolSerializer.Serialize(message);
            Assert.IsTrue(ProtocolSerializer.TryDeserialize(bytes, out var read));
            Assert.IsInstanceOfType(read, typeof(T));
            return (T)read;
        }

        [TestMethod]
        public void Hello_RoundTrips()
        {
            var resume = Guid.NewGuid();
            var read = RoundTrip(new Hello { ClientVersion = "0.9.5.0a", Token = "abc", ResumeId = resume, LastEventSequence = 42 });
            Assert.AreEqual(ProtocolInfo.Version, read.ProtocolVersion);
            Assert.AreEqual("0.9.5.0a", read.ClientVersion);
            Assert.AreEqual("abc", read.Token);
            Assert.AreEqual(resume, read.ResumeId);
            Assert.AreEqual(42, read.LastEventSequence);
        }

        [TestMethod]
        public void SaveBatch_RoundTrips_WithStateFieldsAndEmptyValues()
        {
            var id = Guid.NewGuid();
            var batch = new SaveBatch
            {
                EventAck = 7,
                Players = new[]
                {
                    new PlayerSave
                    {
                        CharacterId = id,
                        Version = 12,
                        Skills = new[] { new SkillSave { Index = 3, Level = 50, Experience = 1234.5, Gained = 10 } },
                        State = new PlayerStateSave
                        {
                            Changed = PlayerStateFields.TaskArgument | PlayerStateFields.Position | PlayerStateFields.AutoRestTarget,
                            TaskArgument = null,
                            X = 1.5f, Y = 2, Z = -3,
                            AutoRestTarget = null
                        }
                    }
                },
                GameState = new GameStateSave { PlayerCount = 3, Raid = new RaidInfo { IsActive = true, MaxBossHealth = 100 } }
            };

            var read = RoundTrip(batch);
            Assert.AreEqual(7, read.EventAck);
            var p = read.Players.Single();
            Assert.AreEqual(id, p.CharacterId);
            Assert.AreEqual(12, p.Version);
            Assert.AreEqual(1234.5, p.Skills[0].Experience);
            Assert.IsTrue(p.State.Has(PlayerStateFields.TaskArgument));
            Assert.IsNull(p.State.TaskArgument);
            Assert.IsFalse(p.State.Has(PlayerStateFields.Health));
            Assert.AreEqual(-3f, p.State.Z);
            Assert.IsTrue(read.GameState.Raid.IsActive);
            Assert.IsNull(read.GameState.Dungeon);
        }

        [TestMethod]
        public void LargeMessage_IsCompressed_AndRoundTrips()
        {
            var players = Enumerable.Range(0, 1000).Select(i => new PlayerSave
            {
                CharacterId = Guid.NewGuid(),
                Version = i,
                Skills = new[] { new SkillSave { Index = 1, Level = 10, Experience = i } }
            }).ToArray();

            var bytes = ProtocolSerializer.Serialize(new SaveBatch { Players = players });
            var uncompressed = MessagePackSerializer.Serialize<IMessage>(new SaveBatch { Players = players },
                ProtocolSerializer.Messages.WithCompression(MessagePackCompression.None));
            Assert.IsTrue(bytes.Length < uncompressed.Length, bytes.Length + " >= " + uncompressed.Length);

            Assert.IsTrue(ProtocolSerializer.TryDeserialize(bytes, out var read));
            Assert.AreEqual(1000, ((SaveBatch)read).Players.Length);
        }

        [TestMethod]
        public void UnknownMessageType_IsSkipped_NotAnError()
        {
            // [type, body] with a type number no message has (a newer peer)
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            writer.WriteArrayHeader(2);
            writer.Write(999);
            writer.WriteArrayHeader(1);
            writer.Write("future");
            writer.Flush();

            Assert.IsFalse(ProtocolSerializer.TryDeserialize(buffer.WrittenMemory, out var message));
            Assert.IsNull(message);
        }

        [TestMethod]
        public void BrokenMessage_Throws()
        {
            Assert.ThrowsException<MessagePackSerializationException>(() =>
                ProtocolSerializer.TryDeserialize(new byte[] { 0x92, 0x00 }, out _));
        }

        [TestMethod]
        public void CommandArgs_RoundTrip_AndMissingTrailingArgsAreDefault()
        {
            var c = Guid.NewGuid();
            var item = Guid.NewGuid();
            var data = CommandArgs.Pack(c, item, 5);

            CommandArgs.Unpack(data, out Guid a, out Guid b, out int n);
            Assert.AreEqual(c, a);
            Assert.AreEqual(item, b);
            Assert.AreEqual(5, n);

            CommandArgs.Unpack(CommandArgs.Pack(c), out Guid only, out string missing, out long missingNumber);
            Assert.AreEqual(c, only);
            Assert.IsNull(missing);
            Assert.AreEqual(0, missingNumber);
        }

        [TestMethod]
        public void CommandArgs_CarryRestModels()
        {
            var data = CommandArgs.Pack(Guid.NewGuid(), new InventoryItem { ItemId = Guid.NewGuid(), Amount = 3, Tag = "x" });
            CommandArgs.Unpack(data, out Guid _, out InventoryItem item);
            Assert.AreEqual(3, item.Amount);
            Assert.AreEqual("x", item.Tag);
        }

        [TestMethod]
        public void CommandResult_CarriesRestModelValue()
        {
            var inventoryItemId = Guid.NewGuid();
            var result = new CommandResult
            {
                RequestId = Guid.NewGuid(),
                Status = CommandStatus.Ok,
                Value = ProtocolSerializer.SerializeValue(new CraftItemResult { Status = CraftItemResultStatus.Success, Value = 2, InventoryItemId = inventoryItemId }),
                Inventory = new[]
                {
                    new InventoryUpdate
                    {
                        CharacterId = Guid.NewGuid(),
                        Revision = 41,
                        Stacks = new[] { new InventoryStack { Id = inventoryItemId, ItemId = Guid.NewGuid(), Amount = 0 } }
                    }
                }
            };

            var read = RoundTrip(result);
            var craft = ProtocolSerializer.DeserializeValue<CraftItemResult>(read.Value);
            Assert.AreEqual(CraftItemResultStatus.Success, craft.Status);
            Assert.AreEqual(inventoryItemId, craft.InventoryItemId);
            Assert.AreEqual(41, read.Inventory[0].Revision);
            Assert.AreEqual(0, read.Inventory[0].Stacks[0].Amount);
        }

        [TestMethod]
        public void EventBatch_RoundTrips()
        {
            var read = RoundTrip(new EventBatch
            {
                Events = new[] { new SessionEvent { Sequence = 1, Type = 5, Data = new byte[] { 1, 2, 3 } } }
            });
            Assert.AreEqual(1, read.Events[0].Sequence);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, read.Events[0].Data);
            Assert.IsNull(read.Events[0].Inventory);
        }
    }
}
