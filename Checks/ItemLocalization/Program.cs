using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Client.MirControls;

// Uses the referenced client assembly and actual MirLabel display getter.
// Does not connect to the server or alter localization files.
string recordsPath = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(AppContext.BaseDirectory, "Fixtures", "item-name-audit.json");
string outputPath = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.Combine(AppContext.BaseDirectory, "item-localization-result.json");
string translationsPath = args.Length > 2 ? Path.GetFullPath(args[2]) : Path.Combine(AppContext.BaseDirectory, "Fixtures", "ItemNames.zh-CN.json");
Directory.SetCurrentDirectory(AppContext.BaseDirectory);
var assembly = typeof(MirLabel).Assembly;
assembly.GetType("Client.Settings")!.GetField("Language")!.SetValue(null, "Chinese");
var display = typeof(MirLabel).GetProperty("DisplayText", BindingFlags.Instance | BindingFlags.NonPublic)!;
var language = assembly.GetType("Client.Settings")!.GetField("Language")!;
var translate = assembly.GetType("Client.MirControls.TrialLocalization")!.GetMethod("Text", BindingFlags.Static | BindingFlags.NonPublic)!;
var canonical = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(translationsPath))!;
var rows = JsonDocument.Parse(File.ReadAllText(recordsPath)).RootElement;
var output = new List<object>();
int english = 0;
int aliasChecks = 0, pickupChecks = 0;
foreach (var row in rows.EnumerateArray())
{
    string name = row.GetProperty("friendlyName").GetString()!;
    using var label = new MirLabel { Text = name };
    string rendered = (string)display.GetValue(label)!;
    bool remains = Regex.IsMatch(rendered, "[A-Za-z]");
    if (remains) english++;
    if (canonical != null)
    {
        string raw = row.GetProperty("name").GetString()!;
        string expected = canonical[name];
        if (remains || !Regex.IsMatch(rendered, "[\u3400-\u9fff]") || rendered != expected || label.Text != name)
            throw new Exception("Item name not fully localized or original changed: " + name);
        using var rawLabel = new MirLabel { Text = raw };
        if ((string)display.GetValue(rawLabel)! != expected || rawLabel.Text != raw)
            throw new Exception("Internal item alias translation or identifier changed: " + raw);
        aliasChecks++;
        string pickup = $"你获得了 {name} (23)。";
        string counted = $"{name} (23)";
        if ((string)translate.Invoke(null, new object[] { pickup })! != $"你获得了 {expected} (23)。" ||
            (string)translate.Invoke(null, new object[] { counted })! != $"{expected} (23)")
            throw new Exception("Pickup/count localization failed: " + name);
        pickupChecks++;
        language.SetValue(null, "English");
        if ((string)display.GetValue(label)! != name)
            throw new Exception("English fallback changed: " + name);
        language.SetValue(null, "Chinese");
    }
    output.Add(new { index = row.GetProperty("index").GetInt32(), type = row.GetProperty("type").GetString(), name, display = rendered, englishRemaining = remains });
}
if (canonical != null)
{
    foreach (var pair in new Dictionary<string, string> { ["Pig"] = "猪", ["SpiderWarrior"] = "蛛魔战士", ["ManectricClub"] = "持棍雷兽", ["ManectricHammer"] = "持锤雷兽", ["GingerTea"] = "姜茶", ["Royal_Guard"] = "大刀卫士", ["IronOre"] = "铁矿石", ["BlackIronOre"] = "黑铁矿石", ["WoomaHorn"] = "沃玛号角", ["ZumaRelic"] = "祖玛头像" })
        if ((string)translate.Invoke(null, new object[] { pair.Key })! != pair.Value)
            throw new Exception("Shared name or classic name regression: " + pair.Key);
}
File.WriteAllText(outputPath, JsonSerializer.Serialize(new { assembly = assembly.Location, records = output.Count, englishRecords = english, aliasChecks, pickupChecks, englishFallbackChecks = pickupChecks, items = output }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Client MirLabel display audit: {english}/{output.Count} item records retain English; {aliasChecks} original aliases and {pickupChecks} pickup/count/English-fallback cases passed.");
