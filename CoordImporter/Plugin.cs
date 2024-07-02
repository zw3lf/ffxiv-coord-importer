using CoordImporter.Managers;
using CoordImporter.Parser;
using CoordImporter.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CoordImporter
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Coordinate Importer";
        private const string CommandName = "/ci";

        private IDalamudPluginInterface PluginInterface { get; init; }
        private ICommandManager CommandManager { get; init; }
        private WindowSystem WindowSystem { get; } = new WindowSystem("CoordinateImporter");

        private MainWindow MainWindow { get; init; }

        private IHost host { get; init; }

        public Plugin(
            IDalamudPluginInterface pluginInterface,
            ICommandManager commandManager,
            IChatGui chat,
            IDataManager dataManager,
            IPluginLog logger)
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();
            builder.Services.AddSingleton(logger);
            builder.Services.AddSingleton(chat);
            builder.Services.AddSingleton(dataManager);
            builder.Services.AddSingleton(pluginInterface);
            builder.Services.AddSingleton<IDataManagerManager, DataManagerManager>();
            builder.Services.AddSingleton<ITrackerParser, BearParser>();
            builder.Services.AddSingleton<ITrackerParser, FaloopParser>();
            builder.Services.AddSingleton<ITrackerParser, SirenParser>();
            builder.Services.AddSingleton<BearParser>();
            builder.Services.AddSingleton<FaloopParser>();
            builder.Services.AddSingleton<SirenParser>();
            builder.Services.AddSingleton<Importer>();
            builder.Services.AddSingleton<MainWindow>();
            builder.Services.AddSingleton<HuntHelperManager>();
            host = builder.Build();

            PluginInterface = pluginInterface;
            CommandManager = commandManager;

            MainWindow = host.Services.GetService<MainWindow>()!;

            WindowSystem.AddWindow(MainWindow);

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Paste coordinates in dialog box and click 'Import'. Coordinates will show in echo chat."
            });

            PluginInterface.UiBuilder.Draw += DrawUI;
        }

        public void Dispose()
        {
            this.WindowSystem.RemoveAllWindows();
            MainWindow.Dispose();
            this.CommandManager.RemoveHandler(CommandName);
            host.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            // in response to the slash command, just display our main ui
            MainWindow.IsOpen = true;
        }

        private void DrawUI()
        {
            this.WindowSystem.Draw();
        }
    }
}
