namespace Client.MirControls
{
    // Script lines are single rows. Each visible run is drawn exactly once,
    // using the same translated text, font and flags for measuring and drawing.
    internal sealed class DialogTextLayout
    {
        private const TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        private readonly MirLabel _row;
        private readonly List<MirLabel> _labels;
        private readonly MouseEventHandler _mouseWheel;
        private int _x;

        internal DialogTextLayout(MirLabel row, List<MirLabel> labels, MouseEventHandler mouseWheel)
        {
            _row = row;
            _labels = labels;
            _mouseWheel = mouseWheel;
        }

        internal void Append(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            // Script columns used large space runs for English labels. Keep a
            // small gap now that translated runs are laid out independently.
            text = System.Text.RegularExpressions.Regex.Replace(text, @"[ \t]{3,}", "  ");
            var label = new MirLabel
            {
                Parent = _row.Parent,
                Text = text,
                ForeColour = _row.ForeColour,
                NotControl = true
            };
            label.MouseWheel += _mouseWheel;
            _labels.Add(label);
            Append(label);
        }

        internal void Append(MirLabel label)
        {
            // ScaleFont consumes its argument; never pass the shared row font.
            var previousFont = label.Font;
            label.Font = new System.Drawing.Font(_row.Font.Name,
                _row.Font.Size * MirControl.FontDpiX / 96f, _row.Font.Style);
            previousFont.Dispose();
            label.Text = TrialLocalization.Text(label.Text);
            label.AutoSize = false;
            label.DrawFormat = Flags;
            Size measured = TextRenderer.MeasureText(CMain.Graphics, label.Text, label.Font,
                new Size(int.MaxValue, int.MaxValue), Flags);
            int width = measured.Width + (label.OutLine ? 2 : 1);
            label.Location = _row.Location.Add(new Point(_x, 0));
            label.Size = new Size(Math.Max(0, Math.Min(width, _row.Size.Width - _x)), _row.Size.Height);
            label.Visible = label.Size.Width > 0;
            _x += width;
        }
    }
}
