using CoordImporter.Managers;
using CoordImporter.Parser;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Text.RegularExpressions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using Lumina.Excel.GeneratedSheets;
using CoordImporter.Windows;
using Dalamud;

namespace CoordImporter
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Coordinate Importer";
        private const string CommandName = "/ci";

        public static DalamudPluginInterface PluginInterface { get; private set; }
        private ICommandManager CommandManager { get; init; }
        private WindowSystem WindowSystem { get; } = new WindowSystem("CoordinateImporter");
        public static IChatGui Chat { get; private set; } = null!;
        public static IDataManager DataManager { get; private set; } = null!;
        public static IPluginLog Logger { get; private set; } = null!;
        
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
            DataManager = dataManager;
            Logger = logger;

            HuntHelperManager = new HuntHelperManager();

            // Invalidate the Placename cache because the sheet in here may not correspond to the correct ClientLanguage.
            // This is because Lumina caches the sheets but it doesn't seem to properly manage the language as part of the caching
            DataManager.Excel.RemoveSheetFromCache<PlaceName>();
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
