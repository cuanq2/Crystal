using System.Text;
using System.Text.Json;
if (args.Length < 2 || args.Length > 4)
    throw new ArgumentException("Usage: ItemNameAudit <Server.MirDB> <items.json> [monsters.json] [quests.json]");
args = args.Select(Path.GetFullPath).ToArray();
if (args.Distinct(StringComparer.OrdinalIgnoreCase).Count() != args.Length)
    throw new ArgumentException("Input database and output files must be distinct.");
var bytes = File.ReadAllBytes(args[0]);
using var reader = new BinaryReader(new MemoryStream(bytes));
int version = reader.ReadInt32(), custom = reader.ReadInt32();
Server.MirEnvir.Envir.LoadVersion = version;
Server.MirEnvir.Envir.LoadCustomVersion = custom;
reader.BaseStream.Position += 5 * 4;
if (version >= 63) reader.ReadInt32();
if (version >= 66) reader.ReadInt32();
if (version >= 68) reader.ReadInt32();
int maps = reader.ReadInt32();
for (int i = 0; i < maps; i++) _ = new Server.MirDatabase.MapInfo(reader);
int count = reader.ReadInt32();
if (count < 100 || count > 100000) throw new Exception("Invalid item section count");
var rows = new List<object>();
for (int i = 0; i < count; i++)
{
    var item = new ItemInfo(reader, version, custom);
    if (!Enum.IsDefined(item.Type) || item.Name.Length == 0) throw new Exception("Invalid item record");
    rows.Add(new { index = item.Index, name = item.Name, friendlyName = item.FriendlyName, type = item.Type.ToString(), level = item.RequiredAmount, shape = item.Shape, image = item.Image, gender = item.RequiredGender.ToString(), job = item.RequiredClass.ToString() });
}
File.WriteAllText(args[1], JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Read {count} items from DB {version}/{custom}");
if (args.Length > 2)
{
    int monsters = reader.ReadInt32();
    var monsterRows = new List<object>();
    for (int i = 0; i < monsters; i++)
    {
        var m = new Server.MirDatabase.MonsterInfo(reader);
        monsterRows.Add(new { index = m.Index, name = m.Name, image = m.Image.ToString(), ai = m.AI, level = m.Level, canTame = m.CanTame, hp = m.Stats[Stat.HP] });
    }
    File.WriteAllText(args[2], JsonSerializer.Serialize(monsterRows, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Read {monsters} monsters");
    if (args.Length > 3)
    {
        int npcs = reader.ReadInt32();
        for (int i = 0; i < npcs; i++) _ = new Server.MirDatabase.NPCInfo(reader);
        int quests = reader.ReadInt32();
        var questRows = new List<object>();
        for (int i = 0; i < quests; i++)
        {
            // Match QuestInfo's binary layout without invoking its LoadInfo(), which
            // reads task scripts relative to the process's current working directory.
            int questIndex = reader.ReadInt32();
            string questName = reader.ReadString(), questGroup = reader.ReadString(), questFile = reader.ReadString();
            reader.ReadInt32(); // RequiredMinLevel
            reader.ReadInt32(); // RequiredMaxLevel
            reader.ReadInt32(); // RequiredQuest
            reader.ReadByte(); // RequiredClass
            reader.ReadByte(); // QuestType
            string go = reader.ReadString(), kill = reader.ReadString(), item = reader.ReadString(), flag = reader.ReadString();
            if (version > 90) reader.ReadInt32(); // TimeLimitInSeconds
            questRows.Add(new { index = questIndex, name = questName, group = questGroup, file = questFile, gotoMessage = go, killMessage = kill, itemMessage = item, flagMessage = flag });
        }
        File.WriteAllText(args[3], JsonSerializer.Serialize(questRows, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Read {quests} quests");
    }
}
