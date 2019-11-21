namespace autologger.Configuration
{
    public class Otp
    {
        public string Base32Secret { get; set; } = string.Empty;

        public int Step { get; set; } = 30;

        public int Size { get; set; } = 6;
    }
}