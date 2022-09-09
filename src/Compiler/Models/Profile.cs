using System.Collections.Generic;

namespace SourceSDK.Models
{
    public class Profile
    {
        public string Name { get; set; }
        public Dictionary<string, string[]> Builders { get; set; }
    }
}
