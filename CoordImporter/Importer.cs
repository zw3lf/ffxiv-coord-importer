using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Lumina.Excel.GeneratedSheets;

namespace CoordImporter
{
    public class MarkInformation
    {
        public MapData? map { get; }
        public float xCoord { get; }
        public float yCoord { get; }
        public string? instanceId { get; }
        public string markName { get; }
        public string inputLine { get; }
        public string mapName { get; }

        public MarkInformation(MapData? map, float xCoord, float yCoord, string? instanceId, string markName, string inputLine, string mapName)
        {
            this.map = map;
            this.xCoord = xCoord;
            this.yCoord = yCoord;
            this.instanceId = instanceId;
            this.markName = markName;
            this.inputLine = inputLine;
            this.mapName = mapName;
        }
    }

    public class Importer
    {
        private IChatGui Chat { get; }
        private IPluginLog Logger { get; }
        private IDictionary<String, MapData> Maps { get; }

        public static Dictionary<String, String> InstanceKeyMap => new Dictionary<String, String>()
        {
            { "1", "\ue0b1" },
            { "2", "\ue0b2" },
            { "3", "\ue0b3" },
        };

        // For the format "(Maybe: Storsie) \ue0bbLabyrinthos ( 17  , 9.6 ) " (including the icky unicode instance/arrow)
        public static Regex SirenRegex => new Regex(
            @"(\((Maybe: )?(?<mark_name>[\w+ '-]+)\) \ue0bb)?(?<map_name>[\w+ '-]+)(?<instance_id>[\ue0b1\ue0b2\ue0b3]?)\s+\(\s*(?<x_coord>[0-9\.]+)\s*,\s*(?<y_coord>[0-9\.]+)\s*\)",
            RegexOptions.Compiled);

        // For the format "Labyrinthos ( 16.5 , 16.8 ) Storsie"
        public static Regex BearRegex => new Regex(
            @"(?<map_name>[\D'\s+-]*)\s*(?<instance_number>[123]?)\s*\(\s*(?<x_coord>[0-9\.]+)\s*,\s*(?<y_coord>[0-9\.]+)\s*\)\s*(?<mark_name>[\w+ +-]+)",
            RegexOptions.Compiled);

        // For the format "Raiden [S]: Gamma - Yanxia ( 23.6, 11.4 )"
        // Including ・ and : because some of the Japanese names have them
        public static Regex FaloopRegex => new Regex(
            @"(?<world_name>[a-zA-Z0-9'-]+)\s+\[S\]: (?<mark_name>[\w+・ '-]+) - (?<map_name>[\w+ :'-]+)\s+\(?(?<instance_number>[1-3]?)\)?\s*\(\s*(?<x_coord>[0-9\.]+)\s*,\s*(?<y_coord>[0-9\.]+)\s*\)",
            RegexOptions.Compiled);

        public Importer(
            IChatGui chat,
            IPluginLog logger,
            IDictionary<String, MapData> maps)
        {
            Chat = chat;
            Logger = logger;
            Maps = maps;
        }


        public List<MarkInformation> ParsePayload(string pastedPayload)
        {

            var splitStrings = pastedPayload.Split(
                new string[] { "\r\n", "\r", "\n" },
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            List<MarkInformation> marks = new List<MarkInformation>();
            foreach (var inputLine in splitStrings)
            {
                Match match;
                string? instanceId;
                GroupCollection groups;
                // Check if the little arrow symbol is in the text. If it is then the line is from Siren
                if (inputLine.Contains("\ue0bb"))
                {
                    match = SirenRegex.Matches(inputLine)[0];
                    groups = match.Groups;
                    Logger.Debug($"Siren regex matched for input {inputLine}. Groups are {this.DumpGroups(groups)}");
                    instanceId = groups["instance_id"].Value.IsNullOrEmpty()
                                     ? null
                                     : groups["instance_id"].Value;
                }
                else
                {
                    // If we get here then the string can be from Faloop or Bear. Easiest way to discern them is to
                    // check if the string contains '[S]', which is unique to Faloop
                    if (inputLine.Contains("[S]"))
                    {
                        // We have a Faloop string
                        match = FaloopRegex.Matches(inputLine)[0];
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

                        match = BearRegex.Matches(inputLine)[0];
                        groups = match.Groups;
                        Logger.Debug($"Bear regex matched for input {inputLine}. Groups are {this.DumpGroups(groups)}");
                    }
                    groups = match.Groups;
                    // Faloop (and Bear) doesn't use the 1/2/3 instance symbols directly (while Siren does), so 
                    // use this dictionary to get the symbol for the output
                    instanceId = groups["instance_number"].Value.IsNullOrEmpty()
                                     ? null
                                     : InstanceKeyMap[groups["instance_number"].Value];
                }
                var mapName = groups["map_name"].Value.Trim();
                Maps.TryGetValue(mapName, out var map);
                var markName = groups["mark_name"].Value;
                var x = float.Parse(groups["x_coord"].Value, CultureInfo.InvariantCulture);
                var y = float.Parse(groups["y_coord"].Value, CultureInfo.InvariantCulture);

                marks.Add(new MarkInformation(map, x, y, instanceId, markName, inputLine, mapName));
            }
            return marks;
        }

        public void EchoMarks(List<MarkInformation> marks)
        {
            foreach (var mark in marks)
            {
                SeString output;
                if (mark.map != null)
                {
                    output = this.CreateMapLink(mark.map.TerritoryId, mark.map.RowId, mark.xCoord, mark.yCoord, mark.instanceId, mark.markName);
                }
                else
                {
                    var builder = new SeStringBuilder();
                    builder.Append(
                        $"Input text \"{mark.inputLine}\" invalid. Could not find a matching map for {mark.mapName}.");
                    output = builder.Build();
                }

                this.Chat.Print(new XivChatEntry
                {
                    Type = XivChatType.Echo,
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
