using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SourceSDK.Interfaces;

namespace SourceSDK.Builders
{
    public class MapBuilder : IMapBuilder
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        public MapBuilder(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger(GetType());
            _configuration = configuration;
        }
        public Func<string> GamePath { get; set; }
        public Func<bool> Verbose { get; set; }
        public Func<bool> Outputs { get; set; }

        public string Folder => "maps";
        public string FileFormat => "*.vmf";
        public void Build(string file)
        {
            if (!File.Exists(Path.Combine(GamePath(), "gameinfo.txt"))) throw new FileNotFoundException("Unable to locate gameinfo.txt");

            vbsp(file);
            vvis(file);
            vrad(file);
            pack(file);
        }

        private void vbsp(string vmfFile)
        {
            var vbspPath = Path.GetFullPath(Path.Combine(GamePath(), "..", "bin", "vbsp.exe"));
            if (!File.Exists(vbspPath)) throw new FileNotFoundException("Unable to locate vbsp.exe");

            var args = new List<string>();
            _configuration.Bind("Builders:Map:VBSP:Args", args);
            args.Add($"-game \"{GamePath()}\"");
            if (Verbose()) args.Add("-verbose");

            args.Add($"\"{vmfFile}\"");
            var fileName = Path.GetFileName(vmfFile);
            launchProcess(vbspPath, args, fileName);
        }
        private void vvis(string vmfFile)
        {
            var vvisPath = Path.GetFullPath(Path.Combine(GamePath(), "..", "bin", "vvis.exe"));
            if (!File.Exists(vvisPath)) throw new FileNotFoundException("Unable to locate vvis.exe");

            var args = new List<string>();
            _configuration.Bind("Builders:Map:VVIS:Args", args);
            args.Add($"-game \"{GamePath()}\"");
            if (Verbose()) args.Add("-verbose");

            var bspFile = Path.ChangeExtension(vmfFile, "bsp");
            args.Add($"\"{bspFile}\"");
            var fileName = Path.GetFileName(bspFile);
            launchProcess(vvisPath, args, fileName);
        }
        private void vrad(string file)
        {
            var vradPath = Path.GetFullPath(Path.Combine(GamePath(), "..", "bin", "vrad.exe"));
            if (!File.Exists(vradPath)) throw new FileNotFoundException("Unable to locate vrad.exe");

            var args = new List<string>();
            _configuration.Bind("Builders:Map:VRAD:Args", args);
            args.Add($"-game \"{GamePath()}\"");
            if (Verbose()) args.Add("-verbose");

            var bspFile = Path.ChangeExtension(file, "bsp");
            args.Add($"\"{bspFile}\"");
            var fileName = Path.GetFileName(bspFile);
            launchProcess(vradPath, args, fileName);

        }

        private void pack(string file)
        {
            var bspzipPath = Path.GetFullPath(Path.Combine(GamePath(), "..", "bin", "bspzip.exe"));
            if (!File.Exists(bspzipPath)) throw new FileNotFoundException("Unable to locate bspzip.exe");

            var args = new List<string>();
            _configuration.Bind("Builders:Map:PACK:Args", args);
            args.Add($"-game \"{GamePath()}\"");
            //if (Verbose()) args.Add("-verbose");

            var bspFile = Path.ChangeExtension(file, "bsp");
            var lstFile = Path.ChangeExtension(file, "lst");

            args.Add($"-dir \"{bspFile}\"");
            var fileName = Path.GetFileName(lstFile);
            if (File.Exists(fileName)) File.Delete(fileName);
            launchProcess(bspzipPath, args, fileName, lstFile);

            if (!File.Exists(lstFile)) throw new FileNotFoundException(lstFile);

            var lstFileContent = File.ReadAllText(lstFile);
            if (!string.IsNullOrWhiteSpace(lstFileContent)) { }

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
        ~MapBuilder()
        {
            Dispose(false);
        }


        #endregion

    }
}
