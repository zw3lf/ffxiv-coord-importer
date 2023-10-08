using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Text.RegularExpressions;
using Dalamud.Data;
using Dalamud.Game.Gui;
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
    public class MapData
    {
        public uint TerritoryId { get; }
        public uint RowId { get; }

        public MapData(uint territoryId, uint rowId)
        {
            this.TerritoryId = territoryId;
            this.RowId = rowId;
        }
    }

    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Coordinate Importer";
        private const string CommandName = "/ci";

        private DalamudPluginInterface PluginInterface { get; init; }
        private ICommandManager CommandManager { get; init; }
        private WindowSystem WindowSystem = new("CoordinateImporter");
        private IChatGui Chat { get; }
        private IDataManager DataManager { get; }
        private IPluginLog Logger { get; }
        private IDictionary<String, MapData> maps { get; set; }
        private Importer Importer { get; init; }

        private MainWindow MainWindow { get; init; }

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] ICommandManager commandManager,
            IChatGui chat,
            IDataManager dataManager,
            IPluginLog logger)
        {
            this.PluginInterface = pluginInterface;
            this.CommandManager = commandManager;
            this.Chat = chat;
            this.DataManager = dataManager;
            this.Logger = logger;

            // This should support names of maps in all available languages
            // We create a hashtable where the key is the name of the map (in whatever language) and the value is the map object
            maps = new Dictionary<String, MapData>();
            foreach (ClientLanguage cl in ClientLanguage.GetValuesAsUnderlyingType<ClientLanguage>())
            {
                if (DataManager.GetExcelSheet<Map>(cl) != null)
                {
                    for (uint i = 0; i < DataManager.GetExcelSheet<Map>(cl)!.RowCount; i++)
                    {
                        var placeNameSheet = DataManager.GetExcelSheet<PlaceName>(cl);
                        if (placeNameSheet != null)
                        {
                            var map = DataManager.GetExcelSheet<Map>(cl)!.GetRow(i);
                            if (map != null)
                            {
                                var placeName = placeNameSheet.GetRow(map.PlaceName.Row);
                                if (placeName != null)
                                {
                                    Logger.Verbose($"Adding map with name {placeName.Name} with language {cl}");
                                    if (!maps.TryAdd(placeName.Name, new MapData(map.TerritoryType.Value!.RowId, map.RowId)))
                                    {
                                        Logger.Verbose($"Attempted to add map with name {placeName.Name} for language {cl} but it already existed");
                                    }
                                }
                            }
                        }
                    }
                }
                Logger.Debug($"Loaded Map data from ClientLanguage {cl}");
            }

            this.Importer = new Importer(Chat, Logger, maps);

            MainWindow = new MainWindow(this);

            WindowSystem.AddWindow(MainWindow);

            this.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Paste coordinates in dialog box and click 'Import'. Coordinates will show in echo chat."
            });

            this.PluginInterface.UiBuilder.Draw += DrawUI;
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

        public void ParseAndEcho(String pastedBuffer)
        {
            Importer.EchoMarks(Importer.ParsePayload(pastedBuffer));
        }
    }
}
