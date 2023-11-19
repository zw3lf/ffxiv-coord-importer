using CoordImporter.Managers;
using CoordImporter.Parsers;
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

        private DalamudPluginInterface PluginInterface { get; init; }
        private ICommandManager CommandManager { get; init; }
        private WindowSystem WindowSystem { get; } = new WindowSystem("CoordinateImporter");

        private MainWindow MainWindow { get; init; }

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] ICommandManager commandManager,
            [RequiredVersion("1.0")] IChatGui chat,
            [RequiredVersion("1.0")] IDataManager dataManager,
            [RequiredVersion("1.0")] IPluginLog logger)
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();
            builder.Services.AddSingleton(logger);
            builder.Services.AddSingleton(chat);
            builder.Services.AddSingleton(dataManager);
            builder.Services.AddSingleton<IDataManagerManager, DataManagerManager>();
            builder.Services.AddSingleton<ICiDataManager>(new CiDataManager(pluginInterface.DataFilePath("CustomNames.json")));
            builder.Services.AddSingleton<ITrackerParser, BearParser>();
            builder.Services.AddSingleton<ITrackerParser, FaloopParser>();
            builder.Services.AddSingleton<ITrackerParser, SirenParser>();
            builder.Services.AddSingleton<BearParser>();
            builder.Services.AddSingleton<FaloopParser>();
            builder.Services.AddSingleton<SirenParser>();
            builder.Services.AddSingleton<Importer>();
            using IHost host = builder.Build();

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
