using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.BusinessLogic.Net.V2;

namespace RavenNest.UnitTests
{
    [TestClass]
    public class SessionEventLogTests
    {
        private string folder;

        [TestInitialize]
        public void Init()
        {
            folder = Path.Combine(Path.GetTempPath(), "ravennest-eventlog-tests", Guid.NewGuid().ToString("N"));
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { Directory.Delete(folder, true); } catch { }
        }

        [TestMethod]
        public void Events_AreNumberedFromOne_AndAckRemovesThem()
        {
            using var log = new SessionEventLog(folder, Guid.NewGuid());
            Assert.AreEqual(1, log.Add(5, new byte[] { 1 }, Guid.Empty).Sequence);
            Assert.AreEqual(2, log.Add(5, new byte[] { 2 }, Guid.Empty).Sequence);
            Assert.AreEqual(3, log.Add(5, new byte[] { 3 }, Guid.Empty).Sequence);

            log.Ack(2);
            Assert.AreEqual(1, log.Count);
            Assert.AreEqual(3, log.After(0, 10).Single().Sequence);
            Assert.AreEqual(2, log.AckedThrough);
        }

        [TestMethod]
        public void After_ReturnsOnlyNewerEvents_UpToMax()
        {
            using var log = new SessionEventLog(folder, Guid.NewGuid());
            for (var i = 0; i < 10; i++) log.Add(1, null, Guid.Empty);
            var batch = log.After(4, 3);
            CollectionAssert.AreEqual(new long[] { 5, 6, 7 }, batch.Select(x => x.Sequence).ToArray());
        }

        [TestMethod]
        public void Reload_KeepsUnconfirmedEvents_AndNumberingContinues()
        {
            var id = Guid.NewGuid();
            using (var log = new SessionEventLog(folder, id))
            {
                log.Add(7, new byte[] { 7 }, Guid.Empty);
                log.Add(8, new byte[] { 8 }, Guid.Empty);
                log.Add(9, new byte[] { 9 }, Guid.Empty);
                log.Ack(1);
                log.Flush();
            }

            using (var reloaded = new SessionEventLog(folder, id))
            {
                Assert.AreEqual(3, reloaded.LastSequence);
                Assert.AreEqual(1, reloaded.AckedThrough);
                var left = reloaded.After(0, 10);
                CollectionAssert.AreEqual(new long[] { 2, 3 }, left.Select(x => x.Sequence).ToArray());
                CollectionAssert.AreEqual(new byte[] { 8 }, left[0].Data);
                Assert.AreEqual(4, reloaded.Add(1, null, Guid.Empty).Sequence);
            }
        }

        [TestMethod]
        public void Reload_AfterEverythingConfirmed_NumberingStillContinues()
        {
            var id = Guid.NewGuid();
            using (var log = new SessionEventLog(folder, id))
            {
                log.Add(1, null, Guid.Empty);
                log.Add(1, null, Guid.Empty);
                log.Ack(2);
            }

            // twice, so the rewritten file (with no events left) is read back as well
            using (new SessionEventLog(folder, id)) { }
            using (var reloaded = new SessionEventLog(folder, id))
            {
                Assert.AreEqual(0, reloaded.Count);
                Assert.AreEqual(3, reloaded.Add(1, null, Guid.Empty).Sequence);
            }
        }

        [TestMethod]
        public void TornWriteAtTheEnd_KeepsWhatCameBefore()
        {
            var id = Guid.NewGuid();
            string path;
            using (var log = new SessionEventLog(folder, id))
            {
                log.Add(1, new byte[] { 1 }, Guid.Empty);
                log.Add(2, new byte[] { 2 }, Guid.Empty);
                path = log.FilePath;
            }

            using (var f = new FileStream(path, FileMode.Append))
            {
                f.Write(new byte[] { 200, 0, 0, 0, 1, 0x93 }); // a record that claims 199 bytes and stops
            }

            using var reloaded = new SessionEventLog(folder, id);
            Assert.AreEqual(2, reloaded.Count);
        }

        [TestMethod]
        public void ResumeId_SurvivesAReload_AndARewrite()
        {
            var id = Guid.NewGuid();
            var resume = Guid.NewGuid();
            using (var log = new SessionEventLog(folder, id))
            {
                log.Add(1, null, Guid.Empty);
                log.ResumeId = resume;
            }

            using (new SessionEventLog(folder, id)) { } // the reload rewrites the file
            using (var reloaded = new SessionEventLog(folder, id))
            {
                Assert.AreEqual(resume, reloaded.ResumeId);
            }
        }

        [TestMethod]
        public void Delete_RemovesTheFile()
        {
            var log = new SessionEventLog(folder, Guid.NewGuid());
            log.Add(1, null, Guid.Empty);
            var path = log.FilePath;
            log.Delete();
            Assert.IsFalse(File.Exists(path));
        }

        [TestMethod]
        public void Sweep_RemovesOldLogs_ButNotLiveOnes()
        {
            var live = Guid.NewGuid();
            var stale = Guid.NewGuid();
            string livePath, stalePath;
            using (var a = new SessionEventLog(folder, live)) livePath = a.FilePath;
            using (var b = new SessionEventLog(folder, stale)) stalePath = b.FilePath;
            File.SetLastWriteTimeUtc(livePath, DateTime.UtcNow.AddDays(-3));
            File.SetLastWriteTimeUtc(stalePath, DateTime.UtcNow.AddDays(-3));

            var removed = SessionEventLog.Sweep(folder, TimeSpan.FromDays(1), new HashSet<string> { live.ToString("N") });
            Assert.AreEqual(1, removed);
            Assert.IsTrue(File.Exists(livePath));
            Assert.IsFalse(File.Exists(stalePath));
        }

        [TestMethod]
        public void ManyAcks_CompactTheFile()
        {
            var id = Guid.NewGuid();
            using var log = new SessionEventLog(folder, id);
            var data = new byte[2000];
            for (var i = 0; i < 1000; i++)
            {
                log.Add(1, data, Guid.Empty);
                log.Ack(log.LastSequence);
            }
            log.Flush();
            Assert.IsTrue(new FileInfo(log.FilePath).Length < 1024 * 1024 + 4096, "the file should have been rewritten");
            Assert.AreEqual(1001, log.Add(1, null, Guid.Empty).Sequence);
        }
    }
}
