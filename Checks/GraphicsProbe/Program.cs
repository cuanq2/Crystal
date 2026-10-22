using SlimDX.Direct3D9;
using System.Text.Json;
using Client.MirGraphics;
using SlimDX;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        using var deadline = new System.Threading.Timer(_ =>
        {
            Console.Error.WriteLine("Graphics/input probe exceeded 30 seconds.");
            Environment.Exit(124);
        }, null, 30000, Timeout.Infinite);
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        LocalizationCheck.Run();
        var adapters = new List<object>();
        bool extendedRenderingPassed = false;
        foreach (bool extended in new[] { false, true })
        {
            try
            {
                using Direct3D d3d = extended ? new Direct3DEx() : new Direct3D();
                foreach (AdapterInformation information in d3d.Adapters)
                {
                    var adapter = information.Details;
                    string stage = "caps";
                    try
                    {
                        var caps = d3d.GetDeviceCaps(adapter.Adapter, DeviceType.Hardware);
                        if (extended)
                        {
                            using var window = new System.Windows.Forms.Form();
                            var parameters = new PresentParameters
                            {
                                Windowed = true,
                                BackBufferWidth = 64,
                                BackBufferHeight = 64,
                                BackBufferFormat = Format.X8R8G8B8,
                                SwapEffect = SwapEffect.Discard,
                                PresentFlags = PresentFlags.LockableBackBuffer
                            };
                            stage = "device";
                            using var device = new Device(d3d, adapter.Adapter, DeviceType.Hardware,
                                window.Handle, CreateFlags.HardwareVertexProcessing, parameters);
                            stage = "client image alpha and mask";
                            AssetTextureCheck.Run(device);
                            stage = "client textbox";
                            TextBoxTextureCheck.Run(device);
                            stage = "dynamic texture";
                            using var texture = TextureResources.CreateWritable(device, true, 5, 3);
                            byte[] pattern = new byte[5 * 3 * 4];
                            for (int y = 0; y < 3; y++)
                                for (int x = 0; x < 5; x++)
                                {
                                    pattern[(y * 5 + x) * 4 + y] = 255;
                                    pattern[(y * 5 + x) * 4 + 3] = 255;
                                }
                            TextureResources.Upload(texture, pattern, 5, 3);
                            stage = "sprite";
                            using var sprite = new Sprite(device);
                            device.Clear(ClearFlags.Target, System.Drawing.Color.CornflowerBlue, 1, 0);
                            device.BeginScene();
                            sprite.Begin(SpriteFlags.AlphaBlend);
                            sprite.Draw(texture, System.Drawing.Color.White);
                            sprite.End();
                            device.EndScene();
                            using var surface = device.GetBackBuffer(0, 0);
                            var pixels = surface.LockRectangle(LockFlags.ReadOnly);
                            int pixel = pixels.Data.Read<int>();
                            pixels.Data.Position = pixels.Pitch;
                            int secondRow = pixels.Data.Read<int>();
                            pixels.Data.Position = pixels.Pitch * 2;
                            int thirdRow = pixels.Data.Read<int>();
                            pixels.Data.Dispose();
                            surface.UnlockRectangle();
                            if ((pixel & 0xffffff) != 0x0000ff || (secondRow & 0xffffff) != 0x00ff00 ||
                                (thirdRow & 0xffffff) != 0xff0000)
                                throw new Exception($"Backbuffer texture row mismatch: {pixel:X8} {secondRow:X8} {thirdRow:X8}");
                            device.Present();
                            surface.Dispose();
                            sprite.Dispose();
                            texture.Dispose();
                            parameters.BackBufferWidth = 96;
                            device.Reset(parameters);
                            using var recreated = TextureResources.CreateWritable(device, true, 5, 3);
                            TextureResources.Upload(recreated, pattern, 5, 3);
                            device.Clear(ClearFlags.Target, System.Drawing.Color.Blue, 1, 0);
                            device.Present();
                            adapters.Add(new
                            {
                                extended,
                                legacyDevice = true,
                                dynamicTexture = true,
                                textureRowReadbackPassed = true,
                                presentPassed = true,
                                resetPassed = true,
                                clientAlphaAndMaskPassed = true,
                                clientTextBoxPassed = true,
                                passwordBalloonRequestsSuppressed = 20
                            });
                            extendedRenderingPassed = true;
                        }
                        adapters.Add(new
                        {
                            extended,
                            adapter.Adapter,
                            adapter.Description,
                            available = true,
                            vertexShader = caps.VertexShaderVersion.ToString(),
                            pixelShader = caps.PixelShaderVersion.ToString()
                        });
                    }
                    catch (Exception error)
                    {
                        adapters.Add(new { extended, adapter.Adapter, adapter.Description, available = false, stage, error = error.ToString() });
                    }
                }
            }
            catch (Exception error) { adapters.Add(new { extended, available = false, error = error.ToString() }); }
        }
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            sessionId = System.Diagnostics.Process.GetCurrentProcess().SessionId,
            remoteSession = System.Windows.Forms.SystemInformation.TerminalServerSession,
            extendedRenderingPassed,
            adapters
        }));
        Environment.ExitCode = extendedRenderingPassed ? 0 : 1;
    }
}
