using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Client.MirControls;
using Client.MirScenes.Dialogs;

internal static class NarrativeLocalizationCheck
{
    internal static void Run(Assembly assembly, Type localizer, string resources)
    {
        GameLanguage.LoadClientLanguage("Localization/Chinese.json");
        if (GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Miss) != "未命中") throw new Exception("Miss must be 未命中");
        var text = localizer.GetMethod("Text", BindingFlags.Static | BindingFlags.NonPublic)!;
        var line = localizer.GetMethod("NpcLine", BindingFlags.Static | BindingFlags.NonPublic)!;
        var monsterName = localizer.GetMethod("MonsterName", BindingFlags.Static | BindingFlags.NonPublic)!;
        string Text(string value) => (string)text.Invoke(null, new object[] { value })!;
        string Line(string value) => (string)line.Invoke(null, new object[] { value })!;
        foreach (var name in new[] { "Guard", "Royal_Guard", "Guard3" })
            foreach (var rarity in Enum.GetValues<MonsterType>())
            {
                string result = (string)monsterName.Invoke(null, new object[] { name, Monster.Guard, (byte)6, rarity, (uint)0 })!;
                if (result != name || Text(result) != "大刀卫士") throw new Exception("Town guard name/rarity regression: " + name);
            }
        string rareMob = (string)monsterName.Invoke(null, new object[] { "Chicken", Monster.Hen, (byte)0, MonsterType.Rare, (uint)0 })!;
        if (!rareMob.Contains('_')) throw new Exception("Non-guard rarity tag was lost");
        var names = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(resources, "npc-name-translations.json")))!;
        foreach (var pair in names)
            if (Text(pair.Key) != pair.Value || Regex.IsMatch(Text(pair.Key), "[A-Za-z]")) throw new Exception("NPC translation failed: " + pair.Key);
        var quests = JsonDocument.Parse(File.ReadAllText(Path.Combine(resources, "quest-name-audit.json")));
        int messages = 0;
        foreach (var q in quests.RootElement.EnumerateArray())
        {
            if (Regex.IsMatch(Text(q.GetProperty("name").GetString()!), "[A-Za-z]")) throw new Exception("Quest title remains English");
            foreach (string field in new[] { "gotoMessage", "killMessage", "itemMessage", "flagMessage" })
            {
                string raw = q.GetProperty(field).GetString()!;
                if (raw.Length == 0) continue;
                if (Regex.IsMatch(Text(raw), "[A-Za-z]")) throw new Exception("Quest progress remains English: " + raw);
                if (string.Join(",", Regex.Matches(raw, @"\d+").Select(m => m.Value)) != string.Join(",", Regex.Matches(Text(raw), @"\d+").Select(m => m.Value)))
                    throw new Exception("Progress coordinates/count changed: " + raw);
                messages++;
            }
        }
        var phrases = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText("Localization/Chinese.Quests.json"))!;
        foreach (var pair in phrases)
        {
            string translated = Line(pair.Key);
            if (translated != pair.Value) throw new Exception("Whole quest sentence failed: " + pair.Key);
            var originalTokens = Regex.Matches(pair.Key, @"/[^<>{}\r\n]+(?=[>}])").Select(m => m.Value).OrderBy(s => s);
            var translatedTokens = Regex.Matches(translated, @"/[^<>{}\r\n]+(?=[>}])").Select(m => m.Value).OrderBy(s => s);
            if (!originalTokens.SequenceEqual(translatedTokens)) throw new Exception("Quest action/color token changed: " + pair.Key);
            string visible = Regex.Replace(translated, @"/[^<>{}\r\n]+(?=[>}])|<\$[^>]*>", "");
            if (Regex.IsMatch(visible, "[A-GI-Za-gi-z]")) throw new Exception("Mixed English/Chinese quest sentence: " + pair.Key);
        }
        // Exercise the actual quest-window parser with the reported GingerTea task.
        using var metrics = new System.Drawing.Bitmap(1, 1);
        using var graphics = System.Drawing.Graphics.FromImage(metrics);
        var previousGraphics = Client.CMain.Graphics;
        Client.CMain.Graphics = graphics;
        using var up = new MirButton(); using var down = new MirButton(); using var bar = new MirButton();
        using var panel = new QuestMessage(up, down, bar, 8) { Size = new System.Drawing.Size(420, 180) };
        var rawLines = new List<string> { "The CraftLady's Request", "Find {GingerTea/LightSteelBlue} from {Scarecrows/Crimson} and Deliver", "to {Assistant Jane/LimeGreen} in {BorderVillage/Yellow}." };
        typeof(QuestMessage).GetMethod("NewText", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(panel, new object[] { rawLines, true });
        var labels = (MirLabel[])typeof(QuestMessage).GetField("_textLabel", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(panel)!;
        var display = typeof(MirLabel).GetProperty("DisplayText", BindingFlags.Instance | BindingFlags.NonPublic)!;
        string rendered = string.Join("\n", labels.Where(x => x != null).Select(x => (string)display.GetValue(x)!));
        if (!rendered.Contains("巧匠的委托") || !rendered.Contains("姜茶") || rendered.Contains("GingerTea") || rendered.Contains("The ")) throw new Exception("Actual quest panel has mixed text: " + rendered);
        Client.CMain.Graphics = previousGraphics;
        Console.Error.WriteLine($"Narrative checks passed: {names.Count} NPC names, {quests.RootElement.GetArrayLength()} quest records, {messages} progress messages, {phrases.Count} full sentences, guard rarity and actual GingerTea quest panel; Miss=未命中.");
    }
}
