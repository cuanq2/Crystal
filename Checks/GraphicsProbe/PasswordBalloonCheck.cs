using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class PasswordBalloonCheck
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct EditBalloonTip
    {
        public int Size;
        [MarshalAs(UnmanagedType.LPWStr)] public string Title;
        [MarshalAs(UnmanagedType.LPWStr)] public string Text;
        public int Icon;
    }

    internal static void Run(TextBox box)
    {
        var tip = new EditBalloonTip
        {
            Size = Marshal.SizeOf<EditBalloonTip>(),
            Title = "Caps Lock",
            Text = "Password input regression check",
            Icon = 2
        };
        IntPtr pointer = Marshal.AllocHGlobal(tip.Size);
        try
        {
            Marshal.StructureToPtr(tip, pointer, false);
            // Invoke only this test process's control, without changing keyboard state
            // or sending input to the running game or any other application.
            var procedure = box.GetType().GetMethod("WndProc", BindingFlags.NonPublic | BindingFlags.Instance)!;
            for (int cycle = 0; cycle < 20; cycle++)
            {
                object[] arguments = { Message.Create(box.Handle, 0x1503, IntPtr.Zero, pointer) };
                Console.Error.WriteLine($"password balloon request {cycle}");
                procedure.Invoke(box, arguments);
                if (((Message)arguments[0]).Result != IntPtr.Zero)
                    throw new Exception("Native password balloon was allowed on the off-screen input control.");
            }
            if (!box.UseSystemPasswordChar) throw new Exception("Password masking was disabled.");
            Console.Error.WriteLine("password balloon suppression passed: 20 requests");
        }
        finally
        {
            Marshal.DestroyStructure<EditBalloonTip>(pointer);
            Marshal.FreeHGlobal(pointer);
        }
    }
}
