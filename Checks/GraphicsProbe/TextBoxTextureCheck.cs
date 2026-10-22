using System.Drawing;
using System.Reflection;
using Client;
using Client.MirControls;
using SlimDX.Direct3D9;

internal static class TextBoxTextureCheck
{
    internal static void Run(Device device)
    {
        var assembly = typeof(MirTextBox).Assembly;
        var manager = assembly.GetType("Client.MirGraphics.DXManager")!;
        using var sprite = new Sprite(device);
        manager.GetField("Sprite")!.SetValue(null, sprite);
        CMain.Cursors = Enumerable.Repeat(System.Windows.Forms.Cursors.IBeam, 8).ToArray();
        using var form = new ProbeForm();
        using var metrics = new Bitmap(1, 1);
        CMain.Graphics.Dispose();
        using var graphics = Graphics.FromImage(metrics);
        CMain.Graphics = graphics;
        assembly.GetType("Client.Program")!.GetField("Form")!.SetValue(null, form);
        var render = typeof(MirTextBox).GetMethod("CreateTexture", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var textureField = typeof(MirControl).GetField("ControlTexture", BindingFlags.NonPublic | BindingFlags.Instance)!;
        int renders = 0;
        foreach (bool password in new[] { false, true })
        {
            using var box = new MirTextBox { Size = new Size(136, 15), Password = password };
            box.TextBox.Parent = form;
            form.Show();
            box.TextBox.Focus();
            Console.Error.WriteLine($"textbox visible={box.TextBox.Visible} focused={box.TextBox.Focused}");
            if (password) PasswordBalloonCheck.Run(box.TextBox);
            for (int step = 0; step <= 40; step++)
            {
                int length = step <= 20 ? step : 40 - step;
                box.Text = new string('a', length);
                box.TextBox.SelectionStart = length;
                Render();
            }
            box.Text = "aaaa";
            box.TextBox.SelectAll();
            box.TextBox.SelectedText = "bbbb";
            if (box.Text != "bbbb") throw new Exception("Selection replacement failed.");
            box.TextBox.Select(4, 0);
            int[] first = Render();
            box.Text = "WWWW";
            box.TextBox.Select(4, 0);
            int[] second = Render();
            if (first.SequenceEqual(second) != password)
                throw new Exception("Password masking or plain-text rendering failed.");
            box.Text = "";
            int[] empty = Render();
            if (empty.SequenceEqual(second)) throw new Exception("Non-empty text was not drawn.");
            for (int cycle = 0; cycle < 3; cycle++)
            {
                form.Hide();
                form.Show();
                box.TextBox.Focus();
                box.Text = "caret";
                box.TextBox.Select(cycle, 0);
                Render();
            }

            int[] Render()
            {
                device.Clear(ClearFlags.Target, Color.Blue, 1, 0);
                device.BeginScene();
                sprite.Begin(SpriteFlags.AlphaBlend);
                render.Invoke(box, null);
                sprite.Draw((Texture)textureField.GetValue(box)!, Color.White);
                sprite.End();
                device.EndScene();
                using var surface = device.GetBackBuffer(0, 0);
                var data = surface.LockRectangle(LockFlags.ReadOnly);
                var pixels = new int[64 * 15];
                try
                {
                    for (int y = 0; y < 15; y++)
                    {
                        data.Data.Position = (long)y * data.Pitch;
                        for (int x = 0; x < 64; x++) pixels[y * 64 + x] = data.Data.Read<int>();
                    }
                }
                finally { data.Data.Dispose(); surface.UnlockRectangle(); }
                device.Present();
                renders++;
                return pixels;
            }
        }
        Console.Error.WriteLine($"textbox checks passed: {renders} renders, masking, replacement, deletion, caret and visibility");
    }

    private sealed class ProbeForm : CMain
    {
        public ProbeForm()
        {
            ShowInTaskbar = false;
            Opacity = 0;
            var idle = typeof(CMain).GetMethod("Application_Idle", BindingFlags.NonPublic | BindingFlags.Static)!;
            System.Windows.Forms.Application.Idle -= (EventHandler)idle.CreateDelegate(typeof(EventHandler));
        }
        protected override void OnLoad(EventArgs e) { }
        protected override void OnFormClosing(System.Windows.Forms.FormClosingEventArgs e) { }
    }
}
