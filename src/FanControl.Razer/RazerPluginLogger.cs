using EMRazer;
using FanControl.Plugins;

namespace FanControl.Razer;

internal class RazerPluginLogger : ILogger
{
    private readonly IPluginLogger _pluginLogger;

    public RazerPluginLogger(IPluginLogger pluginLogger)
    {
        _pluginLogger = pluginLogger;
    }

    public void Log(string message) => _pluginLogger.Log($"[Razer] {message}");
}
