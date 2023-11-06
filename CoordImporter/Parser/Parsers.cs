using CSharpFunctionalExtensions;
using Dalamud;
using Dalamud.Game.Text;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace CoordImporter.Parser;

public record struct MapData(
    uint TerritoryId,
    uint RowId
);

internal static class ParsingExtensions
{
    public static string Dump(this GroupCollection groups)
    {
        var groupStrings = groups.Values.Select(group => $"({group.Name}:{group.Value})");
        return string.Join(',', groupStrings);
    }

    public static Result<MarkData, string> CreateMark(this GroupCollection groups, Func<string,uint> instanceParser)
    {
        var mapName = groups["map_name"].Value.Trim();
        if (!ITrackerParser.MapDataByName.TryGetValue(mapName, out var map))
        {
            return $"Failed to find a map with name: {mapName}";
        }
        
        var markName = groups["mark_name"].Value;
        var x = float.Parse(groups["x_coord"].Value, CultureInfo.InvariantCulture);
        var y = float.Parse(groups["y_coord"].Value, CultureInfo.InvariantCulture);
        var instance = groups["instance"].Value.AsMaybe()
            .Select(instance => instance.Length == 0 ? null : instance)
            .Select(instance => (uint?)instanceParser.Invoke(instance!))
            .GetValueOrDefault();

        return new MarkData(markName, mapName, map.TerritoryId, map.RowId, instance, new Vector2(x, y));
    }
}

public interface ITrackerParser
{
    private static IDictionary<string, MapData>? _mapDataByName;

    static IDictionary<string, MapData> MapDataByName
    {
        get => _mapDataByName ??= LoadMapData();
    }

    private static IDictionary<string, MapData> LoadMapData()
    {
            // This should support names of maps in all available languages. We create a dictionary where the key is the
            // name of the map (in whatever language) and the value is the map data we care about.
            var mapDict = new Dictionary<string, MapData>();
            
            var clientLanguages = (Enum.GetValuesAsUnderlyingType<ClientLanguage>() as ClientLanguage[])!;
            foreach (var clientLanguage in clientLanguages)
            {
                var mapSheet = Plugin.DataManager.GetExcelSheet<Map>(clientLanguage);
                if (mapSheet == null) continue;

                foreach (var map in mapSheet)
                {
                    if (!map.PlaceName.IsValueCreated) continue;

                    var placeName = map.PlaceName.Value!.Name.ToString();
                    var mapData = new MapData(map.TerritoryType.Value!.RowId, map.RowId);

                    Plugin.Logger.Verbose($"Adding map with name {placeName} with language {clientLanguage}");
                    if (!mapDict.TryAdd(placeName, mapData))
                    {
                        Plugin.Logger.Verbose($"Attempted to add map with name {placeName} for language {clientLanguage} but it already existed");
                    }
                }
            }

            return mapDict.ToImmutableDictionary();
    }

    bool CanParseLine(string inputLine);
    
    Result<MarkData, string> Parse(string inputLine);
}

public class SirenParser : ITrackerParser
{
    private static readonly char LinkChar = SeIconChar.LinkMarker.ToIconChar();
    private static readonly char I1Char = SeIconChar.Instance1.ToIconChar();
    private static readonly char I9Char = SeIconChar.Instance9.ToIconChar();
    
    // For the format "(Maybe: Storsie) {LinkChar}Labyrinthos{I1Char} ( 17  , 9.6 ) "
    private static readonly Regex SirenRegex = new Regex(
        $@"(\(Maybe: (?<mark_name>[\w+ '-]+)\) {LinkChar})?(?<map_name>[\w+ '-]+)(?<instance>[{I1Char}-{I9Char}]?)\s+\(\s*(?<x_coord>[0-9\.]+)\s*,\s*(?<y_coord>[0-9\.]+)\s*\)",
        RegexOptions.Compiled);

    public bool CanParseLine(string inputLine) => SirenRegex.IsMatch(inputLine);

    public Result<MarkData, string> Parse(string inputLine)
    {
        var groups = SirenRegex.Matches(inputLine).First().Groups;
        Plugin.Logger.Debug( "Siren regex matched for input {0}. Groups are {1}", inputLine, () => groups.Dump());
        return groups.CreateMark(instance => (uint)(instance.First() - I1Char) + 1);
    }
}

public class FaloopParser : ITrackerParser
{
    // For the format "Raiden [S]: Gamma - Yanxia ( 23.6, 11.4 )"
    private static readonly Regex FaloopRegex = new Regex(
        @"(?<world_name>[a-zA-Z0-9'-]+)\s+\[S\]: (?<mark_name>[\w+ '-]+) - (?<map_name>[\w+ '-]+)\s+\(?(?<instance>[1-9]?)\)?\s*\(\s*(?<x_coord>[0-9\.]+)\s*,\s*(?<y_coord>[0-9\.]+)\s*\)",
        RegexOptions.Compiled);

    public bool CanParseLine(string inputLine) => FaloopRegex.IsMatch(inputLine);

    public Result<MarkData, string> Parse(string inputLine)
    {
        var groups = FaloopRegex.Matches(inputLine).First().Groups;
        Plugin.Logger.Debug( "Faloop regex matched for input {0}. Groups are {1}", inputLine, () => groups.Dump());
        return groups.CreateMark(instance => (uint)(instance.First() - '0'));
    }
}

public class BearParser : ITrackerParser
{
    // For the format "Labyrinthos ( 16.5 , 16.8 ) Storsie"
    private static readonly Regex BearRegex = new Regex(
        @"(?<map_name>[\D'\s+-]*)\s*(?<instance>[1-9]?)\s*\(\s*(?<x_coord>[0-9\.]+)\s*,\s*(?<y_coord>[0-9\.]+)\s*\)\s*(?<mark_name>[\w+ +-]+)",
        RegexOptions.Compiled);

    public bool CanParseLine(string inputLine) => BearRegex.IsMatch(inputLine);

    public Result<MarkData, string> Parse(string inputLine)
    {
        var groups = BearRegex.Matches(inputLine).First().Groups;
        Plugin.Logger.Debug( "Bear regex matched for input {0}. Groups are {1}", inputLine, () => groups.Dump());
        return groups.CreateMark(instance => (uint)(instance.First() - '0'));
    }
}
