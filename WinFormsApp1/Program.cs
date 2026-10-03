using System;
using System.Windows.Forms;
using DeploymentHelper;

namespace DeploymentHelper
{
    internal static class Program
    {
        public static class AppConfig
        {
            public static bool UseDatabase = false;
        }
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();       
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
