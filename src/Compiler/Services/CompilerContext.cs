using System;
using System.ComponentModel;
using System.IO;
using Microsoft.Extensions.Logging;
using SourceSDK.Interfaces;
using SourceSDK.Models;

namespace SourceSDK
{
    public class CompilerContext : ICompilerContext
    {

        private readonly ILogger _logger;
        private readonly IMapBuilder _mapBuilder;
        private readonly IModelBuilder _modelBuilder;
        private readonly IMaterialBuilder _materialBuilder;
        public CompilerContext(ILoggerFactory loggerFactory, IMapBuilder mapBuilder, IModelBuilder modelBuilder, IMaterialBuilder materialBuilder)
        {
            _logger = loggerFactory.CreateLogger(GetType());
            _mapBuilder = mapBuilder;
            _mapBuilder.Verbose = () => Options?.Verbose ?? false;
            _mapBuilder.GamePath = () => Options?.Game ?? string.Empty;
            _mapBuilder.Outputs = () => Options?.Outputs ?? false;
            _modelBuilder = modelBuilder;
            _modelBuilder.Verbose = () => Options?.Verbose ?? false;
            _modelBuilder.GamePath = () => Options?.Game ?? string.Empty;
            _modelBuilder.Outputs = () => Options?.Outputs ?? false;
            _materialBuilder = materialBuilder;
            _materialBuilder.Verbose = () => Options?.Verbose ?? false;
            _materialBuilder.GamePath = () => Options?.Game ?? string.Empty;
            _materialBuilder.Outputs = () => Options?.Outputs ?? false;
        }

        public Option Options { get; set; }
        public void Compile(string source)
        {

            if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);

            var currentStage = 1;
            int stageValue;
            var progress = 0;

            OnProgress(new ProgressChangedEventArgs(progress, "initialize"));
            var builders = new IBuilder[] { _materialBuilder, _modelBuilder, _mapBuilder };

            stageValue = 99 / builders.Length;
            foreach (var builder in builders)
            {
                var builderName = builder.GetType().Name;
                var v = (currentStage * stageValue);

                try
                {
                    var folder = Path.Combine(source, builder.Folder);
                    if (Directory.Exists(folder))
                    {
                        if (builder == null) throw new NotImplementedException("Requiered builder is not defined");
                        var files = Directory.GetFiles(folder, builder.FileFormat, SearchOption.AllDirectories);
                        for (int i = 0; i < files.Length; i++)
                        {
                            var file = files[i];
                            _logger.LogInformation("[{Builder}] [{File}] [{Index}/{Total}]", builderName, Path.GetFileName(file), i + 1, files.Length);
                            builder.Build(file);
                            progress = v + ((i * v) / files.Length);
                            OnProgress(new ProgressChangedEventArgs(progress, builderName));
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "{Message}", e.Message);
                }
                currentStage++;
            }
            OnProgress(new ProgressChangedEventArgs(100, "completed"));

        }

        public event ProgressChangedEventHandler Progress;
        private void OnProgress(ProgressChangedEventArgs e)
        {
            Progress?.Invoke(this, e);
        }

        #region IDisposable


        private void ReleaseUnmanagedResources()
        {
            // TODO release unmanaged resources here
        }
        protected virtual void Dispose(bool disposing)
        {
            ReleaseUnmanagedResources();
            if (disposing)
            {
                _mapBuilder.Dispose();
                _modelBuilder.Dispose();
                _materialBuilder.Dispose();
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        ~CompilerContext()
        {
            Dispose(false);
        }


        #endregion

    }
}
