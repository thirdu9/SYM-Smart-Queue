using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace SymSmartQueue
{
    public class PluginConfiguration : BasePluginConfiguration 
    {
        // This variable maps directly to the input ID in our HTML file
        public string ListenBrainzToken { get; set; } = string.Empty;
    }

    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public override string Name => "SYM Smart Queue Engine";

        public override Guid Id => Guid.Parse("3f9c2a4e-7d1b-4b8a-9e6f-2c5d8a1f0b73");

        public override string Description => "Handles advanced acoustic queue generation and user telemetry for SYM clients.";

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        public static Plugin? Instance { get; private set; }

        // This routes the Jellyfin UI to our embedded HTML page
        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = this.Name,
                    EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
                }
            };
        }
    }
}