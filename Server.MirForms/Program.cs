using log4net;
using System.Reflection;

namespace Server.MirForms
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Packet.IsServer = true;

            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            log4net.Config.XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

            try
            {
                Settings.Load();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                long lastUiRefresh = 0;
                Application.Idle += (_, _) =>
                {
                    if (Environment.TickCount64 - lastUiRefresh < 250) return;
                    lastUiRefresh = Environment.TickCount64;
                    foreach (Form form in Application.OpenForms) ServerUi.Apply(form);
                };
                Application.Run(new SMain());

                Settings.Save();
            }
            catch(Exception ex)
            {
                Logger.GetLogger().Error(ex);
            }
        }
    }
}
