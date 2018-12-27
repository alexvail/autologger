using System;
using System.IO;

using Microsoft.Extensions.Configuration;

namespace autologger
{
    internal class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");

            var config = builder.Build();

            var autologger = new Autologger(config);
            autologger.Main();
        }
    }
}