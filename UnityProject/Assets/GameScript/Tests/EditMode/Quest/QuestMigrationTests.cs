using System;
using Change.Framework.Cqrs;
using GameScript.UI.Quest;
using NUnit.Framework;

namespace GameScript.Tests.Quest
{
    /// <summary>
    /// Verifies Quest module migration to self-handling struct commands/queries.
    /// </summary>
    public class QuestMigrationTests
    {
        [Test]
        public void Send_BumpMainQuestProgress_SelfHandling_UpdatesStateWithoutRegistration()
        {
            var state = new QuestSessionState();
            var bus = new CqrsBus();

            // 自处理：无需 RegisterCommand，直接 Send
            bus.Send(new BumpMainQuestProgressCommand(state, 1));

            Assert.AreEqual(1, state.MainProgress);
        }

        [Test]
        public void Send_BumpMainQuestProgress_HotPath_AllocatesZeroBytes()
        {
            var state = new QuestSessionState();
            var bus = new CqrsBus();
            var command = new BumpMainQuestProgressCommand(state, 1);

            // 预热
            for (var i = 0; i < 1000; i++) bus.Send(command);

            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            var before = GC.GetTotalMemory(true);
            for (var i = 0; i < 100000; i++) bus.Send(command);
            var after = GC.GetTotalMemory(true);

            Assert.AreEqual(before, after,
                "Quest BumpMain self-handling must be zero-allocation (no live-heap growth).");
        }
    }
}
