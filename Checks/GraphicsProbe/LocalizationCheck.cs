using System.Reflection;
using Client.MirControls;
using Client.MirGraphics;
using System.Text.Json;

internal static class LocalizationCheck
{
    internal static void Run()
    {
        var assembly = typeof(MirLabel).Assembly;
        var settings = assembly.GetType("Client.Settings")!;
        var language = settings.GetField("Language")!;
        language.SetValue(null, "Chinese");
        settings.GetField("FontName")!.SetValue(null, "Microsoft YaHei");
        var localizer = assembly.GetType("Client.MirControls.TrialLocalization")!;
        var translate = localizer.GetMethod("Text", BindingFlags.NonPublic | BindingFlags.Static)!;
        var npc = localizer.GetMethod("NpcLine", BindingFlags.NonPublic | BindingFlags.Static)!;
        CheckLegacyTermOverrides(localizer, translate);
        foreach (var pair in new Dictionary<string, string>
        {
            ["WoodenSword"] = "木剑",
            ["Chicken"] = "鸡",
            ["BichonProvince"] = "比奇省",
            ["Venison"] = "鹿肉",
            ["你获得了 Venison。"] = "你获得了 鹿肉。",
            ["Guard"] = "大刀卫士",
            ["Guard1"] = "带刀护卫",
            ["Guard-NorthEast"] = "东北大刀卫士",
            ["ArcherGuard"] = "弓箭守卫",
            ["Assistant_Jane"] = "新手接引人_李小芸",
            ["Merchant_John"] = "肉店老板_张福生",
            ["Teleport_Gilbert"] = "传送员_陈远",
            ["Blacksmith_Smith"] = "铁匠_张铁山",
            ["Royal_Guard"] = "大刀卫士",
            ["The CraftLady's Request"] = "巧匠的委托",
            ["Collect GingerTea: 0/1 "] = "收集 姜茶: 0/1 ",
            ["Collect GingerTea: 1/1 (Completed)"] = "收集 姜茶: 1/1 (已完成)",
            ["Nothing was found."] = "没有找到任何物品。"
        })
            if ((string)translate.Invoke(null, new object[] { pair.Key })! != pair.Value)
                throw new Exception("Trial text lookup failed: " + pair.Key);
        string resourceDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var equipment = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(resourceDir, "equipment-name-translations.json")))!;
        var displayText = typeof(MirLabel).GetProperty("DisplayText", BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var pair in equipment)
        {
            using var label = new MirLabel { Text = pair.Key };
            if ((string)displayText.GetValue(label)! != pair.Value || label.Text != pair.Key)
                throw new Exception("Equipment label translation or original identifier failed: " + pair.Key);
            string message = $"你获得了 {pair.Key} (2)。";
            if ((string)translate.Invoke(null, new object[] { message })! != $"你获得了 {pair.Value} (2)。")
                throw new Exception("Equipment pickup/count translation failed: " + pair.Key);
        }
        var records = JsonDocument.Parse(File.ReadAllText(Path.Combine(resourceDir, "item-name-audit.json")));
        int equipmentRecords = 0;
        foreach (var row in records.RootElement.EnumerateArray())
        {
            if (!equipment.TryGetValue(row.GetProperty("friendlyName").GetString()!, out var expected)) continue;
            var raw = row.GetProperty("name").GetString()!;
            if ((string)translate.Invoke(null, new object[] { raw })! != expected)
                throw new Exception("Raw equipment variant translation failed: " + raw);
            equipmentRecords++;
        }
        Console.Error.WriteLine($"Equipment: {equipment.Count} display names and {equipmentRecords} raw database records passed label/pickup/identifier checks.");
        NarrativeLocalizationCheck.Run(assembly, localizer, resourceDir);
        DialogLayoutCheck.Run();
        NpcDialogueCheck.Run();
        string markup = (string)npc.Invoke(null, new object[] { "<View/@BuySell> Store. {Gold/Gold} <Back/@Main>" })!;
        if (markup != "<查看/@BuySell> 商店. {金币/Gold} <返回/@Main>")
            throw new Exception("NPC translation altered action or color tokens: " + markup);
        // Constructor selection needs a file's presence, not real game assets.
        string spriteFixture = Path.Combine(Path.GetTempPath(), "CrystalSpriteSelection-" + Guid.NewGuid().ToString("N"));
        string translatedFolder = Path.Combine(spriteFixture, "Localization", "Chinese");
        Directory.CreateDirectory(translatedFolder);
        try
        {
            File.WriteAllBytes(Path.Combine(translatedFolder, "Title.Lib"), Array.Empty<byte>());
            var library = new MLibrary(Path.Combine(spriteFixture, "Title"));
            var fileName = typeof(MLibrary).GetField("_fileName", BindingFlags.NonPublic | BindingFlags.Instance)!;
            if ((string)fileName.GetValue(library)! != Path.Combine(translatedFolder, "Title.Lib"))
                throw new Exception("Chinese sprite library was not selected.");
            language.SetValue(null, "English");
            if ((string)translate.Invoke(null, new object[] { "WoodenSword" })! != "WoodenSword") throw new Exception("English fallback failed.");
            var original = new MLibrary(Path.Combine(spriteFixture, "Title"));
            if ((string)fileName.GetValue(original)! != Path.Combine(spriteFixture, "Title.Lib"))
                throw new Exception("English selected a localized sprite library.");
        }
        finally
        {
            language.SetValue(null, "Chinese");
            Directory.Delete(spriteFixture, recursive: true);
        }
        Console.Error.WriteLine("Chinese text, NPC action/color preservation, sprite selection and English fallback passed.");
    }

    private static void CheckLegacyTermOverrides(Type localizer, MethodInfo translate)
    {
        string previousDirectory = Directory.GetCurrentDirectory();
        string fixture = Path.Combine(Path.GetTempPath(), "CrystalTermOverrides-" + Guid.NewGuid().ToString("N"));
        var terms = localizer.GetField("_terms", BindingFlags.NonPublic | BindingFlags.Static)!;
        var pattern = localizer.GetField("_pattern", BindingFlags.NonPublic | BindingFlags.Static)!;
        var cache = (Dictionary<string, string>)localizer.GetField("Cache", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        Directory.CreateDirectory(Path.Combine(fixture, "Localization"));
        try
        {
            File.WriteAllText(Path.Combine(fixture, "Localization", "Chinese.Trial.json"),
                JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["Dead Mine"] = "废弃矿区",
                    ["Dead mine"] = "废矿"
                }));
            Directory.SetCurrentDirectory(fixture);
            if ((string)translate.Invoke(null, new object[] { "DEAD MINE" })! != "废矿")
                throw new Exception("Legacy case-only term overrides must keep the last translation.");
        }
        finally
        {
            Directory.SetCurrentDirectory(previousDirectory);
            terms.SetValue(null, null);
            pattern.SetValue(null, null);
            cache.Clear();
            Directory.Delete(fixture, recursive: true);
        }
        Console.Error.WriteLine("Legacy term overrides passed: case-insensitive lookup and last entry wins.");
    }
}
