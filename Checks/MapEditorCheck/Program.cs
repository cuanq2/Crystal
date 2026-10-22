using Server;
using Server.MirDatabase;
using Server.MirForms.VisualMapInfo;
using Server.MirForms.VisualMapInfo.Class;
using Server.MirForms.VisualMapInfo.Control;
using System.Reflection;
using System.Windows.Forms;
using System.Drawing;

internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private)!.GetValue(owner)!;
    private static void Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private)!.Invoke(owner, args);
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Mouse(MapRegionCanvas canvas, string name, int x, int y, MouseButtons buttons = MouseButtons.Left) =>
        Call(canvas, name, new MouseEventArgs(buttons, 1, x, y, 0));

    [STAThread] static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[0]);
        if (Directory.Exists(output)) throw new ArgumentException("Use a new output directory.");
        string runtime = args.Length > 1 ? Path.GetFullPath(args[1]) : null;
        Directory.CreateDirectory(output); Directory.SetCurrentDirectory(output);
        using var deadline = new System.Threading.Timer(_ => Environment.Exit(124), null, 60000, Timeout.Infinite);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Settings.Load();
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Directory.CreateDirectory("Maps");
        var bytes = new byte[8 + 32 * 32 * 26];
        bytes[0] = 1; bytes[2] = (byte)'C'; bytes[3] = (byte)'#'; bytes[4] = 32; bytes[6] = 32;
        File.WriteAllBytes("Maps/probe.map", bytes);
        var classic = new byte[52 + 32 * 32 * 12]; classic[0] = 32; classic[2] = 32;
        File.WriteAllBytes("Maps/classic.map", classic);
        SMain.EditEnvir.MonsterInfoList.Add(new MonsterInfo { Index = 1, Name = "Probe" });

        foreach (string language in new[] { "Chinese", "English" })
        {
            Settings.Language = language;
            for (int cycle = 0; cycle < 3; cycle++)
            {
                var map = new MapInfo { Index = 1, FileName = cycle == 1 ? "classic" : "probe", Title = "Probe" };
                var spawn = new RespawnInfo { MonsterIndex = 1, Location = new Point(10, 10), Count = 1, Spread = 2, Delay = 5, SaveRespawnTime = true, RespawnTicks = 17 };
                map.Respawns.Add(spawn);
                map.MineZones.Add(new MineZone { Location = new Point(5, 5), Size = 2 });
                Exercise(map, language + cycle, true, cycle == 2);
                if (cycle != 2)
                {
                    Assert(map.Respawns.Count == 1 && ReferenceEquals(map.Respawns[0], spawn), "Keep the original respawn record");
                    Assert(spawn.SaveRespawnTime && spawn.RespawnTicks == 17, "Preserve fields not edited by the visualizer");
                }
                else Assert(map.Respawns.Count == 1 && map.Respawns[0] != spawn, "Deleted record stays deleted, added record is saved");
            }
        }

        // Closing the visualizer may reduce a parent's list. Restoring selected
        // indices from the old list used to throw ArgumentOutOfRangeException.
        using (var parent = new MapInfoForm())
        {
            var map = new MapInfo(); var spawn = new RespawnInfo(); var mine = new MineZone();
            typeof(MapInfoForm).GetField("_info", Private)!.SetValue(parent, map);
            var respawns = Field<ListBox>(parent, "RespawnInfoListBox");
            var mines = Field<ListBox>(parent, "MZListlistBox");
            respawns.Items.Add(spawn); mines.Items.Add(mine);
            Call(parent, "RefreshRespawnList"); Call(parent, "RefreshMineZoneList");
            Assert(respawns.Items.Count == 0 && mines.Items.Count == 0, "Refresh shortened lists");
        }

        if (runtime != null)
        {
            // Only copy the content database and the first map. Never load accounts.
            File.Copy(Path.Combine(runtime, "Server.MirDB"), "Server.MirDB");
            SMain.EditEnvir.MapInfoList.Clear(); SMain.EditEnvir.MonsterInfoList.Clear();
            Assert(SMain.EditEnvir.LoadDB(), "Load isolated database copy");
            var first = SMain.EditEnvir.MapInfoList[0];
            string destination = Path.Combine("Maps", first.FileName + ".map");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(Path.Combine(runtime, destination), destination, true);
            Console.WriteLine($"FIRST MAP: {first.Index} / {first.FileName} / {first.Title}; respawns={first.Respawns.Count}");
            Settings.Language = "Chinese";
            Exercise(first, "first-map", false, false);
        }
        Assert(!Server.MirEnvir.Envir.Main.Running, "Never start a game server");
        Assert(!typeof(VForm).Assembly.GetReferencedAssemblies().Any(a => a.Name!.Contains("PowerPacks")), "No legacy PowerPacks dependency");
        Console.WriteLine("PASS: tab switching, painting, hover, drag, focus/zoom, delete/add, close/reopen, classic maps, parent refresh, original data retained.");
    }

    private static void Exercise(MapInfo map, string name, bool edit, bool remove)
    {
        VisualizerGlobal.MapInfo = map;
        using (var window = new VForm { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000) })
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            window.Show(); Application.DoEvents();
            Console.WriteLine($"PERF {name} open: {timer.ElapsedMilliseconds} ms");
            var tabs = Field<TabControl>(window, "RegionTabs");
            for (int i = 0; i < 8; i++)
            {
                timer.Restart(); tabs.SelectedIndex = i % 2; Application.DoEvents();
                Console.WriteLine($"PERF {name} tab {i % 2}: {timer.ElapsedMilliseconds} ms");
            }
            var canvas = Field<MapRegionCanvas>(window, "Canvas");
            var panel = Field<Panel>(window, "RespawnPanel");
            if (panel.Controls.Count > 100)
            {
                var viewport = (RegionEntryPanel)panel;
                int nativeRows = panel.Controls.Cast<RespawnEntry>().Count(row => row.IsHandleCreated);
                Assert(nativeRows < 30, "First display must not create every native row window");
                Console.WriteLine($"PERF {name} native rows: {nativeRows}/{panel.Controls.Count}");
                timer.Restart(); Server.MirForms.ServerUi.Apply(window);
                Console.WriteLine($"PERF {name} localization refresh: {timer.ElapsedMilliseconds} ms");
                var row = (RespawnEntry)panel.Controls[0];
                viewport.ScrollTo(row); Application.DoEvents();
                Assert(row.Visible && row.Top >= 0 && row.Bottom <= panel.ClientSize.Height, "Scroll to an off-screen record");
                var combo = Field<ComboBox>(row, "MonsterComboBox");
                int monsterIndex = row.MonsterIndex;
                Assert(combo.Items.Count <= 1, "Unopened dropdown stores only current monster");
                timer.Restart(); Call(row, "MonsterComboBox_DropDown", combo, EventArgs.Empty);
                Console.WriteLine($"PERF {name} monster dropdown: {timer.ElapsedMilliseconds} ms; options={combo.Items.Count}");
                Assert(combo.Items.Count == SMain.EditEnvir.MonsterInfoList.Count && row.MonsterIndex == monsterIndex,
                    "Opening lazy dropdown retains selection and exposes all monsters");
                string oldCount = row.Count.Text;
                row.Count.Text = "123";
                viewport.ScrollTo(panel.Controls[panel.Controls.Count - 1]); Application.DoEvents();
                viewport.ScrollTo(row); Application.DoEvents();
                Assert(row.Count.Text == "123" && row.MonsterIndex == monsterIndex, "Scrolling retains unsaved fields and selection");
                row.Count.Text = oldCount;
                viewport.ScrollTo(panel.Controls[panel.Controls.Count - 1]);
                Call(viewport, "OnMouseWheel", new MouseEventArgs(MouseButtons.None, 0, 10, 10, -120));
                Application.DoEvents();
                Assert(viewport.AutoScrollPosition.Y < 0 && panel.Controls.Cast<RespawnEntry>().Count(r => r.Visible) < 30,
                    "Mouse wheel scroll updates the viewport without showing every record");
                var filter = Field<ComboBox>(window, "RespawnsFilter");
                filter.SelectedItem = SMain.EditEnvir.MonsterInfoList.First(m => m.Index == monsterIndex);
                Application.DoEvents();
                Assert(panel.Controls.Cast<RespawnEntry>().Where(r => r.Visible).All(r => r.MonsterIndex == monsterIndex), "Viewport respects monster filter");
                Assert(panel.Controls.Cast<RespawnEntry>().All(r => r.RegionHighlight.Visible == (r.MonsterIndex == monsterIndex && !r.RegionHidden)), "Map regions respect filter independently of scrolling");
                filter.SelectedIndex = filter.Items.Count - 1; Application.DoEvents();
            }
            if (panel.Controls.Count > 0)
            {
                var entry = (RespawnEntry)panel.Controls[0];
                var region = entry.RegionHighlight;
                int x = region.Left + 1, y = region.Top + 1;
                Mouse(canvas, "OnMouseMove", x, y, MouseButtons.None);
                Call(canvas, "OnMouseHover", EventArgs.Empty);
                if (edit)
                {
                    VisualizerGlobal.SelectedTool = VisualizerGlobal.Tool.Move;
                    int oldX = entry.X, oldY = entry.Y;
                    Mouse(canvas, "OnMouseDown", x, y); Mouse(canvas, "OnMouseMove", x + 2, y + 3);
                    Mouse(canvas, "OnMouseUp", x + 2, y + 3);
                    Assert(entry.X == oldX + 2 && entry.Y == oldY + 3, "Drag updates map coordinates");
                    VisualizerGlobal.SelectedTool = VisualizerGlobal.Tool.Focus;
                    x = region.Left + 1; y = region.Top + 1;
                    Mouse(canvas, "OnMouseDown", x, y); Mouse(canvas, "OnMouseClick", x, y); Mouse(canvas, "OnMouseUp", x, y);
                    Assert(VisualizerGlobal.FocusModeActive, "Click focuses a region");
                    VisualizerGlobal.ZoomLevel = 2; Call(window, "RedrawMap");
                    Call(window, "EndFocus_Click", window, EventArgs.Empty);
                    Assert(!VisualizerGlobal.FocusModeActive, "End focus resets mode");
                    if (remove)
                    {
                        entry.RemoveEntry();
                        Assert(panel.Controls.Count == 0, "Delete region safely");
                        VisualizerGlobal.SelectedTool = VisualizerGlobal.Tool.Add;
                        Mouse(canvas, "OnMouseDown", 30, 30); Call(canvas, "OnClick", EventArgs.Empty); Mouse(canvas, "OnMouseUp", 30, 30);
                        Assert(panel.Controls.Count == 1, "Blank canvas click adds a region");
                    }
                }
            }
            window.Refresh(); Application.DoEvents();
            using var bitmap = new Bitmap(window.Width, window.Height);
            window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, window.Size)); bitmap.Save(name + ".png");
            window.Close(); Application.DoEvents();
        }
        Assert(VisualizerGlobal.ClippingMap == null && VisualizerGlobal.FocusRespawnEntry == null && VisualizerGlobal.FocusMineEntry == null, "Dispose map and focus references");
        var subscribers = typeof(VisualizerGlobal).GetField("FocusModeActivated", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
        Assert(subscribers == null, "Unsubscribe global focus event");
    }
}
