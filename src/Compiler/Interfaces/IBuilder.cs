using System;
using SourceSDK.Models;

namespace SourceSDK.Interfaces
{
    public interface IBuilder : IDisposable
    {
        Func<string> GamePath { get; set; }
        Func<bool> Verbose { get; set; }
        Func<bool> Outputs { get; set; }
        string FileFormat { get; }
        string Folder { get; }
        void Build(string file, Profile profile);
    }
}
