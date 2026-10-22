using Server;
using Server.MirForms;
using System.Drawing;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;
using System.ComponentModel;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Pass a new output directory for screenshots and reports.");
        string output = Path.GetFullPath(args[0]);
        if (Directory.Exists(output)) throw new ArgumentException("Output must be new.");
        Directory.CreateDirectory(output);
        Directory.SetCurrentDirectory(output);
        Settings.Load(); // Creates only isolated probe configuration in the new directory.
        Directory.CreateDirectory(Settings.DropPath);
        File.WriteAllText(Path.Combine(Settings.DropPath, "UIProbe.txt"), "");
        SMain.EditEnvir.MonsterInfoList.Add(new Server.MirDatabase.MonsterInfo { Index = 1, Name = "UIProbe", Level = 1 });
        using var deadline = new System.Threading.Timer(_ => Environment.Exit(124), null, 60000, Timeout.Infinite);
        Settings.Language = "Chinese";
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var form = new Form { Text = "Server" };
        var button = new Button { Text = "Accept" };
        var label = new Label { Text = "Players: 0" };
        var input = new TextBox { Text = "Warrior" };
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.AddRange(new object[] { MirClass.Warrior, MirClass.Wizard });
        combo.SelectedIndex = 0;
        var table = new DataGridView();
        table.Columns.Add(new DataGridViewTextBoxColumn { Name = "InternalId", DataPropertyName = "Name", HeaderText = "Name" });
        form.Controls.AddRange(new Control[] { button, label, input, combo, table });
        ServerUi.Apply(form);
        Assert(form.Text == "服务端" && button.Text == "确认" && label.Text == "玩家: 0", "Static/dynamic captions");
        Assert(input.Text == "Warrior", "Editable text must not be translated");
        Assert((MirClass)combo.SelectedItem == MirClass.Warrior && ServerUi.OriginalText(combo) == "Warrior", "Enum selection retains raw values");
        Assert(combo.GetItemText(combo.SelectedItem) == "战士", "Enum display translation");
        var allFilter = new ItemInfoForm.ComboBoxItem { Text = "All" };
        combo.Items.Add(allFilter);
        Assert(combo.GetItemText(allFilter) == "全部" && allFilter.Text == "All", "Filter wrapper retains its action value");
        Assert(table.Columns[0].HeaderText == "名称" && table.Columns[0].Name == "InternalId" && table.Columns[0].DataPropertyName == "Name", "Grid binding retained");
        Assert(ServerUi.OriginalText(table.Columns[0]) == "Name", "Column logic sees original header");
        Assert(ServerUi.OriginalText(button) == "Accept", "Button mode logic sees original caption");
        label.Text = "Players: 12";
        Assert(label.Text == "玩家: 12", "Dynamic text changes");
        button.Text = "Edit Drop File";
        Assert(button.Text == "编辑掉落文件" && ServerUi.OriginalText(button) == "Edit Drop File", "Changing button mode");
        var added = new Button { Text = "Reload" }; form.Controls.Add(added);
        Assert(added.Text == "重新加载", "Dynamically added controls");
        Assert(ServerUi.T("Uptime: 1d:2h:3m:4s") == "运行时长：1天 2时 3分 4秒", "Time display");
        Assert(ServerUi.T("Are you sure you want to delete this account?") == "确定要删除此账号吗？", "Confirmation text");
        Assert(!ServerUi.T("Teleport MP Penalty %").Contains("Penalty"), "Generated stat headers");
        Settings.Language = "English";
        ServerUi.Apply(form);
        Assert(button.Text == "Edit Drop File" && table.Columns[0].HeaderText == "Name", "English fallback");
        Settings.Language = "Chinese";

        // Do not show the main form: its Load handler starts the game environment.
        // Detach it before producing a bitmap, so this probe never starts a server.
        using var main = new SMain();
        var load = typeof(SMain).GetMethod("SMain_Load", BindingFlags.Instance | BindingFlags.NonPublic)!;
        main.Load -= (EventHandler)load.CreateDelegate(typeof(EventHandler), main);
        ServerUi.Apply(main);
        PrepareForRender(main);
        using var bitmap = new Bitmap(main.Width, main.Height);
        main.DrawToBitmap(bitmap, new Rectangle(Point.Empty, main.Size));
        bitmap.Save(Path.Combine(output, "main.png"));
        var captions = new List<string>();
        void Collect(Control control)
        {
            if (control is Label or ButtonBase or GroupBox or TabPage or Form) captions.Add(control.Text);
            if (control is ToolStrip strip)
                foreach (ToolStripItem item in strip.Items) CollectMenu(item);
            foreach (Control child in control.Controls) Collect(child);
        }
        void CollectMenu(ToolStripItem item)
        {
            captions.Add(item.Text);
            if (item is ToolStripDropDownItem menu)
                foreach (ToolStripItem child in menu.DropDownItems) CollectMenu(child);
        }
        Collect(main);
        Assert(captions.Contains("控制") && captions.Contains("启动服务端") && captions.Contains("重新加载"), "Actual server menus");
        File.WriteAllText(Path.Combine(output, "captions.json"), JsonSerializer.Serialize(captions, new JsonSerializerOptions {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        int mainCaptionCount = captions.Count;
        var windows = new Dictionary<string, List<string>>();
        foreach (var type in typeof(SMain).Assembly.GetTypes().Where(t => typeof(Form).IsAssignableFrom(t) && !t.IsAbstract &&
            t != typeof(SMain) && t.GetConstructor(Type.EmptyTypes) != null).OrderBy(t => t.FullName))
        {
            Console.WriteLine("Checking window: " + type.FullName);
            using var window = (Form)Activator.CreateInstance(type)!;
            ServerUi.Apply(window);
            captions.Clear(); Collect(window);
            void Columns(Control control)
            {
                if (control is DataGridView grid) captions.AddRange(grid.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText));
                if (control is ListView list) captions.AddRange(list.Columns.Cast<ColumnHeader>().Select(c => c.Text));
                foreach (Control child in control.Controls) Columns(child);
            }
            Columns(window);
            windows[type.FullName!] = new(captions);
            PrepareForRender(window);
            using var screenshot = new Bitmap(window.Width, window.Height);
            window.DrawToBitmap(screenshot, new Rectangle(Point.Empty, window.Size));
            screenshot.Save(Path.Combine(output, type.Name + ".png"));
        }
        File.WriteAllText(Path.Combine(output, "windows.json"), JsonSerializer.Serialize(windows, new JsonSerializerOptions {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Assert(!Server.MirEnvir.Envir.Main.Running, "Probe must not start a game server");
        Console.WriteLine($"Server UI passed: caption changes, enum values, grid bindings, action keys, English fallback; actual main form {mainCaptionCount} captions and {windows.Count} additional windows. No server started.");
    }

    private static void PrepareForRender(Form form)
    {
        // Render off-screen without Load/Shown/closing handlers that can start
        // servers, load private account data or save configuration.
        var events = (EventHandlerList)typeof(Component).GetProperty("Events", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
        foreach (string name in new[] { "Load", "Shown", "FormClosing", "FormClosed" })
        {
            var field = typeof(Form).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(f => f.FieldType == typeof(object) && f.Name.Replace("_", "").Contains(name, StringComparison.OrdinalIgnoreCase));
            var key = field.GetValue(null)!;
            if (events[key] is { } handlers) events.RemoveHandler(key, handlers);
        }
        foreach (var field in form.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            if (field.GetValue(form) is System.Windows.Forms.Timer timer) timer.Stop();
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-30000, -30000);
        form.Show();
        form.Update();
    }

    private static void Assert(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
    }
}
