namespace Client.MirControls
{
    internal sealed class MirNativeTextBox : TextBox
    {
        private const int EmShowBalloonTip = 0x1503;

        protected override void WndProc(ref Message message)
        {
            // This native control lives off-screen; the game draws its own texture.
            // Its Caps Lock balloon can hang under RDP. Suppress only password
            // balloons, leaving native password masking and keyboard input intact.
            // https://devblogs.microsoft.com/oldnewthing/20081010-00/?p=20603
            if (message.Msg == EmShowBalloonTip && (UseSystemPasswordChar || PasswordChar != '\0'))
            {
                InputDiagnostics.Record("input: password balloon suppressed");
                message.Result = IntPtr.Zero;
                return;
            }

            base.WndProc(ref message);
        }
    }
}
