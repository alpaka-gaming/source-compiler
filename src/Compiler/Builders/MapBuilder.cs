using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SourceSDK.Interfaces;
using SourceSDK.Models;

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

        private Profile _profile;
        public void Build(string file, Profile profile)
        {
            _profile = profile;

            if (!File.Exists(Path.Combine(GamePath(), "gameinfo.txt"))) throw new FileNotFoundException("Unable to locate gameinfo.txt");

            vbsp(file);
            vvis(file);
            vrad(file);
            pack(file);
            cubemaps(file);
        }

        private void vbsp(string vmfFile)
        {
            if (!_profile.Builders.ContainsKey("vbsp")) return;

            var vbspPath = Path.GetFullPath(Path.Combine(GamePath(), "..", "bin", "vbsp.exe"));
            if (!File.Exists(vbspPath)) throw new FileNotFoundException("Unable to locate vbsp.exe");

            var arguments = new List<string>(_profile.Builders["vbsp"]);

            arguments.Add($"-game \"{GamePath()}\"");
            if (Verbose()) arguments.Add("-verbose");

            arguments.Add($"\"{vmfFile}\"");
            var fileName = Path.GetFileName(vmfFile);
            launchProcess(vbspPath, arguments, fileName);
        }
        private void vvis(string vmfFile)
        {
            if (!_profile.Builders.ContainsKey("vvis")) return;

            var vvisPath = Path.GetFullPath(Path.Combine(GamePath(), "..", "bin", "vvis.exe"));
            if (!File.Exists(vvisPath)) throw new FileNotFoundException("Unable to locate vvis.exe");

            var arguments = new List<string>(_profile.Builders["vvis"]);

            arguments.Add($"-game \"{GamePath()}\"");
            if (Verbose()) arguments.Add("-verbose");

            var bspFile = Path.ChangeExtension(vmfFile, "bsp");
            arguments.Add($"\"{bspFile}\"");
            var fileName = Path.GetFileName(bspFile);
            launchProcess(vvisPath, arguments, fileName);
        }
        private void vrad(string file)
        {
            if (!_profile.Builders.ContainsKey("vrad")) return;

            var vradPath = Path.GetFullPath(Path.Combine(GamePath(), "..", "bin", "vrad.exe"));
            if (!File.Exists(vradPath)) throw new FileNotFoundException("Unable to locate vrad.exe");

            var arguments = new List<string>(_profile.Builders["vrad"]);

            arguments.Add($"-game \"{GamePath()}\"");
            if (Verbose()) arguments.Add("-verbose");

            var bspFile = Path.ChangeExtension(file, "bsp");
            arguments.Add($"\"{bspFile}\"");
            var fileName = Path.GetFileName(bspFile);
            launchProcess(vradPath, arguments, fileName);

        }

        private void cubemaps(string file)
        {
            if (!_profile.Builders.ContainsKey("cubemaps")) return;

            var hlPath = Path.GetFullPath(Path.Combine(GamePath(), "..", "hl2.exe"));
            if (!File.Exists(hlPath)) throw new FileNotFoundException("Unable to locate hl2.exe");

            if (!Directory.Exists(Path.Combine(GamePath(), "bin")))
            {
                Directory.CreateDirectory(Path.Combine(GamePath(), "bin"));
                File.Copy(Path.Combine(GamePath(), "..", "sourcetest", "bin", "client.dll"), Path.Combine(GamePath(), "bin", "client.dll"));
                File.Copy(Path.Combine(GamePath(), "..", "sourcetest", "bin", "server.dll"), Path.Combine(GamePath(), "bin", "server.dll"));
            }

            var arguments = new List<string>(_profile.Builders["cubemaps"]);
            if (arguments.Contains("-hidden"))
            {
                arguments.Remove("-hidden");
                arguments.AddRange(new[]
                {
                    "-noborder", "-x 4000", "-y 2000"
                });
            }

            arguments.Add("-steam");
            arguments.Add($"-game \"{Path.GetFileName(GamePath())}\"");
            arguments.Add("-novid");
            arguments.Add("-nosound");
            arguments.Add("+mat_specular 0");
            arguments.Add("-buildcubemaps");

            var bspFile = Path.ChangeExtension(file, "bsp");
            var fileName = Path.GetFileName(bspFile);

            if (!Directory.Exists(Path.Combine(GamePath(), "maps"))) Directory.CreateDirectory(Path.Combine(GamePath(), "maps"));
            File.Copy(bspFile, Path.Combine(GamePath(), "maps", fileName), true);

            arguments.Add($"+map {Path.GetFileNameWithoutExtension(fileName)}");
            var both = arguments.Contains("-both");
            if (both) arguments.Remove("-both");

            //TODO: Fetch HDR Levels

            if ((arguments.Contains("-ldr") && !arguments.Contains("-hdr")) || both)
            {
                arguments.Remove("-ldr");
                arguments.Remove("+mat_hdr_level 2");
                arguments.Add("+mat_hdr_level 0");
                launchProcess(hlPath, arguments, fileName);
            }

            if ((!arguments.Contains("-ldr") && arguments.Contains("-hdr")) || both)
            {
                arguments.Remove("-hdr");
                arguments.Remove("+mat_hdr_level 0");
                arguments.Add("+mat_hdr_level 2");
                launchProcess(hlPath, arguments, fileName);
            }

        }
        private void pack(string file)
        {
            if (!_profile.Builders.ContainsKey("pack")) return;

            var bspzipPath = Path.GetFullPath(Path.Combine(GamePath(), "..", "bin", "bspzip.exe"));
            if (!File.Exists(bspzipPath)) throw new FileNotFoundException("Unable to locate bspzip.exe");

            var arguments = new List<string>(_profile.Builders["pack"]);

            arguments.Add($"-game \"{GamePath()}\"");
            //if (Verbose()) args.Add("-verbose");

            var fileName = Path.GetFileName(file);
            var bspFile = Path.ChangeExtension(Path.Combine(GamePath(), "maps", fileName), "bsp");
            var bspOutFile = Path.ChangeExtension(Path.Combine(GamePath(), "maps", fileName), "new");
            var lstFile = Path.ChangeExtension(bspFile, "lst");
            
            if (!File.Exists(bspFile))
                File.Copy(Path.ChangeExtension(file, "bsp"), bspFile, true);

            arguments.Add($"-dir \"{bspFile}\"");
            if (File.Exists(lstFile)) File.Delete(lstFile);
            launchProcess(bspzipPath, arguments, fileName, lstFile);

            if (!File.Exists(lstFile)) throw new FileNotFoundException(lstFile);

            var lstFileContent = File.ReadAllLines(lstFile);
            var validFiles = new List<string>();
            foreach (var line in lstFileContent)
            {
                var filePath = Path.Combine(Path.Combine(GamePath(), line));
                if (File.Exists(filePath))
                    validFiles.Add(filePath);
            }

            if (validFiles.Any())
            {
                if (File.Exists(Path.ChangeExtension(lstFile, "tmp"))) File.Delete(Path.ChangeExtension(lstFile, "tmp"));
                File.WriteAllLines(Path.ChangeExtension(lstFile, "tmp"), validFiles);

                var arguments2 = new List<string>();
                arguments2.Add($"-game \"{GamePath()}\"");
                arguments2.Add($"-addlist \"{bspFile}\" \"{lstFile}\" \"{bspOutFile}\"");

                launchProcess(bspzipPath, arguments2, fileName, lstFile);

                if (File.Exists(bspOutFile))
                    File.Copy(bspOutFile, Path.ChangeExtension(file, "bsp"), true);
            }
            else
            {
                if (File.Exists(bspFile))
                    File.Copy(bspFile, Path.ChangeExtension(file, "bsp"), true);
            }
        }

        private void launchProcess(string executable, IEnumerable<string> arguments, string fileName, string targetFile = "")
        {
            var process = new Process();
            process.StartInfo = new ProcessStartInfo()
            {
                FileName = executable,
                Arguments = string.Join(" ", arguments.Distinct()),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            _logger.LogDebug("[{Builder}] [Executing] {FileName} {Arguments}", GetType().Name, process.StartInfo.FileName, process.StartInfo.Arguments);

            process.Start();

            if (Outputs() || !string.IsNullOrWhiteSpace(targetFile))
                while (!process.StandardOutput.EndOfStream)
                {
                    var line = process.StandardOutput.ReadLine();
                    if (!string.IsNullOrWhiteSpace(targetFile))
                        File.AppendAllLines(targetFile, new[]
                        {
                            line
                        });
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
