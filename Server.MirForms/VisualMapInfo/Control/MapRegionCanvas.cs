using System.ComponentModel;
using WinControl = System.Windows.Forms.Control;

namespace Server.MirForms.VisualMapInfo.Control;

// A managed overlay: PowerPacks shapes reference ContextMenu, which no longer
// exists in .NET 8. Draw all regions together so overlapping areas stay visible.
public sealed class MapRegionCanvas : WinControl
{
    internal readonly List<MapRegion> Regions = new();
    private MapRegion hovered, pressed;

    public MapRegionCanvas()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Transparent;
        Dock = DockStyle.Fill;
        TabStop = false;
    }

    private MapRegion Hit(Point point) => Regions.LastOrDefault(r => r.Visible && r.Bounds.Contains(point));
    private static MouseEventArgs Local(MapRegion region, MouseEventArgs e) =>
        new(e.Button, e.Clicks, e.X - region.Left, e.Y - region.Top, e.Delta);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        foreach (var region in Regions)
        {
            if (!region.Visible || region.Size.Width <= 0 || region.Size.Height <= 0) continue;
            if (region.FillStyle == RegionFillStyle.Solid)
            {
                using var brush = new SolidBrush(region.FillColor);
                e.Graphics.FillRectangle(brush, region.Bounds);
            }
            using var pen = new Pen(region.BorderColor, region.BorderWidth);
            e.Graphics.DrawRectangle(pen, region.Left, region.Top, region.Size.Width, region.Size.Height);
        }
    }

    private void Hover(MapRegion region)
    {
        if (hovered == region) return;
        hovered?.Leave();
        hovered = region;
        hovered?.Enter();
        Cursor = hovered?.Cursor ?? Cursors.Default;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        Hover(Hit(e.Location));
        var target = pressed ?? hovered;
        if (target != null) target.Move(Local(target, e)); else base.OnMouseMove(e);
        Cursor = target?.Cursor ?? Cursors.Default;
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        pressed = Hit(e.Location);
        if (pressed != null) { Capture = true; pressed.Down(Local(pressed, e)); }
        else base.OnMouseDown(e);
    }
    protected override void OnClick(EventArgs e)
    {
        if (pressed == null) base.OnClick(e);
    }
    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (pressed != null) pressed.Click(Local(pressed, e)); else base.OnMouseClick(e);
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        pressed = null; Capture = false; base.OnMouseUp(e);
    }
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        if (!Capture) pressed = null;
        base.OnMouseCaptureChanged(e);
    }
    protected override void OnMouseLeave(EventArgs e) { Hover(null); base.OnMouseLeave(e); }
    protected override void OnMouseHover(EventArgs e) { hovered?.Hover(); base.OnMouseHover(e); }
    internal void Remove(MapRegion region)
    {
        if (hovered == region) Hover(null);
        if (pressed == region) { pressed = null; Capture = false; }
        Regions.Remove(region); Invalidate();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            hovered = pressed = null;
            foreach (var region in Regions.ToArray()) region.Dispose();
        }
        base.Dispose(disposing);
    }
}

public enum RegionFillStyle { Transparent, Solid }

public sealed class MapRegion : Component
{
    private MapRegionCanvas parent;
    private Rectangle bounds;
    private bool visible = true;
    private Color borderColor = Color.Lime, fillColor;
    private int borderWidth = 1;
    private RegionFillStyle fillStyle;
    public MapRegionCanvas Parent
    {
        get => parent;
        set
        {
            if (parent == value) return;
            parent?.Remove(this); parent = value;
            if (parent != null) { parent.Regions.Add(this); parent.Invalidate(); }
        }
    }
    public Rectangle Bounds => bounds;
    public int Left { get => bounds.X; set { bounds.X = value; parent?.Invalidate(); } }
    public int Top { get => bounds.Y; set { bounds.Y = value; parent?.Invalidate(); } }
    public Size Size { get => bounds.Size; set { bounds.Size = value; parent?.Invalidate(); } }
    public bool Visible { get => visible; set { visible = value; parent?.Invalidate(); } }
    public Color BorderColor { get => borderColor; set { borderColor = value; parent?.Invalidate(); } }
    public Color FillColor { get => fillColor; set { fillColor = value; parent?.Invalidate(); } }
    public int BorderWidth { get => borderWidth; set { borderWidth = value; parent?.Invalidate(); } }
    public RegionFillStyle FillStyle { get => fillStyle; set { fillStyle = value; parent?.Invalidate(); } }
    public Cursor Cursor { get; set; } = Cursors.Default;
    public event MouseEventHandler MouseMove, MouseDown, MouseClick;
    public event EventHandler MouseEnter, MouseLeave, MouseHover;
    internal void Move(MouseEventArgs e) => MouseMove?.Invoke(this, e);
    internal void Down(MouseEventArgs e) => MouseDown?.Invoke(this, e);
    internal void Click(MouseEventArgs e) => MouseClick?.Invoke(this, e);
    internal void Enter() => MouseEnter?.Invoke(this, EventArgs.Empty);
    internal void Leave() => MouseLeave?.Invoke(this, EventArgs.Empty);
    internal void Hover() => MouseHover?.Invoke(this, EventArgs.Empty);
    public void BringToFront()
    {
        if (parent == null) return;
        parent.Regions.Remove(this); parent.Regions.Add(this); parent.Invalidate();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) Parent = null;
        base.Dispose(disposing);
    }
}
