using Server.MirForms.VisualMapInfo.Class;
using Server.MirForms.VisualMapInfo.Control;
using Server.MirEnvir;
using Server.MirDatabase;

namespace Server.MirForms.VisualMapInfo
{
    public partial class VForm : Form
    {
        MapRegionCanvas Canvas = new MapRegionCanvas();

        public Envir Envir => SMain.EditEnvir;

        public Point MouseDownLocation;
        private bool _mapLoaded;

        private void ReleaseMapResources()
        {
            VisualizerGlobal.FocusModeActivated -= FocusModeActivated;
            Canvas.Dispose();
            if (!_mapLoaded) return;
            _mapLoaded = false;
            VisualizerGlobal.FocusModeActive = false;
            VisualizerGlobal.SelectedFocusType = VisualizerGlobal.FocusType.None;
            VisualizerGlobal.FocusMineEntry = null;
            VisualizerGlobal.FocusRespawnEntry = null;
            VisualizerGlobal.SelectedTool = VisualizerGlobal.Tool.Select;
            VisualizerGlobal.Cursor = Cursors.Arrow;
            VisualizerGlobal.ZoomLevel = 1;
            var image = MapImage.Image;
            MapImage.Image = null;
            if (image != VisualizerGlobal.ClippingMap) image?.Dispose();
            VisualizerGlobal.ClippingMap?.Dispose();
            VisualizerGlobal.ClippingMap = null;
        }

        public VForm()
        {
            InitializeComponent();
            Server.MirForms.ServerUi.Apply(this);
        }

        private void VForm_Load(object sender, EventArgs e)
        {
            try { InitializeMap(); }
            catch (Exception ex)
            {
                ServerUi.Show("无法打开地图：" + ex.Message, "地图编辑器", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }
            InitializeMineInfo();
            InitializeRespawnInfo();
            _mapLoaded = true;
            VisualizerGlobal.FocusModeActivated += FocusModeActivated;
        }

        private void VForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_mapLoaded || e.Cancel) return;
            var entries = RespawnPanel.Controls.OfType<RespawnEntry>().ToArray();
            var values = new List<(RespawnEntry Entry, ushort Count, ushort Delay)>();
            foreach (var entry in entries)
            {
                if (!ushort.TryParse(entry.Count.Text, out ushort count) ||
                    !ushort.TryParse(entry.Delay.Text, out ushort delay))
                {
                    e.Cancel = true;
                    ServerUi.Show("刷新数量和延迟必须是 0 到 65535 的整数。请修正后再关闭。", "地图编辑器",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                values.Add((entry, count, delay));
            }

            var respawns = new List<RespawnInfo>();
            foreach (var (entry, count, delay) in values)
            {
                // Retain schedule/index fields that this editor does not expose.
                var info = entry.Tag as RespawnInfo ?? new RespawnInfo();
                info.Location = new Point(entry.X, entry.Y);
                info.MonsterIndex = entry.MonsterIndex;
                info.Spread = entry.Range;
                info.Count = count;
                info.Delay = delay;
                info.RoutePath = entry.RoutePath;
                info.Direction = entry.Direction;
                info.RandomDelay = entry.RandomDelay;
                respawns.Add(info);
            }
            var mines = MiningPanel.Controls.OfType<MineEntry>().Select(entry => new MineZone
            {
                Location = new Point(entry.X, entry.Y), Mine = entry.MineIndex, Size = entry.Range
            }).ToArray();
            VisualizerGlobal.MapInfo.Respawns.Clear();
            VisualizerGlobal.MapInfo.Respawns.AddRange(respawns);
            VisualizerGlobal.MapInfo.MineZones.Clear();
            VisualizerGlobal.MapInfo.MineZones.AddRange(mines);
        }

        private void InitializeMap()
        {
            ReadMap readMap = new ReadMap();

            readMap.mapFile = VisualizerGlobal.MapInfo.FileName;
            readMap.Load();

            MapImage.Image = VisualizerGlobal.ClippingMap;

            Canvas.Parent = MapImage;
            Canvas.MouseDown += MapImage_MouseDown;
            Canvas.Click += MapImage_Click;
            Canvas.BringToFront();

            MapDetailsLabel.Text =
                $"Map Name: {VisualizerGlobal.MapInfo.Title}   Width: {VisualizerGlobal.ClippingMap.Width}   Height: {VisualizerGlobal.ClippingMap.Height}";
        }

        private void InitializeMineInfo()
        {
            List<string> miningFilterItems = new() { { "Disabled" } };
            Settings.MineSetList.ForEach(x => miningFilterItems.Add(x.Name));
            miningFilterItems.Add("No Filter");

            MiningFilter.DataSource = miningFilterItems;
            MiningFilter.SelectedIndex = miningFilterItems.Count - 1;

            for (int i = 0; i < VisualizerGlobal.MapInfo.MineZones.Count; i++)
            {
                MineEntry MineRegion = new MineEntry();
                MineRegion.Dock = DockStyle.Top;
                MineRegion.MineIndex = VisualizerGlobal.MapInfo.MineZones[i].Mine;
                MineRegion.X = VisualizerGlobal.MapInfo.MineZones[i].Location.X;
                MineRegion.Y = VisualizerGlobal.MapInfo.MineZones[i].Location.Y;
                MineRegion.tempRange = VisualizerGlobal.MapInfo.MineZones[i].Size;
                MineRegion.Range = VisualizerGlobal.MapInfo.MineZones[i].Size;
                MineRegion.ShowControl();

                MiningPanel.Controls.Add(MineRegion);

                MineRegion.RegionHighlight.Parent = Canvas;
            }            
        }

        private void InitializeRespawnInfo()
        {
            RespawnsFilter.BeginUpdate();
            RespawnsFilter.Items.AddRange(Envir.MonsterInfoList.Cast<object>().ToArray());

            RespawnsFilter.Items.Add("No Filter");
            RespawnsFilter.SelectedIndex = RespawnsFilter.Items.Count - 1;
            RespawnsFilter.EndUpdate();

            var entries = new List<RespawnEntry>();
            for (int i = 0; i < VisualizerGlobal.MapInfo.Respawns.Count; i++)
            {
                RespawnEntry RespawnRegion = new RespawnEntry();
                RespawnRegion.Dock = DockStyle.Top;
                RespawnRegion.Tag = VisualizerGlobal.MapInfo.Respawns[i];
                RespawnRegion.MonsterIndex = VisualizerGlobal.MapInfo.Respawns[i].MonsterIndex;
                RespawnRegion.X = VisualizerGlobal.MapInfo.Respawns[i].Location.X;
                RespawnRegion.Y = VisualizerGlobal.MapInfo.Respawns[i].Location.Y;
                RespawnRegion.Range = VisualizerGlobal.MapInfo.Respawns[i].Spread;
                RespawnRegion.Count.Text = VisualizerGlobal.MapInfo.Respawns[i].Count.ToString();
                RespawnRegion.Delay.Text = VisualizerGlobal.MapInfo.Respawns[i].Delay.ToString();
                RespawnRegion.RoutePath = VisualizerGlobal.MapInfo.Respawns[i].RoutePath;
                RespawnRegion.Direction = VisualizerGlobal.MapInfo.Respawns[i].Direction;
                RespawnRegion.RandomDelay = VisualizerGlobal.MapInfo.Respawns[i].RandomDelay;
                RespawnRegion.HideControl();

                entries.Add(RespawnRegion);

                RespawnRegion.RegionHighlight.Parent = Canvas;
            }
            RespawnPanel.SuspendLayout();
            try { RespawnPanel.Controls.AddRange(entries.ToArray()); }
            finally { RespawnPanel.ResumeLayout(true); }
        }

        private void RedrawMap()
        {
            Bitmap Map = new Bitmap(
                VisualizerGlobal.ClippingMap.Width * VisualizerGlobal.ZoomLevel,
                VisualizerGlobal.ClippingMap.Height * VisualizerGlobal.ZoomLevel);

            using (Graphics g = Graphics.FromImage(Map))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(VisualizerGlobal.ClippingMap, 0, 0, Map.Width, Map.Height);
            }

            var previous = MapImage.Image;
            MapImage.Image = Map;
            if (previous != VisualizerGlobal.ClippingMap) previous?.Dispose();

            if (VisualizerGlobal.SelectedFocusType == VisualizerGlobal.FocusType.Mining)
                VisualizerGlobal.FocusMineEntry.UpdateForFocus(); 
            if (VisualizerGlobal.SelectedFocusType == VisualizerGlobal.FocusType.Respawn)
                VisualizerGlobal.FocusRespawnEntry.UpdateForFocus();
        }

        private void FocusModeActivated(object sender, EventArgs e)
        {
            for (int i = MiningPanel.Controls.Count - 1; i > -1; i--)
                try
                {
                    MineEntry MineControl = (MineEntry)MiningPanel.Controls[i];

                    MineControl.Visible = false;
                    MineControl.RegionHighlight.Visible = false;
                }
                catch (Exception)
                {
                    continue;
                }

            for (int i = RespawnPanel.Controls.Count - 1; i > -1; i--)
                try
                {
                    RespawnEntry RespawnControl = (RespawnEntry)RespawnPanel.Controls[i];

                    RespawnControl.RegionHighlight.Visible = false;
                }
                catch (Exception)
                {
                    continue;
                }

            RespawnPanel.SetFilter(control => VisualizerGlobal.SelectedFocusType == VisualizerGlobal.FocusType.Respawn &&
                ReferenceEquals(control, VisualizerGlobal.FocusRespawnEntry));

            if (VisualizerGlobal.SelectedFocusType == VisualizerGlobal.FocusType.Mining)
            {
                VisualizerGlobal.FocusMineEntry.Visible = true;
                VisualizerGlobal.FocusMineEntry.RegionHighlight.Visible = true;
                VisualizerGlobal.FocusMineEntry.UpdateForFocus();
            }
            if (VisualizerGlobal.SelectedFocusType == VisualizerGlobal.FocusType.Respawn)
            {
                VisualizerGlobal.FocusRespawnEntry.RegionHighlight.Visible = true;
                VisualizerGlobal.FocusRespawnEntry.UpdateForFocus();
            }

            EndFocus.Visible = true;
            FocusBreak.Visible = true;

            ToolSelectedChanged(MoveButton, null);
        }

        private void ToolSelectedChanged(object sender, EventArgs e)
        {
            MapImage.Cursor = Cursors.Arrow;

            ToolStripButton[] ToolButtons = new ToolStripButton[] { SelectButton, AddButton, MoveButton, ResizeButton };

            foreach (var Tool in ToolButtons)
                Tool.Checked = false;

            ToolStripButton ToolSender = (ToolStripButton)sender;
            ToolSender.Checked = true;

            switch (Server.MirForms.ServerUi.OriginalText(ToolSender))
            {
                case "Select Region":
                    VisualizerGlobal.SelectedTool = VisualizerGlobal.Tool.Select;
                    VisualizerGlobal.Cursor = Cursors.Arrow;
                    break;
                case "Add Region":
                    VisualizerGlobal.SelectedTool = VisualizerGlobal.Tool.Add;
                    VisualizerGlobal.Cursor = Cursors.UpArrow;
                    break;
                case "Move Region":
                    VisualizerGlobal.SelectedTool = VisualizerGlobal.Tool.Move;
                    VisualizerGlobal.Cursor = Cursors.SizeAll;
                    break;
                case "Resize Region":
                    VisualizerGlobal.SelectedTool = VisualizerGlobal.Tool.Resize;
                    VisualizerGlobal.Cursor = Cursors.SizeWE;
                    break;
                default:
                    break;
            }
        }
        
        private void EndFocus_Click(object sender, EventArgs e)
        {
            VisualizerGlobal.FocusModeActive = false;
            EndFocus.Visible = false;
            FocusBreak.Visible = false;

            MiningFilter.Enabled = true;
            MiningRemoveSelected.Enabled = true;

            RespawnsFilter.Enabled = true;
            RespawnsRemoveSelected.Enabled = true;

            VisualizerGlobal.ZoomLevel = 1;
            RedrawMap();

            if (VisualizerGlobal.SelectedFocusType == VisualizerGlobal.FocusType.Mining)
                MiningFilter_SelectedIndexChanged(MiningFilter, null);
            if (VisualizerGlobal.SelectedFocusType == VisualizerGlobal.FocusType.Respawn)
                RespawnsFilter_SelectedIndexChanged(RespawnsFilter, null);

            VisualizerGlobal.FocusMineEntry = null;
            VisualizerGlobal.FocusRespawnEntry = null;
            VisualizerGlobal.SelectedFocusType = VisualizerGlobal.FocusType.None;
        }

        private void MapImage_Click(object sender, EventArgs e)
        {
            if (RegionTabs.SelectedTab == null) return;
            if (Server.MirForms.ServerUi.OriginalText(RegionTabs.SelectedTab) == "Mining")
                if (VisualizerGlobal.SelectedTool == VisualizerGlobal.Tool.Add)
                {
                    MineEntry MineControl = new MineEntry()
                    {
                        Dock = DockStyle.Top,
                        X = MouseDownLocation.X,
                        Y = MouseDownLocation.Y,
                        Range = 50
                    };

                    MineControl.ShowControl();
                    MineControl.RegionHighlight.Parent = Canvas;

                    MiningPanel.Controls.Add(MineControl);

                    ToolSelectedChanged(MoveButton, e);
                }

            if (RegionTabs.SelectedTab == null) return;
            if (Server.MirForms.ServerUi.OriginalText(RegionTabs.SelectedTab) == "Respawns")
                if (VisualizerGlobal.SelectedTool == VisualizerGlobal.Tool.Add)
                {
                    RespawnEntry RespawnControl = new RespawnEntry()
                    {
                        Dock = DockStyle.Top,
                        X = MouseDownLocation.X,
                        Y = MouseDownLocation.Y,
                        Range = 50
                    };

                    RespawnControl.ShowControl();
                    RespawnControl.RegionHighlight.Parent = Canvas;

                    RespawnPanel.Controls.Add(RespawnControl);

                    ToolSelectedChanged(MoveButton, e);
                }
        }

        private void RegionTabs_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (RegionTabs.SelectedTab == null) return;
            if (Server.MirForms.ServerUi.OriginalText(RegionTabs.SelectedTab) == "Mining")
            {
                for (int i = RespawnPanel.Controls.Count; i > -1; --i)
                    try
                    {
                        RespawnEntry RespawnControl = (RespawnEntry)RespawnPanel.Controls[i];
                        RespawnControl.HideControl();
                    }
                    catch (Exception) { continue; }
                
                MiningFilter_SelectedIndexChanged(MiningFilter, null);
            }
            else if (Server.MirForms.ServerUi.OriginalText(RegionTabs.SelectedTab) == "Respawns")
            {
                for (int i = MiningPanel.Controls.Count; i > -1; --i)
                    try
                    {
                        MineEntry MineControl = (MineEntry)MiningPanel.Controls[i];
                        MineControl.HideControl();
                    }
                    catch (Exception) { continue; }

                RespawnsFilter_SelectedIndexChanged(RespawnsFilter, null);
            }
        }

        private void MapImage_MouseDown(object sender, MouseEventArgs e)
        {
            if (VisualizerGlobal.SelectedTool == VisualizerGlobal.Tool.Select) return;

            MouseDownLocation = e.Location;
        } 

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;  // Turn on WS_EX_COMPOSITED
                return cp;
            }
        }

        // Quick Keys
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.M)
            {
                ToolSelectedChanged(MoveButton, new EventArgs());

                return true;
            }

            if (keyData == Keys.S)
            {
                ToolSelectedChanged(SelectButton, new EventArgs());

                return true;
            }

            if (keyData == Keys.R)
            {
                ToolSelectedChanged(ResizeButton, new EventArgs());

                return true;
            }

            if (keyData == Keys.A)
            {
                ToolSelectedChanged(AddButton, new EventArgs());

                return true;
            }

            if (keyData == Keys.Add && VisualizerGlobal.FocusModeActive == true)
            {
                if (VisualizerGlobal.ZoomLevel != 6)
                    VisualizerGlobal.ZoomLevel++;

                RedrawMap();

                return true;
            }

            if (keyData == Keys.Subtract && VisualizerGlobal.FocusModeActive == true)
            {
                if (VisualizerGlobal.ZoomLevel != 1)
                    VisualizerGlobal.ZoomLevel--;

                RedrawMap();

                return true;
            }

            if (keyData == Keys.Left && VisualizerGlobal.FocusModeActive == true)
            {
                if (VisualizerGlobal.SelectedFocusType == VisualizerGlobal.FocusType.Mining)
                {
                    VisualizerGlobal.FocusMineEntry.X--;
                    VisualizerGlobal.FocusMineEntry.Range = VisualizerGlobal.FocusMineEntry.tempRange;
                }
                else if (VisualizerGlobal.SelectedFocusType == VisualizerGlobal.FocusType.Respawn)
                {
                    VisualizerGlobal.FocusRespawnEntry.X--;
                    VisualizerGlobal.FocusRespawnEntry.Range = VisualizerGlobal.FocusRespawnEntry.tempRange;
                }

                return true;
            } 
            
            if (keyData == Keys.Right && VisualizerGlobal.FocusModeActive == true)
            {
                if (VisualizerGlobal.SelectedFocusType == VisualizerGlobal.FocusType.Mining)
                {
                    VisualizerGlobal.FocusMineEntry.X++;
                    VisualizerGlobal.FocusMineEntry.Range = VisualizerGlobal.FocusMineEntry.tempRange;
                }
                else if (VisualizerGlobal.SelectedFocusType == VisualizerGlobal.FocusType.Respawn)
                {
                    VisualizerGlobal.FocusRespawnEntry.X++;
                    VisualizerGlobal.FocusRespawnEntry.Range = VisualizerGlobal.FocusRespawnEntry.tempRange;
                }

                return true;
            }

            if (keyData == Keys.Up && VisualizerGlobal.FocusModeActive == true)
            {
                if (VisualizerGlobal.SelectedFocusType == VisualizerGlobal.FocusType.Mining)
                {
                    VisualizerGlobal.FocusMineEntry.Y--;
                    VisualizerGlobal.FocusMineEntry.Range = VisualizerGlobal.FocusMineEntry.tempRange;
                }
                else if (VisualizerGlobal.SelectedFocusType == VisualizerGlobal.FocusType.Respawn)
                {
                    VisualizerGlobal.FocusRespawnEntry.Y--;
                    VisualizerGlobal.FocusRespawnEntry.Range = VisualizerGlobal.FocusRespawnEntry.tempRange;
                }

                return true;
            }

            if (keyData == Keys.Down && VisualizerGlobal.FocusModeActive == true)
            {
                if (VisualizerGlobal.SelectedFocusType == VisualizerGlobal.FocusType.Mining)
                {
                    VisualizerGlobal.FocusMineEntry.Y++;
                    VisualizerGlobal.FocusMineEntry.Range = VisualizerGlobal.FocusMineEntry.tempRange;
                }
                else if (VisualizerGlobal.SelectedFocusType == VisualizerGlobal.FocusType.Respawn)
                {
                    VisualizerGlobal.FocusRespawnEntry.Y++;
                    VisualizerGlobal.FocusRespawnEntry.Range = VisualizerGlobal.FocusRespawnEntry.tempRange;
                }

                return true;
            }

            if (keyData == Keys.Escape && VisualizerGlobal.FocusModeActive == true)
            {
                EndFocus_Click(EndFocus, null);

                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        #region "START Mining Tool Bar"

        private void MiningSelectAll_Click(object sender, EventArgs e)
        {
            for (int i = MiningPanel.Controls.Count - 1; i > -1; i--)
            {
                MineEntry MineControl = (MineEntry)MiningPanel.Controls[i];
                MineControl.Selected.Checked = true;
            }
        }

        private void MiningSelectNone_Click(object sender, EventArgs e)
        {
            for (int i = MiningPanel.Controls.Count - 1; i > -1; i--)
            {
                MineEntry MineControl = (MineEntry)MiningPanel.Controls[i];
                MineControl.Selected.Checked = false;
            }
        }

        private void MiningInvertSelection_Click(object sender, EventArgs e)
        {
            for (int i = MiningPanel.Controls.Count - 1; i > -1; i--)
            {
                MineEntry MineControl = (MineEntry)MiningPanel.Controls[i];
                MineControl.Selected.Checked = !MineControl.Selected.Checked;
            }
        }

        private void MiningRemoveSelected_Click(object sender, EventArgs e)
        {
            if (MiningPanel.Controls.Count == 0) return;

            DialogResult result = Server.MirForms.ServerUi.Show("Remove selected records?", "", MessageBoxButtons.YesNoCancel);
            if (result != DialogResult.Yes) return;

            for (int i = MiningPanel.Controls.Count; i > -1; --i)
            {
                try
                {
                    MineEntry MineControl = (MineEntry)MiningPanel.Controls[i];
                    if (MineControl.Selected.Checked == true)
                        MineControl.RemoveEntry();
                }
                catch (Exception)
                {
                    continue;
                }
            }
        }

        private void MiningHideRegion_Click(object sender, EventArgs e)
        {
            for (int i = MiningPanel.Controls.Count - 1; i > -1; i--)
            {
                try
                {
                    MineEntry MineControl = (MineEntry)MiningPanel.Controls[i];
                    if (MineControl.Selected.Checked == true)
                        MineControl.HideRegion();
                }
                catch (Exception)
                {
                    continue;
                }
            }
        }

        private void MiningShowRegion_Click(object sender, EventArgs e)
        {
            for (int i = MiningPanel.Controls.Count - 1; i > -1; i--)
            {
                try
                {
                    MineEntry MineControl = (MineEntry)MiningPanel.Controls[i];
                    if (MineControl.Selected.Checked == true)
                        MineControl.ShowRegion();
                }
                catch (Exception)
                {
                    continue;
                }
            }
        }

        private void MiningFocusRegion_Click(object sender, EventArgs e)
        {
            VisualizerGlobal.SelectedTool = VisualizerGlobal.Tool.Focus;
            VisualizerGlobal.Cursor = Cursors.Hand;

            MiningFilter.Enabled = false;
            MiningRemoveSelected.Enabled = false;
        }

        private void MiningFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            VisualizerGlobal.ZoomLevel = 1;

            if (Server.MirForms.ServerUi.OriginalText(MiningFilter) == "No Filter")
                for (int i = MiningPanel.Controls.Count - 1; i > -1; i--)
                    try
                    {
                        MineEntry MineControl = (MineEntry)MiningPanel.Controls[i];

                        MineControl.Visible = true;
                        if (!MineControl.RegionHidden)
                            MineControl.RegionHighlight.Visible = true;
                    }
                    catch (Exception)
                    {
                        continue;
                    }
            else
                for (int i = MiningPanel.Controls.Count - 1; i > -1; i--)
                    try
                    {
                        MineEntry MineControl = (MineEntry)MiningPanel.Controls[i];

                        if (MineControl.MineIndex == MiningFilter.SelectedIndex)
                        {
                            MineControl.Visible = true;

                            if (!MineControl.RegionHidden)
                                MineControl.RegionHighlight.Visible = true;
                        }
                        else
                        {
                            MineControl.RegionHighlight.Visible = false;
                            MineControl.Visible = false;
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }
        }

        #endregion "END Mining Tool Bar"

        #region "START Respawn Tool Bar

        private void RespawnsSelectAll_Click(object sender, EventArgs e)
        {
            for (int i = RespawnPanel.Controls.Count - 1; i > -1; i--)
            {
                RespawnEntry RespawnControl = (RespawnEntry)RespawnPanel.Controls[i];
                RespawnControl.Selected.Checked = true;
            }
        }

        private void RespawnsSelectNone_Click(object sender, EventArgs e)
        {
            for (int i = RespawnPanel.Controls.Count - 1; i > -1; i--)
            {
                RespawnEntry RespawnControl = (RespawnEntry)RespawnPanel.Controls[i];
                RespawnControl.Selected.Checked = false;
            }
        }

        private void RespawnsRemoveSelected_Click(object sender, EventArgs e)
        {
            if (RespawnPanel.Controls.Count == 0) return;

            DialogResult result = Server.MirForms.ServerUi.Show("Remove selected records?", "", MessageBoxButtons.YesNoCancel);
            if (result != DialogResult.Yes) return;

            for (int i = RespawnPanel.Controls.Count; i > -1; --i)
            {
                try
                {
                    RespawnEntry RespawnControl = (RespawnEntry)RespawnPanel.Controls[i];
                    if (RespawnControl.Selected.Checked == true)
                    {
                        RespawnControl.RemoveEntry();
                    }
                }
                catch (Exception)
                {
                    continue;
                }
            }
        }

        private void ResapwnsHideRegion_Click(object sender, EventArgs e)
        {
            for (int i = RespawnPanel.Controls.Count - 1; i > -1; i--)
            {
                try
                {
                    RespawnEntry RespawnControl = (RespawnEntry)RespawnPanel.Controls[i];
                    if (RespawnControl.Selected.Checked == true)
                        RespawnControl.HideRegion();
                }
                catch (Exception)
                {
                    continue;
                }
            }
        }

        private void ResapwnsShowRegion_Click(object sender, EventArgs e)
        {
            for (int i = RespawnPanel.Controls.Count - 1; i > -1; i--)
            {
                try
                {
                    RespawnEntry RespawnControl = (RespawnEntry)RespawnPanel.Controls[i];
                    if (RespawnControl.Selected.Checked == true)
                        RespawnControl.ShowRegion();
                }
                catch (Exception)
                {
                    continue;
                }
            }
        }

        private void ResapwnsFocusRegion_Click(object sender, EventArgs e)
        {
            VisualizerGlobal.SelectedTool = VisualizerGlobal.Tool.Focus;
            VisualizerGlobal.Cursor = Cursors.Hand;

            RespawnsFilter.Enabled = false;
            RespawnsRemoveSelected.Enabled = false;
        }
        
        private void RespawnsInvertSelection_Click(object sender, EventArgs e)
        {
            for (int i = RespawnPanel.Controls.Count - 1; i > -1; i--)
            {
                RespawnEntry RespawnControl = (RespawnEntry)RespawnPanel.Controls[i];
                RespawnControl.Selected.Checked = !RespawnControl.Selected.Checked;
            }
        }

        private void RespawnsFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            var info = RespawnsFilter.SelectedItem as MonsterInfo;
            bool all = ServerUi.OriginalText(RespawnsFilter) == "No Filter";
            VisualizerGlobal.ZoomLevel = 1;
            RespawnPanel.SetFilter(control => all || control is RespawnEntry entry && entry.MonsterIndex == info?.Index);
            foreach (RespawnEntry entry in RespawnPanel.Controls)
                entry.RegionHighlight.Visible = (all || entry.MonsterIndex == info?.Index) && !entry.RegionHidden;
        }

        #endregion "END Respawn Tool Bar

        private void RegionTabs_Selecting(object sender, TabControlCancelEventArgs e)
        {
            if (VisualizerGlobal.SelectedFocusType != VisualizerGlobal.FocusType.None)
                e.Cancel = true;
        }



        

        

    }
}
