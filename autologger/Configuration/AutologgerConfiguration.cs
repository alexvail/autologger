using autologger.Options;

namespace autologger.Configuration
{
    public class AutologgerConfiguration
    {
        public bool LaunchMstscOnStart { get; set; }

        public Credentials Credentials { get; set; }

        public KeyCombinations KeyCombinations { get; set; }
    }
}