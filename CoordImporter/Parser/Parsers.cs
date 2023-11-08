using CoordImporter.Managers;
using CSharpFunctionalExtensions;
using Dalamud.Game.Text;
using System;
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
        if (!Plugin.DataManager.GetMapDataByName().TryGetValue(mapName, out var map))
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
    public static void LogParse(string tracker, string inputLine, GroupCollection groups)
    {
        Plugin.Logger.Debug(
            "{0} regex matched for input {1}. Groups are {2}",
            tracker,
            inputLine,
            groups.Dump()
        );
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
        ITrackerParser.LogParse("Siren", inputLine, groups);
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
        ITrackerParser.LogParse("Faloop", inputLine, groups);
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
        ITrackerParser.LogParse("Bear", inputLine, groups);
        return groups.CreateMark(instance => (uint)(instance.First() - '0'));
    }
}
