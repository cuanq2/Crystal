using System.Drawing;
using System.Reflection;
using Client.MirControls;
using Client.MirScenes.Dialogs;

internal static class DialogLayoutCheck
{
    internal static void Run()
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        var previous = Client.CMain.Graphics;
        Client.CMain.Graphics = graphics;
        try
        {
            using var npc = new NPCDialog();
            var lines = new List<string>
            {
                "找到{姜茶/LightSteelBlue}后交给{新手接引人/LimeGreen}。",
                "{红色/Red}{绿色/Lime}{蓝色/Blue}",
                "<购买/@Buy>  <返回/@Main>",
                "前往[NPC:1|李小芸]，收集[ITEM:1|姜茶]。",
                "A & B {XYZ/Yellow} 123。",
                "{同样文字/Red}和{同样文字/Blue}"
            };
            var expected = new[] { "找到姜茶后交给新手接引人。", "红色绿色蓝色", "购买  返回",
                "前往李小芸，收集姜茶。", "A & B XYZ 123。", "同样文字和同样文字" };
            npc.NewText(lines);
            Check(npc.TextLabel, npc.TextButtons, expected);
            if (npc.TextButtons.Single(l => l.Text == "红色").ForeColour != Color.Red ||
                typeof(MirControl).GetField("Click", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(npc.TextButtons.Single(l => l.Text == "购买")) == null)
                throw new Exception("NPC colors or clickable options were lost.");
            int controls = npc.Controls.Count;
            for (int i = 0; i < 5; i++) npc.NewText(lines);
            if (npc.Controls.Count != controls) throw new Exception("NPC refresh leaks old text controls.");
            var merchant = new Dictionary<string, string>
            {
                ["Candle/candle"] = "蜡烛", ["Dungeonescape/dungeonescape"] = "地牢逃脱卷",
                ["RandomTeleport/randomteleport"] = "随机传送卷", ["RepairOil/repairoil"] = "修复油",
                ["Townteleport/townteleport"] = "回城卷", ["Close/Exit"] = "关闭"
            };
            npc.NewText(merchant.Keys.Select(x => "<" + x.Replace("/", "/@") + ">").ToList());
            Check(npc.TextLabel, npc.TextButtons, merchant.Values.ToArray());
            foreach (var label in npc.TextButtons)
                if (typeof(MirControl).GetField("Click", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(label) == null)
                    throw new Exception("Repaired merchant option is not clickable: " + label.Text);
            Console.Error.WriteLine("Merchant links passed: five item descriptions and close render as clickable Chinese text.");
            npc.NewText(Enumerable.Range(0, 45).Select(i => $"第{i}行{{彩色/Red}}").ToList());
            typeof(NPCDialog).GetField("_index", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(npc, 35);
            npc.NewText(npc.CurrentLines, false);
            Check(npc.TextLabel, npc.TextButtons, Enumerable.Range(35, 8).Select(i => $"第{i}行彩色").ToArray());

            using var up = new MirButton(); using var down = new MirButton(); using var bar = new MirButton();
            using var quest = new QuestMessage(up, down, bar, 8) { Size = new Size(420, 180) };
            var newText = typeof(QuestMessage).GetMethod("NewText", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var questLines = new List<string> { "任务标题", lines[0], lines[1], lines[3], lines[4] };
            newText.Invoke(quest, new object[] { questLines, true });
            var rows = (MirLabel[])typeof(QuestMessage).GetField("_textLabel", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(quest)!;
            var runs = (List<MirLabel>)typeof(QuestMessage).GetField("_textButtons", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(quest)!;
            Check(rows, runs, new[] { "任务标题", expected[0], expected[1], expected[3], expected[4] });
            controls = quest.Controls.Count;
            newText.Invoke(quest, new object[] { questLines, true });
            if (quest.Controls.Count != controls) throw new Exception("Quest refresh leaks old text controls.");
            Console.Error.WriteLine("Dialog layout passed: single rendering, nonoverlapping runs/rows, colors, clickable options, links, ampersands, repeated text, refresh and scrolling beyond row 30.");
        }
        finally { Client.CMain.Graphics = previous; }
    }

    private static void Check(MirLabel[] rows, List<MirLabel> runs, string[] expected)
    {
        var populated = rows.Where(r => r != null).ToArray();
        if (populated.Length != expected.Length) throw new Exception("Dialog row count mismatch.");
        for (int i = 0; i < populated.Length; i++)
        {
            var row = populated[i];
            if (row.DrawControlTexture) throw new Exception("Dialog still paints a duplicate base text layer.");
            var segments = runs.Where(l => l.Location.Y == row.Location.Y).OrderBy(l => l.Location.X).ToArray();
            if (string.Concat(segments.Select(l => l.Text)) != expected[i]) throw new Exception("Dialog visible text changed.");
            if (i > 0 && populated[i - 1].Location.Y + populated[i - 1].Size.Height > row.Location.Y)
                throw new Exception("Dialog rows overlap.");
            for (int j = 0; j < segments.Length; j++)
            {
                var label = segments[j];
                if (!label.DrawControlTexture || !label.Visible) throw new Exception("Expected visible run is missing.");
                int width = System.Windows.Forms.TextRenderer.MeasureText(Client.CMain.Graphics, label.Text,
                    label.Font, new Size(int.MaxValue, int.MaxValue), label.DrawFormat).Width + 2;
                if (label.Size.Width < width) throw new Exception("Short dialogue unexpectedly clips a run.");
                if (j > 0 && segments[j - 1].Location.X + segments[j - 1].Size.Width > label.Location.X)
                    throw new Exception("Dialog text runs overlap.");
            }
        }
    }
}
