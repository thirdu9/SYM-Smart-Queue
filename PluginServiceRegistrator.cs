using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using SymSmartQueue.Data;
using SymSmartQueue.Startup;

namespace SymSmartQueue
{
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        // Depending on your exact Jellyfin 10.9 build, the interface signature may not require the applicationHost parameter.
        // If your agent gets a compilation error regarding the implementation, simply remove the second parameter.
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            // Register database engine as a Singleton so all API calls and background tasks share the same SQLite connection logic.
            serviceCollection.AddHostedService<SymBootstrapper>();
            serviceCollection.AddSingleton<DatabaseManager>();
        }
    }
}