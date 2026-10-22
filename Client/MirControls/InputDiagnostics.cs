namespace Client.MirControls
{
    // Opt-in local troubleshooting; never record the text entered into a control.
    internal static class InputDiagnostics
    {
        private static readonly bool Enabled =
            Environment.GetEnvironmentVariable("CRYSTAL_INPUT_DIAGNOSTICS") == "1";

        internal static void Record(string stage)
        {
            if (!Enabled) return;
            try
            {
                File.AppendAllText("InputDiagnostics.log", $"{DateTime.Now:O} {stage}{Environment.NewLine}");
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
