using Grand.Infrastructure.Plugins;

namespace Grand.Plugin.Api.Extended
{
    public class ApiExtendedPlugin : BasePlugin
    {
        public ApiExtendedPlugin()
        {
        }

        public override async Task Install()
        {
            //locales
            await base.Install();
        }

        public override async Task Uninstall()
        {
            //locales
            await base.Uninstall();
        }
    }
}
