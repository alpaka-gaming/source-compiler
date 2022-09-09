using CommandLine;

namespace SourceSDK.Models
{
    public class Option
    {
        
        [Option('p', "profile", Required = false, Default = "normal", HelpText = "Set the build profile.")]
        public string Profile { get; set; }
        
        [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
        public bool Verbose { get; set; }
        
        [Option('o', "outputs", Required = false, HelpText = "Print the internal builders output.")]
        public bool Outputs { get; set; }

        [Option('g', "game", Required = true, HelpText = "Set the game path.")]
        public string Game { get; set; }
        
        [Option('s', "source", Required = true, HelpText = "Set the source path.")]
        public string Source { get; set; }

    }
}
