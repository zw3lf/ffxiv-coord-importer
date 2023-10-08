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

        private DalamudPluginInterface PluginInterface { get; init; }
        private ICommandManager CommandManager { get; init; }
        private WindowSystem WindowSystem = new("CoordinateImporter");
        private IChatGui Chat { get; }
        private IDataManager DataManager { get; }
        private IPluginLog Logger { get; }
        private IDictionary<String, Map> maps { get; set; }

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
            maps = new Dictionary<String, Map>();
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
                                    if (!maps.TryAdd(placeName.Name, map))
                                    {
                                        Logger.Verbose($"Attempted to add map with name {placeName.Name} for language {cl} but it already existed");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            // Invalidate the Placename cache because the sheet in here may not correspond to the correct ClientLanguage.
            // This is because Lumina caches the sheets but it doesn't seem to properly manage the language as part of the caching
            DataManager.Excel.RemoveSheetFromCache<PlaceName>();
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

        public void EchoString(string pastedPayload)
        {
            var instanceKeyMap = new Dictionary<String, String>()
            {
                { "1", "\ue0b1" },
                { "2", "\ue0b2" },
                { "3", "\ue0b3" },
            };

            var type = XivChatType.Echo;
            var splitStrings = pastedPayload.Split(
                new string[] { "\r\n", "\r", "\n" },
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            // For the format "(Maybe: Storsie) \ue0bbLabyrinthos ( 17  , 9.6 ) " (including the icky unicode instance/arrow)
            var sirenRegex = new Regex(
                @"(\(Maybe: (?<mark_name>[\w+ '-]+)\) \ue0bb)?(?<map_name>[\w+ '-]+)(?<instance_id>[\ue0b1\ue0b2\ue0b3]?)\s+\(\s*(?<x_coord>[0-9\.]+)\s*,\s*(?<y_coord>[0-9\.]+)\s*\)",
                RegexOptions.Compiled);
            // For the format "Labyrinthos ( 16.5 , 16.8 ) Storsie"
            var bearRegex = new Regex(
                @"(?<map_name>[\D'\s+-]*)\s*(?<instance_number>[123]?)\s*\(\s*(?<x_coord>[0-9\.]+)\s*,\s*(?<y_coord>[0-9\.]+)\s*\)\s*(?<mark_name>[\w+ +-]+)",
                RegexOptions.Compiled);
            // For the format "Raiden [S]: Gamma - Yanxia ( 23.6, 11.4 )"
            var faloopRegex = new Regex(
                @"(?<world_name>[a-zA-Z0-9'-]+)\s+\[S\]: (?<mark_name>[\w+ '-]+) - (?<map_name>[\w+ '-]+)\s+\(?(?<instance_number>[1-3]?)\)?\s*\(\s*(?<x_coord>[0-9\.]+)\s*,\s*(?<y_coord>[0-9\.]+)\s*\)",
                RegexOptions.Compiled);

            foreach (var inputLine in splitStrings)
            {
                SeString output;
                Match match;
                string? instanceId;
                GroupCollection groups;
                // Check if the little arrow symbol is in the text. If it is then the line is from Siren
                if (inputLine.Contains("\ue0bb"))
                {
                    match = sirenRegex.Matches(inputLine)[0];
                    groups = match.Groups;
                    Logger.Debug($"Siren regex matched for input {inputLine}. Groups are {this.DumpGroups(groups)}");
                    instanceId = groups["instance_id"].Value;
                }
                else
                {
                    // If we get here then the string can be from Faloop or Bear. Easiest way to discern them is to
                    // check if the string contains '[S]', which is unique to Faloop
                    if (inputLine.Contains("[S]"))
                    {
                        // We have a Faloop string
                        match = faloopRegex.Matches(inputLine)[0];
                        Logger.Debug($"Faloop regex matched for input {inputLine}. Groups are {this.DumpGroups(match.Groups)}");
                    }
                    else
                    {
                        // If a map on Bear doesn't have a mark's location then the coordinates are 'NOT AVAILABLE'
                        if (inputLine.Contains("NOT AVAILABLE"))
                        {
                            Logger.Debug($"Input {inputLine} does not have coordinates. Ignoring");
                            continue;
                        }

                        match = bearRegex.Matches(inputLine)[0];
                        groups = match.Groups;
                        Logger.Debug($"Bear regex matched for input {inputLine}. Groups are {this.DumpGroups(groups)}");
                    }
                    groups = match.Groups;
                    // Faloop (and Bear) doesn't use the 1/2/3 instance symbols directly (while Siren does), so 
                    // use this dictionary to get the symbol for the output
                    instanceId = groups["instance_number"].Value.IsNullOrEmpty()
                                     ? null
                                     : instanceKeyMap[groups["instance_number"].Value];
                }
                var mapName = groups["map_name"].Value.Trim();
                maps.TryGetValue(mapName, out var map);
                var markName = groups["mark_name"].Value;
                var x = float.Parse(groups["x_coord"].Value, CultureInfo.InvariantCulture);
                var y = float.Parse(groups["y_coord"].Value, CultureInfo.InvariantCulture);

                if (map != null)
                {
                    output = CreateMapLink(map.TerritoryType.Value!.RowId, map.RowId, x, y, instanceId, markName);
                }
                else
                {
                    var builder = new SeStringBuilder();
                    builder.Append(
                        $"Input text \"{inputLine}\" invalid. Could not find a matching map for {groups["map_name"]}.");
                    output = builder.Build();
                }

                this.Chat.Print(new XivChatEntry
                {
                    Type = type,
                    Name = "",
                    Message = output
                });
            }
        }

        // This is a custom version of Dalamud's CreateMapLink method. It includes the mark name and the instance ID
        private SeString CreateMapLink(
            uint territoryId,
            uint mapId,
            float xCoord,
            float yCoord,
            string? instanceId,
            string markName)
        {
            string text;
            MapLinkPayload mapLinkPayload = new MapLinkPayload(territoryId, mapId, xCoord, yCoord, 0.05f);
            if (instanceId == null)
            {
                text = mapLinkPayload.PlaceName + " " + mapLinkPayload.CoordinateString;
            }
            else
            {
                text = mapLinkPayload.PlaceName + instanceId + " " + mapLinkPayload.CoordinateString;
            }

            List<Payload> payloads = new List<Payload>((IEnumerable<Payload>)new Payload[4]
            {
                (Payload)mapLinkPayload,
                (Payload)new TextPayload(text),
                (Payload)new TextPayload($" ({markName})"),
                (Payload)RawPayload.LinkTerminator
            });
            payloads.InsertRange(1, SeString.TextArrowPayloads);
            return new SeString(payloads);
        }

        private String DumpGroups(GroupCollection groups)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Group group in groups)
            {
                sb.Append($"({group.Name}:{group.Value}),");
            }
            sb.Remove(sb.Length - 1, 1);
            return sb.ToString();
        }
    }
}
