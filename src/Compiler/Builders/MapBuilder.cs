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
using ValveKeyValue;

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
            unpack(file);
            pack(file);
            cubemaps(file);
        }

        private void vbsp(string file)
        {
            if (!_profile.Builders.ContainsKey("vbsp")) return;

            var gameFolder = GamePath();
            var vbspPath = Path.GetFullPath(Path.Combine(gameFolder, "..", "bin", "vbsp.exe"));
            if (!File.Exists(vbspPath)) throw new FileNotFoundException("Unable to locate vbsp.exe");

            var arguments = new List<string>(_profile.Builders["vbsp"]);

            arguments.Add($"-game \"{gameFolder}\"");
            if (Verbose()) arguments.Add("-verbose");

            arguments.Add($"\"{file}\"");
            
            //launchProcess(vbspPath, arguments, fileName);
            Program.Launch(vbspPath, file, arguments: arguments.ToArray());
        }
        private void vvis(string file)
        {
            if (!_profile.Builders.ContainsKey("vvis")) return;

            var gameFolder = GamePath();
            var vvisPath = Path.GetFullPath(Path.Combine(gameFolder, "..", "bin", "vvis.exe"));
            if (!File.Exists(vvisPath)) throw new FileNotFoundException("Unable to locate vvis.exe");

            var arguments = new List<string>(_profile.Builders["vvis"]);

            arguments.Add($"-game \"{gameFolder}\"");
            if (Verbose()) arguments.Add("-verbose");

            var bspFile = Path.ChangeExtension(file, "bsp");
            arguments.Add($"\"{bspFile}\"");
            
            //launchProcess(vvisPath, arguments, fileName);
            Program.Launch(vvisPath, bspFile, arguments: arguments.ToArray());
        }
        private void vrad(string file)
        {
            if (!_profile.Builders.ContainsKey("vrad")) return;

            var gameFolder = GamePath();
            var vradPath = Path.GetFullPath(Path.Combine(gameFolder, "..", "bin", "vrad.exe"));
            if (!File.Exists(vradPath)) throw new FileNotFoundException("Unable to locate vrad.exe");

            var arguments = new List<string>(_profile.Builders["vrad"]);

            arguments.Add($"-game \"{GamePath()}\"");
            if (Verbose()) arguments.Add("-verbose");

            var bspFile = Path.ChangeExtension(file, "bsp");
            arguments.Add($"\"{bspFile}\"");

            //launchProcess(vradPath, arguments, fileName);
            Program.Launch(vradPath, bspFile, arguments: arguments.ToArray());

        }

        private void cubemaps(string file)
        {
            if (!_profile.Builders.ContainsKey("cubemaps")) return;

            var gameFolder = GamePath();
            var hlPath = Path.GetFullPath(Path.Combine(gameFolder, "..", "hl2.exe"));
            if (!File.Exists(hlPath)) throw new FileNotFoundException("Unable to locate hl2.exe");

            if (!Directory.Exists(Path.Combine(GamePath(), "bin")))
            {
                Directory.CreateDirectory(Path.Combine(gameFolder, "bin"));
                File.Copy(Path.Combine(gameFolder, "..", "sourcetest", "bin", "client.dll"), Path.Combine(gameFolder, "bin", "client.dll"));
                File.Copy(Path.Combine(gameFolder, "..", "sourcetest", "bin", "server.dll"), Path.Combine(gameFolder, "bin", "server.dll"));
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
            arguments.Add($"-game \"{Path.GetFileName(gameFolder)}\"");
            arguments.Add("-novid");
            arguments.Add("-nosound");
            arguments.Add("+mat_specular 0");
            arguments.Add("-buildcubemaps");

            var bspFile = Path.ChangeExtension(file, "bsp");
            var fileName = Path.GetFileName(bspFile);

            if (!Directory.Exists(Path.Combine(gameFolder, "maps"))) Directory.CreateDirectory(Path.Combine(gameFolder, "maps"));
            File.Move(bspFile, Path.Combine(gameFolder, "maps", fileName), true);

            arguments.Add($"+map {Path.GetFileNameWithoutExtension(fileName)}");
            var both = arguments.Contains("-both");
            if (both) arguments.Remove("-both");

            //TODO: Fetch HDR Levels

            if ((arguments.Contains("-ldr") && !arguments.Contains("-hdr")) || both)
            {
                arguments.Remove("-ldr");
                arguments.Remove("+mat_hdr_level 2");
                arguments.Add("+mat_hdr_level 0");
                //launchProcess(hlPath, arguments, fileName);
                Program.Launch(hlPath, bspFile, arguments: arguments.ToArray());
            }

            if ((!arguments.Contains("-ldr") && arguments.Contains("-hdr")) || both)
            {
                arguments.Remove("-hdr");
                arguments.Remove("+mat_hdr_level 0");
                arguments.Add("+mat_hdr_level 2");
                
                //launchProcess(hlPath, arguments, fileName);
                Program.Launch(hlPath, bspFile, arguments: arguments.ToArray());
            }
            
            File.Move(Path.Combine(gameFolder, "maps", fileName), bspFile, true);

        }
        private void pack(string file, bool dryrun = false)
        {
            if (!_profile.Builders.ContainsKey("pack")) return;

            var gameFolder = GamePath();
            var bspzipPath = Path.GetFullPath(Path.Combine(gameFolder, "..", "bin", "bspzip.exe"));
            if (!File.Exists(bspzipPath)) throw new FileNotFoundException("Unable to locate bspzip.exe");

            var arguments = new List<string>(_profile.Builders["pack"]);

            arguments.Add($"-game \"{gameFolder}\"");

            var bspFile = Path.ChangeExtension(file, "bsp");

            var bspFileInfo = new BspFile(bspFile, gameFolder);
            bspFileInfo.Read();
            

            // KVObject data = null;
            // try
            // {
            //     using (var stream = File.OpenRead(file))
            //     {
            //         var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
            //         var options = new KVSerializerOptions
            //         {
            //             HasEscapeSequences = true
            //         };
            //         data = kv.Deserialize(stream, options);
            //     }
            // }
            // catch (Exception)
            // {
            //     // ignored
            // }

            if (!File.Exists(bspFile))
                File.Copy(Path.ChangeExtension(file, "bsp"), bspFile, true);

            arguments.Add($"-dir \"{bspFile}\"");
            // if (File.Exists(lstFile)) File.Delete(lstFile);
            // launchProcess(bspzipPath, arguments, fileName, lstFile);
            //
            // if (!File.Exists(lstFile)) throw new FileNotFoundException(lstFile);
            //
            // var lstFileContent = File.ReadAllLines(lstFile);
            var validFiles = new List<string>();
            // foreach (var line in lstFileContent)
            // {
            //     var filePath = Path.Combine(Path.Combine(GamePath(), line));
            //     if (File.Exists(filePath))
            //         validFiles.Add(filePath);
            // }

            if (validFiles.Any())
            {
                // if (File.Exists(Path.ChangeExtension(lstFile, "tmp"))) File.Delete(Path.ChangeExtension(lstFile, "tmp"));
                // File.WriteAllLines(Path.ChangeExtension(lstFile, "tmp"), validFiles);
                //
                // var arguments2 = new List<string>();
                // arguments2.Add($"-game \"{GamePath()}\"");
                // arguments2.Add($"-addlist \"{bspFile}\" \"{lstFile}\" \"{bspOutFile}\"");
                //
                // launchProcess(bspzipPath, arguments2, fileName, lstFile);

                // if (File.Exists(bspOutFile))
                //     File.Copy(bspOutFile, Path.ChangeExtension(file, "bsp"), true);
            }
            else
            {
                if (File.Exists(bspFile))
                    File.Copy(bspFile, Path.ChangeExtension(file, "bsp"), true);
            }
        }

        private void unpack(string file, string target = "")
        {
            if (!_profile.Builders.ContainsKey("pack")) return;

            var gameFolder = GamePath();
            var bspzipPath = Path.GetFullPath(Path.Combine(gameFolder, "..", "bin", "bspzip.exe"));
            if (!File.Exists(bspzipPath)) throw new FileNotFoundException("Unable to locate bspzip.exe");

            if (string.IsNullOrWhiteSpace(target)) target = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(file));
            if (Directory.Exists(target)) Directory.Delete(target, true);
            
            var bspFile = Path.ChangeExtension(file, "bsp");
            
            var arguments = new List<string>(_profile.Builders["pack"]);
            arguments.Add($"-extractfiles \"{bspFile}\"");
            arguments.Add($"\"{target}\"");

            Program.Launch(bspzipPath, file, gameFolder, arguments: arguments.ToArray());

        }

        // private void launchProcess(string executable, IEnumerable<string> arguments, string fileName, string gameFolder = "", string targetFile = "")
        // {
        //     var process = new Process();
        //     process.StartInfo = new ProcessStartInfo()
        //     {
        //         FileName = executable,
        //         Arguments = string.Join(" ", arguments.Distinct()),
        //         UseShellExecute = false,
        //         RedirectStandardOutput = true,
        //         CreateNoWindow = true
        //     };
        //     if (!string.IsNullOrWhiteSpace(gameFolder))
        //         process.StartInfo.EnvironmentVariables["VPROJECT"] = gameFolder;
        //
        //     _logger.LogDebug("[{Builder}] [Executing] {FileName} {Arguments}", GetType().Name, process.StartInfo.FileName, process.StartInfo.Arguments);
        //
        //     process.Start();
        //
        //     if (Outputs() || !string.IsNullOrWhiteSpace(targetFile))
        //         while (!process.StandardOutput.EndOfStream)
        //         {
        //             var line = process.StandardOutput.ReadLine();
        //             if (!string.IsNullOrWhiteSpace(targetFile))
        //                 File.AppendAllLines(targetFile, new[]
        //                 {
        //                     line
        //                 });
        //             if (Outputs()) _logger.LogInformation("[{Builder}] [{File}] {Line}", GetType().Name, fileName, line);
        //         }
        //     process.WaitForExit();
        // }

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
