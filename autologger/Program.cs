using System;
using System.Diagnostics;
using System.DrawingCore;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web;

using autologger.Configuration;

using Microsoft.Extensions.Configuration;

using ZXing.ZKWeb;

namespace autologger
{
    internal class Program
    {
        private const string ConfigFileName = "autologger.json";

        [STAThread]
        public static void Main()
        {
            var configuration = BuildConfiguration();

            if (!string.IsNullOrEmpty(configuration.QrCodeImageFilePath))
            {
                if (!Path.IsPathRooted(configuration.QrCodeImageFilePath))
                {
                    // Try to find file in executing directory
                    configuration.QrCodeImageFilePath = Path.Combine(GetCurrentDirectory(), configuration.QrCodeImageFilePath);
                }

                if (!File.Exists(configuration.QrCodeImageFilePath))
                {
                    throw new FileNotFoundException("QR image file not found.", configuration.QrCodeImageFilePath);
                }

                configuration.Otp = GetOtpConfig(DecodeQrCodeImage(configuration.QrCodeImageFilePath));
            }

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
                .SetBasePath(GetCurrentDirectory())
                .AddJsonFile(ConfigFileName);

            return configurationBuilder.Build().Get<AutologgerConfiguration>();
        }

        private static string GetCurrentDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        private static Otp GetOtpConfig(string otpAuthUri)
        {
            var match = Regex.Match(otpAuthUri, @"otpauth://([^/]+)/([^?]+)\?(.*)", RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                throw new Exception("Could not parse OtpAuthUri string.");
            }

            var parsedQueryString = HttpUtility.ParseQueryString(match.Groups[3].Value);

            return new Otp
            {
                Base32Secret = parsedQueryString["secret"],
                Size = int.Parse(parsedQueryString["digits"]),
                Step = int.Parse(parsedQueryString["period"])
            };
        }

        private static string DecodeQrCodeImage(string filePath)
        {
            var reader = new ZXing.BarcodeReader();
            var barcodeBitmap = (Bitmap)Image.FromFile(filePath);
            var bitmapLuminanceSource = new BitmapLuminanceSource(barcodeBitmap);

            var result = reader.Decode(bitmapLuminanceSource) ?? throw new Exception("Failed to decode QR code.");

            return result.Text;
        }

        private static void LaunchMstsc()
        {
            Process.Start("mstsc.exe");
        }
    }
}