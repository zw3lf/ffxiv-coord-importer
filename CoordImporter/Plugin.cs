using CoordImporter.Managers;
using CoordImporter.Parsers;
using CoordImporter.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XIVHuntUtils.Managers;

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

        private ServiceProvider ServiceProvider { get; init; }

        public Plugin(
            IDalamudPluginInterface pluginInterface,
            ICommandManager commandManager,
            IChatGui chat,
            IDataManager dataManager,
            IPluginLog logger)
        {
            ServiceProvider = new ServiceCollection()
                .AddSingleton(logger)
                .AddSingleton(chat)
                .AddSingleton(dataManager)
                .AddSingleton(pluginInterface)
                .AddSingleton<IDataManagerManager, DataManagerManager>()
                .AddSingleton<IMobManager, MobManager>()
                .AddSingleton<ITerritoryManager, TerritoryManager>()
                .AddSingleton<ITravelManager, TravelManager>()
                .AddSingleton<IHuntManager, HuntManager>()
                .AddSingleton<ITrackerParser, BearParser>()
                .AddSingleton<ITrackerParser, FaloopParser>()
                .AddSingleton<ITrackerParser, SirenParser>()
                .AddSingleton<ITrackerParser, TurtleParser>()
                .AddSingleton<BearParser>()
                .AddSingleton<FaloopParser>()
                .AddSingleton<SirenParser>()
                .AddSingleton<TurtleParser>()
                .AddSingleton<Importer>()
                .AddSingleton<MainWindow>()
                .AddSingleton<HuntHelperManager>()
                .AddSingleton<InitializationManager>()
                .BuildServiceProvider();

            PluginInterface = pluginInterface;
            CommandManager = commandManager;
            
            ServiceProvider.GetService<InitializationManager>()!.InitializeNecessaryComponents();

            MainWindow = ServiceProvider.GetService<MainWindow>()!;

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
            ServiceProvider.Dispose();
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
