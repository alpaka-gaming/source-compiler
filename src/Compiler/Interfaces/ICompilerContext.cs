using System;
using System.ComponentModel;
using SourceSDK.Models;

namespace SourceSDK.Interfaces
{
    public interface ICompilerContext : IDisposable
    {
        Option Options { get; set; }

        void Compile(string source);
        
        event ProgressChangedEventHandler Progress;
    }
}
