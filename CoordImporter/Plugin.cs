using CoordImporter.Managers;
using CoordImporter.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel.GeneratedSheets;

namespace CoordImporter
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Coordinate Importer";
        private const string CommandName = "/ci";

        public static DalamudPluginInterface PluginInterface { get; set; } = null!;
        public static IChatGui Chat { get; set; } = null!;
        public static IPluginLog Logger { get; set; } = null!;
        public static IDataManagerManager DataManagerManager { get; set; } = null!;

        private ICommandManager CommandManager { get; init; }
        private WindowSystem WindowSystem { get; } = new WindowSystem("CoordinateImporter");
        
        private HuntHelperManager HuntHelperManager { get; init; }

        private MainWindow MainWindow { get; init; }

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] ICommandManager commandManager,
            [RequiredVersion("1.0")] IChatGui chat,
            [RequiredVersion("1.0")] IDataManager dataManager,
            [RequiredVersion("1.0")] IPluginLog logger)
        {
            PluginInterface = pluginInterface;
            this.CommandManager = commandManager;
            Chat = chat;
            Logger = logger;
            DataManagerManager = new DataManagerManager(dataManager);

            HuntHelperManager = new HuntHelperManager();

            MainWindow = new MainWindow(HuntHelperManager);

            WindowSystem.AddWindow(MainWindow);

            this.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
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
