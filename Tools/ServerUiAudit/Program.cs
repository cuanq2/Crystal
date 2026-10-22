using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

if (args.Length is < 2 or > 3) throw new ArgumentException("Usage: ServerUiAudit <Server.MirForms> <output.json> [--check]");
var root = Path.GetFullPath(args[0]);
var entries = new SortedDictionary<string, HashSet<string>>(StringComparer.Ordinal);
void Add(string text, string source)
{
    if (!Regex.IsMatch(text, "[A-Za-z]")) return;
    if (!entries.TryGetValue(text, out var sources)) entries[text] = sources = new();
    sources.Add(source);
}
foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
{
    if (Path.GetRelativePath(root, file).Split(Path.DirectorySeparatorChar).Any(x => x is "obj" or "bin")) continue;
    var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
    foreach (var node in tree.GetRoot().DescendantNodes().Where(n =>
        n is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression) || n is InterpolatedStringTextSyntax))
    {
        bool ui = node.Ancestors().Any(parent => parent switch
        {
            VariableDeclaratorSyntax v => v.Identifier.Text is "promptText" or "formTitle",
            AssignmentExpressionSyntax a => (a.Left is MemberAccessExpressionSyntax m &&
                m.Name.Identifier.Text is "Text" or "HeaderText" or "ToolTipText" or "Title" or "Filter" or "Description") ||
                (a.Left is IdentifierNameSyntax id && id.Identifier.Text is "Text" or "HeaderText" or "ToolTipText" or "Title" or "Filter" or "Description" or "promptText"),
            InvocationExpressionSyntax call => call.Expression.ToString().Contains("MessageBox.Show") ||
                call.Expression.ToString().Contains("ServerUi.Show") ||
                call.Expression.ToString().Contains("ServerUi.Input") ||
                call.Expression.ToString().EndsWith("Interaction.InputBox") ||
                call.Expression.ToString().EndsWith("SetToolTip") ||
                call.Expression.ToString().EndsWith("Items.AddRange") ||
                call.Expression.ToString().EndsWith("Items.Add") ||
                call.Expression.ToString().EndsWith("Columns.Add"),
            _ => false
        });
        if (!ui) continue;
        string text = node is LiteralExpressionSyntax l ? l.Token.ValueText : ((InterpolatedStringTextSyntax)node).TextToken.ValueText;
        Add(text, Path.GetRelativePath(root, file).Replace('\\', '/') + ":" + (node.GetLocation().GetLineSpan().StartLinePosition.Line + 1));
    }
}
foreach (string file in Directory.EnumerateFiles(root, "*.resx", SearchOption.AllDirectories))
    foreach (var data in XDocument.Load(file).Descendants("data").Where(x => x.Attribute("type") == null && x.Attribute("mimetype") == null))
        if (data.Element("value") is { } value) Add(value.Value, Path.GetRelativePath(root, file) + ":" + data.Attribute("name")?.Value);
File.WriteAllText(args[1], JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
Console.WriteLine($"Audited {entries.Count} unique UI strings.");
if (args.Length == 3)
{
    if (args[2] != "--check") throw new ArgumentException("Expected --check");
    var terms = JsonSerializer.Deserialize<Dictionary<string, string>>(
        File.ReadAllText(Path.Combine(root, "Localization", "Interface.Chinese.json")))!;
    var missing = entries.Keys.Where(key => !terms.ContainsKey(key)).ToArray();
    foreach (string key in missing) Console.Error.WriteLine("Missing UI translation: " + JsonSerializer.Serialize(key));
    if (missing.Length != 0) Environment.Exit(1);
    Console.WriteLine("All audited strings have dictionary entries (including retained technical tokens).");
}
