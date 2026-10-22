using System.Drawing;
using System.IO.Compression;
using System.Reflection;
using Client.MirGraphics;
using SlimDX.Direct3D9;

internal static class AssetTextureCheck
{
    internal static void Run(Device device)
    {
        // Exercise the compiled client's image upload and hit testing in this test process.
        var manager = typeof(MImage).Assembly.GetType("Client.MirGraphics.DXManager")!;
        manager.GetField("Device", BindingFlags.Public | BindingFlags.Static)!.SetValue(null, device);
        manager.GetProperty("ExtendedDevice", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, true);

        byte[] pixels = Enumerable.Repeat((byte)255, 5 * 3 * 4).ToArray();
        pixels[3] = 0;
        byte[] mask = Enumerable.Repeat((byte)127, 2 * 2 * 4).ToArray();
        byte[] compressed = Compress(pixels), compressedMask = Compress(mask);
        using var data = new MemoryStream();
        using (var writer = new BinaryWriter(data, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((short)5); writer.Write((short)3);
            for (int i = 0; i < 4; i++) writer.Write((short)0);
            writer.Write((byte)128); writer.Write(compressed.Length); writer.Write(compressed);
            writer.Write((short)2); writer.Write((short)2);
            writer.Write((short)0); writer.Write((short)0);
            writer.Write(compressedMask.Length); writer.Write(compressedMask);
        }
        data.Position = 0;
        using var reader = new BinaryReader(data);
        var image = new MImage(reader);
        try
        {
            data.Position = 17;
            image.CreateTexture(reader);
            if (image.VisiblePixel(Point.Empty) || !image.VisiblePixel(new Point(1, 0)) ||
                image.VisiblePixel(new Point(5, 0)))
                throw new Exception("Alpha hit testing failed after texture unlock.");
            var description = image.MaskImage.GetLevelDescription(0);
            if (description.Width != 2 || description.Height != 2 || data.Position != data.Length)
                throw new Exception("Mask dimensions or payload boundary mismatch.");
        }
        finally { image.DisposeTexture(); }
        if (image.VisiblePixel(new Point(1, 0)))
            throw new Exception("Disposed image retained hit-test pixels.");
    }

    private static byte[] Compress(byte[] data)
    {
        using var target = new MemoryStream();
        using (var gzip = new GZipStream(target, CompressionMode.Compress, leaveOpen: true))
            gzip.Write(data);
        return target.ToArray();
    }
}
