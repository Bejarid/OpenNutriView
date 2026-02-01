using Eco.Core.Plugins.Interfaces;
using Eco.Shared.Localization;
using Eco.Shared.Utils;
namespace OpenNutriView
{
    public class OpenNutriViewPlugin :
      Singleton<OpenNutriViewPlugin>,
      IModKitPlugin,
      IServerPlugin
    {
        public string GetCategory() => Localizer.DoStr("Mods");

        public string GetStatus() => string.Empty;
    }
}