using System;
using Change.Framework.Cqrs;
using GameScript.UI.Quest;
using NUnit.Framework;

namespace GameScript.Tests.Quest
{
    /// <summary>
    /// 验证 Quest 模块从旧 Class handler 迁移到双模式后的正确性。
    /// 演示 Struct 自处理（BumpMainQuestProgressCommand → ISelfHandlingCommand）。
    /// </summary>
    public class QuestMigrationTests
    {
        [Test]
        public void Send_BumpMainQuestProgress_SelfHandling_UpdatesStateWithoutRegistration()
        {
            var state = new QuestSessionState();
            var bus = new CqrsBus();

            // 自处理：无需 RegisterCommand，直接 Send
            // 注意：BumpMainProgress 用 Math.Min(0 + delta, MainTarget=2) 封顶，故 delta=1 → progress=1
            bus.Send(new BumpMainQuestProgressCommand(state, 1));

            Assert.AreEqual(1, state.MainProgress);
        }

        // BumpMainQuestProgressCommand 现已是 ISelfHandlingCommand；
        // 注册任何 Class handler 都应触发 ModeConflictException。
        private sealed class DummyBumpMainHandler : ICommandHandler<BumpMainQuestProgressCommand>
        {
            public void Handle(in BumpMainQuestProgressCommand command) { }
        }

        [Test]
        public void RegisterCommand_OnBumpMainQuestProgress_NowThrowsModeConflict()
        {
            var bus = new CqrsBus();

            Assert.Throws<ModeConflictException>(
                () => bus.RegisterCommand(new DummyBumpMainHandler()));
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
