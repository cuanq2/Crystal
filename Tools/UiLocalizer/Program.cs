using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;

if (args.Length < 4 || args[0] is not ("frame" or "sheet" or "build") || (args[0] == "sheet" && args.Length < 5))
    throw new ArgumentException("Usage: UiLocalizer frame <lib> <output> <indexes> | sheet <lib> <output> <start> <end> | build <data-dir> <output-dir> <patches.json>");
string source = Path.GetFullPath(args[1]), output = Path.GetFullPath(args[2]);
if (args[0] == "build" && source.TrimEnd(Path.DirectorySeparatorChar).Equals(output.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("Build output must differ from the original asset directory.");
Directory.CreateDirectory(output);
if (args[0] == "frame")
{
    var lib = new Library(source);
    foreach (var token in args[3].Split(','))
    {
        int index = int.Parse(token);
        using var bm = lib.Image(index);
        bm.Save(Path.Combine(output, $"{Path.GetFileNameWithoutExtension(source)}-{index}.png"));
    }
}
else if (args[0] == "sheet")
{
    var lib = new Library(source);
    int start = int.Parse(args[3]), end = Math.Min(lib.Count, int.Parse(args[4]));
    var indexes = Enumerable.Range(start, end - start).Where(i => lib.Size(i).Width > 0 && lib.Size(i).Height > 0).ToArray();
    for (int page = 0; page * 80 < indexes.Length; page++)
    {
        var slice = indexes.Skip(page * 80).Take(80).ToArray();
        using var sheet = new Bitmap(1200, ((slice.Length + 5) / 6) * 85);
        using var g = Graphics.FromImage(sheet);
        g.Clear(Color.FromArgb(34, 34, 38));
        using var font = new Font("Arial", 10);
        for (int n = 0; n < slice.Length; n++)
        {
            int index = slice[n], x = n % 6 * 200, y = n / 6 * 85;
            using var bm = lib.Image(index);
            float scale = Math.Min(1f, Math.Min(195f / bm.Width, 62f / bm.Height));
            g.DrawString($"{index} ({bm.Width}x{bm.Height})", font, Brushes.White, x, y);
            g.DrawImage(bm, x, y + 20, bm.Width * scale, bm.Height * scale);
        }
        sheet.Save(Path.Combine(output, $"{Path.GetFileNameWithoutExtension(source)}-{start}-{page}.png"));
    }
    Console.WriteLine($"Exported {indexes.Length} frames.");
}
else if (args[0] == "build")
{
    var patches = JsonSerializer.Deserialize<List<Patch>>(File.ReadAllText(args[3]), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    foreach (var group in patches.GroupBy(p => p.Library))
    {
        var lib = new Library(Path.Combine(source, group.Key + ".Lib"));
        foreach (var patch in group)
        {
            using var bm = lib.Image(patch.Index);
            using var g = Graphics.FromImage(bm);
            var rect = patch.Rect is { Length: 4 } ? new Rectangle(patch.Rect[0], patch.Rect[1], patch.Rect[2], patch.Rect[3]) : new Rectangle(Point.Empty, bm.Size);
            if (patch.Style == "label")
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                using var clear = new SolidBrush(Color.Transparent);
                g.FillRectangle(clear, rect);
                g.CompositingMode = CompositingMode.SourceOver;
            }
            else
            {
                using var background = new LinearGradientBrush(rect, Color.FromArgb(48, 41, 33), Color.FromArgb(20, 18, 17), LinearGradientMode.Vertical);
                g.FillRectangle(background, rect);
                if (patch.Style == "button")
                {
                    using var border = new Pen(Color.FromArgb(150, 119, 68));
                    g.DrawRectangle(border, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
                }
            }
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            float size = Math.Min(18, Math.Max(9, rect.Height * .62f));
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            Font font = new Font("Microsoft YaHei", size, FontStyle.Bold, GraphicsUnit.Pixel);
            while (g.MeasureString(patch.Text, font).Width > rect.Width - 2 && size > 8)
            {
                font.Dispose(); font = new Font("Microsoft YaHei", --size, FontStyle.Bold, GraphicsUnit.Pixel);
            }
            using (font)
            using (var ink = new SolidBrush(Color.FromArgb(239, 214, 159)))
                g.DrawString(patch.Text, font, ink, rect, format);
            g.Dispose();
            lib.Replace(patch.Index, bm);
        }
        lib.Save(Path.Combine(output, group.Key + ".Lib"));
        Console.WriteLine($"{group.Key}: {group.Count()} patches");
    }
}

record Patch(string Library, int Index, string Text, string Style, int[]? Rect);
sealed class Library
{
    readonly byte[] raw; readonly int version, frameSeek; readonly int[] offsets;
    readonly Dictionary<int, byte[]> replacements = new();
    public int Count => offsets.Length;
    public Library(string path)
    {
        raw = File.ReadAllBytes(path); using var r = new BinaryReader(new MemoryStream(raw));
        version = r.ReadInt32(); int count = r.ReadInt32(); frameSeek = version >= 3 ? r.ReadInt32() : raw.Length;
        offsets = Enumerable.Range(0, count).Select(_ => r.ReadInt32()).ToArray();
    }
    public Size Size(int i) => new(BitConverter.ToInt16(raw, offsets[i]), BitConverter.ToInt16(raw, offsets[i] + 2));
    public Bitmap Image(int i)
    {
        byte[] data = replacements.TryGetValue(i, out var replaced) ? replaced : raw;
        int o = ReferenceEquals(data, raw) ? offsets[i] : 0;
        var s = Size(i); int len = BitConverter.ToInt32(data, o + 13);
        using var gzip = new GZipStream(new MemoryStream(data, o + 17, len), CompressionMode.Decompress);
        using var pixels = new MemoryStream(); gzip.CopyTo(pixels); var bytes = pixels.ToArray();
        if (bytes.Length != s.Width * s.Height * 4) throw new Exception($"Invalid frame {i}");
        var bm = new Bitmap(s.Width, s.Height, PixelFormat.Format32bppArgb);
        var locked = bm.LockBits(new Rectangle(Point.Empty, s), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < s.Height; y++) Marshal.Copy(bytes, y * s.Width * 4, IntPtr.Add(locked.Scan0, y * locked.Stride), s.Width * 4);
        bm.UnlockBits(locked); return bm;
    }
    public void Replace(int i, Bitmap bm)
    {
        var bytes = new byte[bm.Width * bm.Height * 4];
        var locked = bm.LockBits(new Rectangle(Point.Empty, bm.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < bm.Height; y++) Marshal.Copy(IntPtr.Add(locked.Scan0, y * locked.Stride), bytes, y * bm.Width * 4, bm.Width * 4);
        bm.UnlockBits(locked);
        using var compressed = new MemoryStream(); using (var gzip = new GZipStream(compressed, CompressionLevel.Optimal, true)) gzip.Write(bytes);
        using var record = new MemoryStream(); record.Write(raw, offsets[i], 12); record.WriteByte((byte)(raw[offsets[i] + 12] & 0x7f));
        using var writer = new BinaryWriter(record); writer.Write((int)compressed.Length); writer.Write(compressed.ToArray()); replacements[i] = record.ToArray();
    }
    public void Save(string path)
    {
        using var target = new MemoryStream(); using var w = new BinaryWriter(target);
        w.Write(version); w.Write(Count); if (version >= 3) w.Write(0);
        int header = (int)target.Position; for (int i = 0; i < Count; i++) w.Write(0);
        var positions = new int[Count];
        for (int i = 0; i < Count; i++)
        {
            positions[i] = (int)target.Position;
            if (replacements.TryGetValue(i, out var record)) w.Write(record);
            else
            {
                int o = offsets[i], length = 17 + BitConverter.ToInt32(raw, o + 13);
                if ((raw[o + 12] & 128) != 0) length += 12 + BitConverter.ToInt32(raw, o + length + 8);
                w.Write(raw, o, length);
            }
        }
        int frames = (int)target.Position; if (version >= 3) w.Write(raw, frameSeek, raw.Length - frameSeek);
        target.Position = 8; if (version >= 3) w.Write(frames); target.Position = header; foreach (int pos in positions) w.Write(pos);
        File.WriteAllBytes(path, target.ToArray());
    }
}
