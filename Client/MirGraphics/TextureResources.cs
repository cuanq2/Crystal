using SlimDX.Direct3D9;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Client.MirGraphics
{
    internal static class TextureResources
    {
        internal static Texture CreateWritable(Device device, bool extended, int width, int height)
        {
            return new Texture(device, width, height, 1,
                extended ? Usage.Dynamic : Usage.None, Format.A8R8G8B8,
                extended ? Pool.Default : Pool.Managed);
        }

        internal static void Upload(Texture texture, byte[] pixels, int width, int height)
        {
            int rowBytes = checked(width * 4);
            if (pixels.Length != checked(rowBytes * height))
                throw new InvalidDataException("Texture pixel data does not match its dimensions.");

            var rectangle = texture.LockRectangle(0, LockFlags.Discard);
            try
            {
                using var stream = rectangle.Data;
                for (int y = 0; y < height; y++)
                {
                    stream.Position = (long)y * rectangle.Pitch;
                    stream.Write(pixels, y * rowBytes, rowBytes);
                }
            }
            finally
            {
                texture.UnlockRectangle(0);
            }
        }

        internal static void UploadBitmap(Texture texture, Bitmap bitmap)
        {
            int rowBytes = checked(bitmap.Width * 4);
            byte[] pixels = new byte[checked(rowBytes * bitmap.Height)];
            var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < bitmap.Height; y++)
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * rowBytes, rowBytes);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            Upload(texture, pixels, bitmap.Width, bitmap.Height);
        }
    }
}
