using FairyGUI;

namespace GameScript.UI.Quest
{
    /// <summary>
    /// Builds a <see cref="GComponent"/> tree matching <see cref="QuestFairyGuiView"/> child names,
    /// without a FairyGUI Editor export (for tests and <c>quest_panel</c> prefab).
    /// </summary>
    public static class QuestUiRootBuilder
    {
        public static GComponent BuildRoot()
        {
            var root = new GComponent();
            root.name = "QuestPanelRoot";
            root.SetSize(720, 900);

            root.AddChild(CreateTextField("txtWallet", 620, 40, 80, 36));
            root.AddChild(CreateTextField("txtMainTitle", 40, 100, 640, 40));
            root.AddChild(CreateTextField("txtMainProgress", 40, 150, 640, 36));

            var listSide = new GList();
            listSide.name = "listSide";
            listSide.SetXY(40, 220);
            listSide.SetSize(640, 200);
            root.AddChild(listSide);

            var listDaily = new GList();
            listDaily.name = "listDaily";
            listDaily.SetXY(40, 440);
            listDaily.SetSize(640, 200);
            root.AddChild(listDaily);

            var tabs = new Controller();
            tabs.name = "tabs";
            tabs.AddPage("main");
            tabs.AddPage("side");
            tabs.AddPage("daily");
            root.AddController(tabs);

            return root;
        }

        private static GTextField CreateTextField(string name, float x, float y, float w, float h)
        {
            var tf = new GTextField();
            tf.name = name;
            tf.SetXY(x, y);
            tf.SetSize(w, h);
            tf.text = string.Empty;
            return tf;
        }

        /// <summary>One row for side/daily lists (title, progress, claim).</summary>
        public static GComponent BuildQuestRowItem()
        {
            var row = new GComponent();
            row.name = "QuestRow";
            row.SetSize(600, 48);

            var title = new GTextField();
            title.name = "title";
            title.SetXY(8, 8);
            title.SetSize(240, 32);
            row.AddChild(title);

            var progress = new GTextField();
            progress.name = "progress";
            progress.SetXY(260, 8);
            progress.SetSize(160, 32);
            row.AddChild(progress);

            var btn = new GButton();
            btn.name = "btnClaim";
            btn.SetXY(440, 4);
            btn.SetSize(140, 40);
            btn.title = "领取";
            row.AddChild(btn);

            return row;
        }
    }
}
