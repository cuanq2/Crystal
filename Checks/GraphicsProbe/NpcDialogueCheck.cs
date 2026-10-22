using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Client.MirControls;

internal static class NpcDialogueCheck
{
    internal static void Run()
    {
        var localizer = typeof(MirLabel).Assembly.GetType("Client.MirControls.TrialLocalization")!;
        var translate = localizer.GetMethod("NpcLine", BindingFlags.Static | BindingFlags.NonPublic)!;
        string Line(string value) => (string)translate.Invoke(null, new object[] { value })!;
        // Check the values the server inserts when equipment slots are empty,
        // not just the <$...> placeholders in the script templates.
        var emptyEquipment = new Dictionary<ServerTextKeys, string>
        {
            [ServerTextKeys.NoArmour] = "无护甲",
            [ServerTextKeys.NoWeapon] = "无武器",
            [ServerTextKeys.NoRing] = "无戒指",
            [ServerTextKeys.NoBracelet] = "无手镯",
            [ServerTextKeys.NoNecklace] = "无项链",
            [ServerTextKeys.NoBelt] = "无腰带",
            [ServerTextKeys.NoBoots] = "无靴子",
            [ServerTextKeys.NoHelmet] = "无头盔",
            [ServerTextKeys.NoAmulet] = "无护符",
            [ServerTextKeys.NoStone] = "无宝石",
            [ServerTextKeys.NoTorch] = "无火把",
            [ServerTextKeys.NoMount] = "未装备坐骑"
        };
        var display = typeof(MirLabel).GetProperty("DisplayText", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var language = typeof(MirLabel).Assembly.GetType("Client.Settings")!.GetField("Language")!;
        foreach (var pair in emptyEquipment)
        {
            string raw = GameLanguage.ServerTextMap.GetLocalization(pair.Key);
            using var label = new MirLabel { Text = raw };
            if ((string)display.GetValue(label)! != pair.Value || label.Text != raw)
                throw new Exception("Empty equipment value remains untranslated: " + raw);
            string coloured = "I see you're wearing: {" + raw + "/CORAL}";
            if (Line(coloured) != "你当前装备： {" + pair.Value + "/CORAL}")
                throw new Exception("Expanded empty equipment dialogue failed: " + Line(coloured));
            language.SetValue(null, "English");
            try
            {
                if ((string)display.GetValue(label)! != raw || Line(coloured) != coloured)
                    throw new Exception("English empty equipment fallback changed: " + raw);
            }
            finally { language.SetValue(null, "Chinese"); }
        }
        if (Line("I see you're wearing: {L: No Ring R: No Ring/CORAL}") !=
            "你当前装备： {左： 无戒指 右： 无戒指/CORAL}")
            throw new Exception("Both empty ring slots must be translated together.");
        Console.Error.WriteLine($"Empty equipment checks passed: {emptyEquipment.Count} server fallback values, colored dialogue, paired ring slots and English fallback.");
        var source = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
            File.ReadAllText("Fixtures/gm-dialogue-source.json"))!;
        const string tokens = @"<\$[^>]+>|/@[^>\r\n]+|/[^/{}\r\n]+(?=})|https?://[^)\s]+|@[A-Za-z][A-Za-z0-9]*";
        foreach (string raw in source.Keys)
        {
            string result = Line(raw);
            if (!Regex.Matches(raw, tokens).Select(m => m.Value).SequenceEqual(Regex.Matches(result, tokens).Select(m => m.Value)))
                throw new Exception("NPC action/color/variable/command changed: " + raw);
            string visible = Regex.Replace(result, tokens, "");
            visible = Regex.Replace(visible, @"\b(?:HP|MP|R001)\b", "");
            if (Regex.IsMatch(visible, "[A-Za-z]") || visible.Contains("??"))
                throw new Exception("GM dialogue remains untranslated: " + raw + " => " + result);
        }
        foreach (var pair in new Dictionary<string, string>
        {
            ["What function would you like me to perform today 2026/10/22"] = "请选择要使用的管理功能。今日日期： 2026/10/22",
            ["Current Owner of SabukWall is: PlayerGuild"] = "当前沙巴克城主： PlayerGuild",
            ["Hello {LocalHero/KHAKI}, my name is {GM Manager/KHAKI}."] = "你好， {LocalHero/KHAKI}，我是 {管理员 管理员/KHAKI}.",
            ["<Start War/End War/@StartWar>"] = "<开始／结束攻城/@StartWar>"
        })
            if (Line(pair.Key) != pair.Value) throw new Exception("Expanded NPC variable or menu failed: " + Line(pair.Key));
        using var command = new MirLabel { Text = "@GM" };
        if ((string)typeof(MirLabel).GetProperty("DisplayText", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(command)! != "@GM")
            throw new Exception("Visible GM command was translated into an unusable command.");
        Console.Error.WriteLine($"GM dialogue checks passed: {source.Count} lines from {source.Values.SelectMany(x => x).Distinct().Count()} scripts; variables, commands, colors and actions preserved.");
    }
}
