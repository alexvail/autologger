namespace autologger.Configuration
{
    public class AutologgerConfiguration
    {
        public bool LaunchMstscOnStart { get; set; } = true;

        public string QrCodeImageFilePath { get; set; } = string.Empty;

        public Credentials Credentials { get; set; } = new Credentials();

        public Otp Otp { get; set; } = new Otp();

        public KeyCombinations KeyCombinations { get; set; } = new KeyCombinations();
    }
}