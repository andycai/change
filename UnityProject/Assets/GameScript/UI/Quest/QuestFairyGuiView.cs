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

            FillSideList(_root.GetChild("listSide").asList, snap.Sides);
            FillDailyList(_root.GetChild("listDaily").asList, snap.Dailies);

            var tabs = _root.GetController("tabs");
            if (tabs != null)
            {
                tabs.selectedIndex = model.ActiveTabIndex;
            }
        }

        private static void FillSideList(GList list, System.Collections.Generic.IReadOnlyList<SideQuestVm> rows)
        {
            ClearList(list);
            foreach (var row in rows)
            {
                var item = QuestUiRootBuilder.BuildQuestRowItem();
                list.AddChild(item);
                item.GetChild("title").asTextField.text = $"支线 {row.Id}";
                item.GetChild("progress").asTextField.text = $"{row.Progress}/{row.Target}";
                item.GetChild("btnClaim").asButton.touchable = row.CanClaim;
            }
        }

        private static void FillDailyList(GList list, System.Collections.Generic.IReadOnlyList<DailyQuestVm> rows)
        {
            ClearList(list);
            foreach (var row in rows)
            {
                var item = QuestUiRootBuilder.BuildQuestRowItem();
                list.AddChild(item);
                item.GetChild("title").asTextField.text = $"日常 {row.Id}";
                item.GetChild("progress").asTextField.text = $"{row.Progress}/{row.Target}";
                item.GetChild("btnClaim").asButton.touchable = row.CanClaim;
            }
        }

        private static void ClearList(GList list)
        {
            for (var i = list.numChildren - 1; i >= 0; i--)
            {
                var child = list.GetChildAt(i);
                list.RemoveChild(child, true);
            }
        }
    }
}
