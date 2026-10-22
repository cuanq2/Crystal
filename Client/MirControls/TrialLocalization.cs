using System.Text.Json;
using System.Text.RegularExpressions;

namespace Client.MirControls
{
    internal static class TrialLocalization
    {
        private static Dictionary<string, string> _terms;
        private static Regex _pattern;
        private static Dictionary<string, string> _questLines;
        private static readonly Dictionary<string, string> Cache = new();

        internal static string Text(string text)
        {
            if (string.IsNullOrEmpty(text) || !Settings.Language.Equals("Chinese", StringComparison.OrdinalIgnoreCase)) return text;
            if (_terms == null)
            {
                string path = Path.Combine("Localization", "Chinese.Trial.json");
                _terms = File.Exists(path)
                    ? new Dictionary<string, string>(JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)), StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>();
                if (_terms.Count > 0)
                    _pattern = new Regex(@"(?<![A-Za-z0-9])(?:" + string.Join("|", _terms.Keys.OrderByDescending(x => x.Length).Select(Regex.Escape)) + @")(?![A-Za-z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
            if (_pattern == null) return text;
            if (Cache.TryGetValue(text, out string cached)) return cached;
            string result = _pattern.Replace(text, match => _terms[match.Value]);
            if (Cache.Count >= 4096) Cache.Clear();
            Cache[text] = result;
            return result;
        }

        internal static string NpcLine(string line)
        {
            if (!string.IsNullOrEmpty(line) && Settings.Language.Equals("Chinese", StringComparison.OrdinalIgnoreCase))
            {
                if (_questLines == null)
                {
                    string path = Path.Combine("Localization", "Chinese.Quests.json");
                    _questLines = File.Exists(path)
                        ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
                        : new Dictionary<string, string>();
                }
                if (_questLines.TryGetValue(line.Trim(), out string translation)) return translation;
            }
            // Preserve server action keys, link destinations and colour identifiers.
            var tokens = new List<string>();
            string protectedLine = Regex.Replace(line, @"/[^<>{}\r\n]+(?=[>}])", match =>
            {
                tokens.Add(match.Value);
                return "\uE000" + (tokens.Count - 1) + "\uE001";
            });
            protectedLine = Text(protectedLine);
            for (int i = 0; i < tokens.Count; i++) protectedLine = protectedLine.Replace("\uE000" + i + "\uE001", tokens[i]);
            return protectedLine;
        }

        internal static string MonsterName(string name, Monster image, byte ai, MonsterType rarity, uint master)
        {
            bool townGuard = (ai == 6 || ai == 57 || ai == 58 || ai == 80) &&
                (image == Monster.Guard || image == Monster.TaoistGuard || image == Monster.OmaGuard ||
                 image == Monster.ArcherGuard || image == Monster.CrossbowOma || image == Monster.Guard3 || image == Monster.ArcherGuard3);
            if (Settings.Language.Equals("Chinese", StringComparison.OrdinalIgnoreCase) && townGuard) return name;
            return master == 0 && rarity != MonsterType.Normal ? $"{rarity.ToLocalizedString()}_{name}" : name;
        }
    }
}
