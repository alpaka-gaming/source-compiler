using System;
using System.IO;
using System.Linq;
using SourceSDK.Interfaces;
using SourceSDK.Models;
using ValveKeyValue;

namespace SourceSDK.Builders
{
    public class MaterialBuilder : IMaterialBuilder
    {
        public Func<string> GamePath { get; set; }
        public Func<bool> Verbose { get; set; }
        public Func<bool> Outputs { get; set; }

        public string Folder => "materials";
        public string FileFormat => "*.vmt";

        //TODO: Better implementation
        private Profile _profile;
        public void Build(string file, Profile profile)
        {
            _profile = profile;

            if (!File.Exists(Path.Combine(GamePath(), "gameinfo.txt"))) throw new FileNotFoundException("Unable to locate gameinfo.txt");

            var filePath = Path.GetDirectoryName(file);
            if (string.IsNullOrEmpty(filePath)) throw new NullReferenceException();
            var sourcePath = Path.Combine(filePath.Split($"\\{Folder}\\").First(), Folder);
            var targetPath = Path.Join(GamePath(), Folder);
            if (!Directory.Exists(targetPath)) Directory.CreateDirectory(targetPath);

            KVObject data = null;
            try
            {
                using (var stream = File.OpenRead(file))
                {
                    var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
                    var options = new KVSerializerOptions
                    {
                        HasEscapeSequences = true
                    };
                    data = kv.Deserialize(stream, options);
                }
            }
            catch (Exception)
            {
                // ignored
            }

            var vmtSourceFile = file;
            var vmtTargetFile = Path.Combine(targetPath, file.Split($"\\{Folder}\\").Last());
            var vmtTargetFolder = Path.GetDirectoryName(vmtTargetFile);
            if (!string.IsNullOrWhiteSpace(vmtTargetFolder))
            {
                if (!Directory.Exists(vmtTargetFolder)) Directory.CreateDirectory(vmtTargetFolder);
                File.Copy(vmtSourceFile, vmtTargetFile, true);
            }
            if (data != null)
                foreach (var item in data)
                {
                    var vtfSourceFile = Path.Combine(sourcePath, $"{item.Value}.vtf");
                    var vtfTargetFile = Path.Combine(targetPath, $"{item.Value}.vtf");
                    if (File.Exists(vtfSourceFile))
                    {
                        var vtfTargetFolder = Path.GetDirectoryName(vtfTargetFile);
                        if (!string.IsNullOrWhiteSpace(vtfTargetFolder))
                        {
                            if (!Directory.Exists(vtfTargetFolder)) Directory.CreateDirectory(vtfTargetFolder);
                            File.Copy(vtfSourceFile, vtfTargetFile, true);
                        }
                    }
                }

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
        ~MaterialBuilder()
        {
            Dispose(false);
        }


        #endregion

    }
}
