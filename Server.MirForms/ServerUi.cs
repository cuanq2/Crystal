using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using WinControl = System.Windows.Forms.Control;

namespace Server.MirForms;

// Display-only localization. Database values, enum values and editable inputs
// remain unchanged; UI code that uses captions as keys reads OriginalText.
public static class ServerUi
{
    private sealed class Caption
    {
        public string Original = "", Translated = "";
        public bool Attached;
        public int LabelRight;
    }
    private static readonly ConditionalWeakTable<object, Caption> Captions = new();
    private static readonly Dictionary<string, string> Terms = Load();
    private static readonly Regex Fragments = new(@"(?<![A-Za-z0-9_])(?:" +
        string.Join("|", Terms.Keys.Where(k => k.Trim() == k && k.Length > 1 && Regex.IsMatch(k, "[A-Za-z]") && !k.Contains('\n'))
            .OrderByDescending(k => k.Length).Select(Regex.Escape)) + @")(?![A-Za-z0-9_])", RegexOptions.Compiled);

    private static Dictionary<string, string> Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Localization", "Interface.Chinese.json");
        return File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) : new();
    }

    public static bool Chinese => Settings.Language.Equals("Chinese", StringComparison.OrdinalIgnoreCase);

    public static string T(string text)
    {
        if (!Chinese || string.IsNullOrEmpty(text)) return text;
        if (Terms.TryGetValue(text, out var translated)) return translated;
        var uptime = Regex.Match(text, @"^Uptime: (\d+)d:(\d+)h:(\d+)m:(\d+)s$");
        if (uptime.Success) return $"运行时长：{uptime.Groups[1]}天 {uptime.Groups[2]}时 {uptime.Groups[3]}分 {uptime.Groups[4]}秒";
        return Fragments.Replace(text, match => Terms[match.Value]);
    }

    public static string OriginalText(WinControl control)
    {
        if (control is ComboBox combo && combo.SelectedItem != null) return combo.SelectedItem.ToString();
        if (control is ListBox list && list.SelectedItem != null) return list.SelectedItem.ToString();
        return Original(control, control.Text);
    }
    public static string OriginalText(ToolStripItem item) => Original(item, item.Text);
    public static string OriginalText(DataGridViewColumn column) => Original(column, column.HeaderText);
    private static string Original(object owner, string text) =>
        Captions.TryGetValue(owner, out var caption) && text == caption.Translated ? caption.Original : text;

    private static string CaptionText(object owner, string text)
    {
        var state = Captions.GetOrCreateValue(owner);
        if (text != state.Translated) state.Original = text;
        state.Translated = T(state.Original);
        return state.Translated;
    }

    private static void ApplyCaption(WinControl control)
    {
        control.Text = CaptionText(control, control.Text);
        var state = Captions.GetOrCreateValue(control);
        // Keep colon labels aligned to their original input edge when Chinese
        // text is wider. AutoSize otherwise grows across the adjacent input.
        if (control is Label { AutoSize: true } label && state.LabelRight > 0)
            label.Left = Math.Max(0, state.LabelRight - label.PreferredWidth);
    }

    private static void ApplyItem(ToolStripItem item)
    {
        var state = Captions.GetOrCreateValue(item);
        if (!state.Attached)
        {
            state.Attached = true;
            item.TextChanged += (_, _) => item.Text = CaptionText(item, item.Text);
        }
        item.Text = CaptionText(item, item.Text);
        item.ToolTipText = T(item.ToolTipText);
        if (item is ToolStripDropDownItem menu)
            foreach (ToolStripItem child in menu.DropDownItems) ApplyItem(child);
    }

    public static void Apply(WinControl control)
    {
        var state = Captions.GetOrCreateValue(control);
        bool caption = control is Form or Label or ButtonBase or GroupBox or TabPage;
        if (!state.Attached)
        {
            state.Attached = true;
            if (control is Label { AutoSize: true } label && label.Text.TrimEnd().EndsWith(':'))
                state.LabelRight = label.Right;
            if (caption) control.TextChanged += (_, _) => ApplyCaption(control);
            control.ControlAdded += (_, e) => Apply(e.Control);
            if (control is ListControl list)
            {
                list.FormattingEnabled = true;
                list.Format += (_, e) =>
                {
                    // Formatting changes presentation only; SelectedItem retains its value.
                    if (e.ListItem is Enum or ItemInfoForm.ComboBoxItem || e.Value is string text && Terms.ContainsKey(text))
                        e.Value = T(e.Value?.ToString());
                };
            }
            if (control is DataGridView grid)
            {
                grid.ColumnAdded += (_, e) => e.Column.HeaderText = CaptionText(e.Column, e.Column.HeaderText);
                grid.CellFormatting += (_, e) =>
                {
                    if (e.Value is Enum value) { e.Value = T(value.ToString()); e.FormattingApplied = true; }
                };
            }
            if (control is ToolStrip strip) strip.ItemAdded += (_, e) => ApplyItem(e.Item);
        }
        if (caption) ApplyCaption(control);
        if (control is ToolStrip tools)
            foreach (ToolStripItem item in tools.Items) ApplyItem(item);
        if (control.ContextMenuStrip != null)
            foreach (ToolStripItem item in control.ContextMenuStrip.Items) ApplyItem(item);
        if (control is ListView view)
            foreach (ColumnHeader column in view.Columns) column.Text = T(column.Text);
        if (control is DataGridView table)
            foreach (DataGridViewColumn column in table.Columns) column.HeaderText = CaptionText(column, column.HeaderText);
        foreach (WinControl child in control.Controls) Apply(child);
        // Designer tooltips are components, not child controls.
        if (control is Form or UserControl)
            foreach (var field in control.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (field.FieldType == typeof(ToolTip) && field.GetValue(control) is ToolTip tip)
                    TranslateTips(control, tip);
    }

    private static void TranslateTips(WinControl parent, ToolTip tip)
    {
        string text = tip.GetToolTip(parent);
        if (!string.IsNullOrEmpty(text)) tip.SetToolTip(parent, T(text));
        foreach (WinControl child in parent.Controls) TranslateTips(child, tip);
    }

    public static DialogResult Show(string text, string caption = "提示", MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.None, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1) =>
        System.Windows.Forms.MessageBox.Show(T(text), T(caption), buttons, icon, defaultButton);

    public static DialogResult Show(IWin32Window owner, string text, string caption = "提示", MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.None, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1) =>
        System.Windows.Forms.MessageBox.Show(owner, T(text), T(caption), buttons, icon, defaultButton);

    public static string Input(string prompt, string title, string defaultValue = "") =>
        Microsoft.VisualBasic.Interaction.InputBox(T(prompt), T(title), defaultValue);

    public static DialogResult ShowDialog(CommonDialog dialog)
    {
        if (dialog is FileDialog file) { file.Title = T(file.Title); file.Filter = T(file.Filter); }
        if (dialog is FolderBrowserDialog folder) folder.Description = T(folder.Description);
        return dialog.ShowDialog();
    }
}
