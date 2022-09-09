using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SourceSDK.Interfaces;
using SourceSDK.Models;

namespace SourceSDK.Builders
{
    public class ModelBuilder : IModelBuilder
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        public ModelBuilder(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger(GetType());
            _configuration = configuration;
        }

        public string Folder => "models";
        public string FileFormat => "*.qc";

        private Profile _profile;
        public void Build(string file, Profile profile)
        {
            _profile = profile;
            if (!_profile.Builders.ContainsKey("studiomdl")) return;
            if (!File.Exists(Path.Combine(GamePath(), "gameinfo.txt"))) throw new FileNotFoundException("Unable to locate gameinfo.txt");

            var studiomdlPath = Path.GetFullPath(Path.Combine(GamePath(), "..", "bin", "studiomdl.exe"));
            if (!File.Exists(studiomdlPath)) throw new FileNotFoundException("Unable to locate studiomdl.exe");

            var args = _profile.Builders["studiomdl"];
            var arguments = new List<string>(args);
            arguments.Add($"-game \"{GamePath()}\"");
            if (Verbose()) arguments.Add("-verbose");
            arguments.Add($"\"{file}\"");
            var fileName = Path.GetFileName(file);
            launchProcess(studiomdlPath, arguments, fileName);
        }
        private void launchProcess(string executable, IEnumerable<string> arguments, string fileName, string targetFile = "")
        {
            var process = new Process();
            process.StartInfo = new ProcessStartInfo()
            {
                FileName = executable, Arguments = string.Join(" ", arguments), UseShellExecute = false, RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            
            _logger.LogDebug("[{Builder}] [Executing] {FileName} {Arguments}", GetType().Name, process.StartInfo.FileName, process.StartInfo.Arguments);
            
            process.Start();

            if (Outputs() || !string.IsNullOrWhiteSpace(targetFile))
                while (!process.StandardOutput.EndOfStream)
                {
                    var line = process.StandardOutput.ReadLine();
                    if (!string.IsNullOrWhiteSpace(targetFile)) File.AppendAllLines(targetFile, new[] { line });
                    if (Outputs()) _logger.LogInformation("[{Builder}] [{File}] {Line}", GetType().Name, fileName, line);
                }
            process.WaitForExit();
        }

        public Func<string> GamePath { get; set; }
        public Func<bool> Verbose { get; set; }
        public Func<bool> Outputs { get; set; }

        #region IDisposable


        private void ReleaseUnmanagedResources()
        {
            // TODO release unmanaged resources here
        }
        protected virtual void Dispose(bool disposing)
        {
            ReleaseUnmanagedResources();
            if (disposing) { }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        ~ModelBuilder()
        {
            Dispose(false);
        }


        #endregion

    }
}
