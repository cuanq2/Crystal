using WinControl = System.Windows.Forms.Control;

namespace Server.MirForms.VisualMapInfo.Control;

// Keep every editable record in Controls, but create/show native child windows
// only for rows inside the viewport. Scrolling does not discard pending edits.
public sealed class RegionEntryPanel : Panel
{
    private Predicate<WinControl> filter = _ => true;
    private bool arranging;

    public void SetFilter(Predicate<WinControl> value)
    {
        filter = value;
        AutoScrollPosition = Point.Empty;
        PerformLayout();
    }

    public void ScrollTo(WinControl entry)
    {
        int y = 0;
        foreach (WinControl row in Controls.Cast<WinControl>().Reverse())
        {
            if (!filter(row)) continue;
            if (row == entry)
            {
                AutoScrollPosition = new Point(0, y);
                PerformLayout();
                return;
            }
            y += row.Height;
        }
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        e.Control.Dock = DockStyle.None;
        e.Control.Visible = false;
        base.OnControlAdded(e);
    }
    protected override void OnScroll(ScrollEventArgs se) { base.OnScroll(se); PerformLayout(); }
    protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); PerformLayout(); }
    protected override void OnLayout(LayoutEventArgs levent)
    {
        if (arranging) return;
        arranging = true;
        try
        {
            var rows = Controls.Cast<WinControl>().Reverse().ToArray();
            int height = rows.Where(row => filter(row)).Sum(row => row.Height);
            AutoScrollMinSize = new Size(0, height);
            base.OnLayout(levent);
            int y = AutoScrollPosition.Y;
            foreach (WinControl row in rows)
            {
                bool included = filter(row);
                bool visible = included && y + row.Height > 0 && y < ClientSize.Height;
                if (visible)
                {
                    row.SetBounds(0, y, ClientSize.Width, row.Height);
                    row.Visible = true;
                }
                else row.Visible = false;
                if (included) y += row.Height;
            }
        }
        finally { arranging = false; }
    }
}
