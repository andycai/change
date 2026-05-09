using FairyGUI;

namespace GameScript.UI.Quest
{
    public sealed class QuestFairyGuiView : IQuestWindowView
    {
        private readonly GComponent _root;

        public QuestFairyGuiView(GComponent root)
        {
            _root = root;
        }

        public void Apply(in QuestWindowViewModel model)
        {
            var snap = model.Snapshot;
            _root.GetChild("txtWallet").asTextField.text = snap.WalletGold.ToString();
            _root.GetChild("txtMainTitle").asTextField.text =
                snap.Main.Completed ? "主线已完成" : $"主线 第{snap.Main.Index + 1}步";
            _root.GetChild("txtMainProgress").asTextField.text =
                snap.Main.Completed ? "-" : $"{snap.Main.Progress}/{snap.Main.Target}";

            var listSide = _root.GetChild("listSide").asList;
            listSide.RemoveChildrenToPool();
            foreach (var row in snap.Sides)
            {
                var item = listSide.AddItemFromPool().asCom;
                item.GetChild("title").asTextField.text = $"支线 {row.Id}";
                item.GetChild("progress").asTextField.text = $"{row.Progress}/{row.Target}";
                item.GetChild("btnClaim").asButton.touchable = row.CanClaim;
            }

            var listDaily = _root.GetChild("listDaily").asList;
            listDaily.RemoveChildrenToPool();
            foreach (var row in snap.Dailies)
            {
                var item = listDaily.AddItemFromPool().asCom;
                item.GetChild("title").asTextField.text = $"日常 {row.Id}";
                item.GetChild("progress").asTextField.text = $"{row.Progress}/{row.Target}";
                item.GetChild("btnClaim").asButton.touchable = row.CanClaim;
            }

            var tabs = _root.GetController("tabs");
            if (tabs != null)
            {
                tabs.selectedIndex = model.ActiveTabIndex;
            }
        }
    }
}
