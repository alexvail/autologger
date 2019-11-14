using System;
using System.Diagnostics;
using System.IO;

using autologger.Configuration;
using autologger.Options;

using Microsoft.Extensions.Configuration;

namespace autologger
{
    internal class Program
    {
        private const string ConfigFileName = "autologger.json";

        [STAThread]
        public static void Main()
        {
            var configuration = BuildConfiguration();

            if (configuration.LaunchMstscOnStart)
            {
                LaunchMstsc();
            }

            var autologger = new Autologger(configuration);
            autologger.Run();
        }

        private static AutologgerConfiguration BuildConfiguration()
        {
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(ConfigFileName);

            return configurationBuilder.Build().Get<AutologgerConfiguration>();
        }

        private static void LaunchMstsc()
        {
            Process.Start("mstsc.exe");
        }
    }
}