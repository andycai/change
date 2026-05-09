using GameScript.UI.Quest;
using NUnit.Framework;

namespace GameScript.Tests.Quest
{
    public sealed class QuestUiRootBuilderTests
    {
        [Test]
        public void BuildRoot_HasExpectedHierarchy()
        {
            var root = QuestUiRootBuilder.BuildRoot();
            Assert.NotNull(root.GetChild("txtWallet"));
            Assert.NotNull(root.GetChild("txtMainTitle"));
            Assert.NotNull(root.GetChild("txtMainProgress"));
            Assert.NotNull(root.GetChild("listSide"));
            Assert.NotNull(root.GetChild("listDaily"));
            Assert.NotNull(root.GetController("tabs"));
        }

        [Test]
        public void BuildQuestRowItem_HasTitleProgressClaim()
        {
            var row = QuestUiRootBuilder.BuildQuestRowItem();
            Assert.NotNull(row.GetChild("title"));
            Assert.NotNull(row.GetChild("progress"));
            Assert.NotNull(row.GetChild("btnClaim"));
        }
    }
}
