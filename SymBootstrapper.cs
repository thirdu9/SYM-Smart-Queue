using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MediaBrowser.Common.Configuration;

namespace SymSmartQueue.Startup
{
    public class SymBootstrapper : IHostedService
    {
        private readonly IApplicationPaths _appPaths;
        private readonly ILogger<SymBootstrapper> _logger;

        public SymBootstrapper(IApplicationPaths appPaths, ILogger<SymBootstrapper> logger)
        {
            _appPaths = appPaths;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Fire and forget so we don't hold up Jellyfin's startup
            _ = Task.Run(() => RunDockerCompileScriptAsync(cancellationToken), cancellationToken);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async Task RunDockerCompileScriptAsync(CancellationToken cancellationToken)
        {
            var pluginDir = _appPaths.PluginConfigurationsPath;
            var symDir = Path.Combine(pluginDir, "SymSmartQueue");
            if (!Directory.Exists(symDir)) Directory.CreateDirectory(symDir);

            string binaryPath = Path.Combine(symDir, "essentia_streaming_extractor_music");
            string modelsPath = Path.Combine(symDir, "profile.yaml");

            // If the persistent binary and models already exist, do nothing
            if (File.Exists(binaryPath) && File.Exists(modelsPath))
            {
                _logger.LogInformation("[SYM Engine] Persistent ML dependencies found. Engine is ready.");
                return;
            }

            _logger.LogInformation("[SYM Engine] Dependencies missing. Initiating background Docker compilation...");

            // URL to install script on GitHub
            string scriptUrl = "https://raw.githubusercontent.com/thirdu9/SYM-Smart-Queue/refs/heads/main/install.sh";
            string scriptPath = Path.Combine(symDir, "install.sh");

            try
            {
                using var process = new Process();
                process.StartInfo.FileName = "/bin/bash";
                process.StartInfo.Arguments = $"-c \"curl -sSL {scriptUrl} > '{scriptPath}' && bash '{scriptPath}' '{symDir}'\"";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.Start();

                // Read output to log it asynchronously
                _ = Task.Run(async () => 
                {
                    while (!process.StandardOutput.EndOfStream)
                    {
                        var line = await process.StandardOutput.ReadLineAsync();
                        if (!string.IsNullOrEmpty(line)) _logger.LogInformation("[SYM Build] {Output}", line);
                    }
                }, cancellationToken);

                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode == 0)
                {
                    _logger.LogInformation("[SYM Engine] Background compilation successful! ML Engine is now active.");
                }
                else
                {
                    _logger.LogError("[SYM Engine] Background compilation failed with exit code {Code}.", process.ExitCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SYM Engine] CRITICAL: Failed to execute background compilation script.");
            }
        }
    }
}