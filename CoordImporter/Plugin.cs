using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Configuration;
using Dalamud.Plugin;
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

namespace CoordImporter
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Coordinate Importer";
        private const string CommandName = "/ci";

        private DalamudPluginInterface PluginInterface { get; init; }
        private CommandManager CommandManager { get; init; }
        public WindowSystem WindowSystem = new("CoordinateImporter");
        private ChatGui Chat { get; }
        public DataManager DataManager { get; }
        private List<Map> maps;

        private MainWindow MainWindow { get; init; }

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager,
            ChatGui chat,
            DataManager dataManager)
        {
            this.PluginInterface = pluginInterface;
            this.CommandManager = commandManager;
            this.Chat = chat;
            this.DataManager = dataManager;

            // A nice little bit of jank
            // Dalamud has all the map data indexed by ID. We want it by name. So let's make a local copy of that
            // list for us to traverse each time we want to find a map name. Linear search and all that. 
            maps = new List<Map>();
            if (DataManager.GetExcelSheet<Map>() != null)
            {
                for (uint i = 0; i < DataManager.GetExcelSheet<Map>()!.RowCount; i++)
                {
                    var map = DataManager.GetExcelSheet<Map>()!.GetRow(i);
                    if (map != null)
                    {
                        maps.Add(map);
                    }
                }
            }

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
                @"(?<map_name>[a-zA-Z'\s+-]*)\s*(?<instance_number>[123]?)\s*\(\s*(?<x_coord>[0-9\.]+)\s*,\s*(?<y_coord>[0-9\.]+)\s*\)\s*(?<mark_name>[\w+ +-]+)",
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
                    }
                    else
                    {
                        // If a map on Bear doesn't have a mark's location then the coordinates are 'NOT AVAILABLE'
                        if (inputLine.Contains("NOT AVAILABLE"))
                        {
                            var unavailableMark = new SeStringBuilder();
                            unavailableMark.Append($"Input {inputLine} does not have coordinates. Ignoring");
                            // this.Chat.PrintChat(new XivChatEntry
                            // {
                            //     Type = type,
                            //     Name = "",
                            //     Message = unavailableMark.Build()
                            // });
                            continue;
                        }

                        match = bearRegex.Matches(inputLine)[0];
                    }
                    groups = match.Groups;
                    // Bear doesn't use the 1/2/3 instance symbols directly (while Siren does), so for Bear
                    // use this dictionary to get the symbol for the output
                    instanceId = groups["instance_number"].Value.IsNullOrEmpty()
                                     ? null
                                     : instanceKeyMap[groups["instance_number"].Value];
                }

                Map? map = FindMapIdByName(groups["map_name"].Value.Trim());
                var markName = groups["mark_name"].Value;
                var x = float.Parse(groups["x_coord"].Value, CultureInfo.InvariantCulture);
                var y = float.Parse(groups["y_coord"].Value, CultureInfo.InvariantCulture);

                if (map != null)
                {
                    output = this.CreateMapLink(map.TerritoryType.Value!.RowId, map.RowId, x, y, instanceId, markName);
                }
                else
                {
                    var builder = new SeStringBuilder();
                    builder.Append(
                        $"Input text \"{inputLine}\" invalid. Could not find a matching map for {groups["map_name"]}.");
                    output = builder.Build();
                }

                this.Chat.PrintChat(new XivChatEntry
                {
                    Type = type,
                    Name = "",
                    Message = output
                });
            }
        }

        private Map? FindMapIdByName(string name)
        {
            foreach (Map map in maps)
            {
                if (map.PlaceName.Value != null)
                {
                    if (name == map.PlaceName.Value!.Name)
                    {
                        return map;
                    }
                }
            }

            return null;
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
    }
}
