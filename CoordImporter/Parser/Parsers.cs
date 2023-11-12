using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using CoordImporter.Managers;
using CSharpFunctionalExtensions;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;

namespace CoordImporter.Parser;

public interface ITrackerParser
{
    public bool CanParseLine(string inputLine);

    public Result<MarkData, string> Parse(string inputLine);
}

public abstract class Parsers : ITrackerParser
{
    private IPluginLog Logger { get; init; }
    private IDataManagerManager DataManagerManager { get; init; }

    protected Parsers(IPluginLog logger, IDataManagerManager dataManagerManager)
    {
        Logger = logger;
        DataManagerManager = dataManagerManager;
    }

    protected Result<MarkData, string> CreateMark(GroupCollection groups, Func<string, uint> instanceParser)
    {
        var mapName = groups["map_name"].Value.Trim();
        return DataManagerManager
               .GetMapDataByName(mapName)
               .Match(
                   Result.Success<MapData, string>,
                   () => $"Failed to find a map with name: {mapName}"
               )
               .Map(map =>
               {
                   var markName = groups["mark_name"].Value;
                   var x = float.Parse(groups["x_coord"].Value, CultureInfo.InvariantCulture);
                   var y = float.Parse(groups["y_coord"].Value, CultureInfo.InvariantCulture);
                   var instance = groups["instance"]
                                  .Value.AsMaybe()
                                  .Select(instance => instance.Length == 0
                                                          ? null
                                                          : instance)
                                  .Select(instance => (uint?)instanceParser.Invoke(instance!))
                                  .GetValueOrDefault();

                   return new MarkData(markName, mapName, map.TerritoryId, map.RowId, instance, new Vector2(x, y));
               });
    }

    protected static string Dump(GroupCollection groups)
    {
        var groupStrings = groups.Values.Select(group => $"({group.Name}:{group.Value})");
        return string.Join(',', groupStrings);
    }

    protected void LogParse(string tracker, string inputLine, GroupCollection groups)
    {
        Logger.Debug(
            "{0} regex matched for input {1}. Groups are {2}",
            tracker,
            inputLine,
            Dump(groups)
        );
    }

    public abstract bool CanParseLine(string inputLine);
    public abstract Result<MarkData, string> Parse(string inputLine);
}

public class SirenParser : Parsers
{
    private static readonly char I1Char = SeIconChar.Instance1.ToIconChar();

    // For the format "(Maybe: Storsie) {LinkChar}Labyrinthos{I1Char} ( 17  , 9.6 ) "

    protected readonly Regex Regex = new Regex(
        @"\((Maybe: )?(?<mark_name>[\w '-]+)\) \ue0bb(?<map_name>[\w '-]+)(?<instance>[\ue0b1-\ue0b9]?)\s+\(\s*(?<x_coord>[0-9\.]+)\s*,\s*(?<y_coord>[0-9\.]+)\s*\)",
        RegexOptions.Compiled);


    public override Result<MarkData, string> Parse(string inputLine)
    {
        var groups = Regex.Matches(inputLine).First().Groups;
        LogParse("Siren", inputLine, groups);
        return CreateMark(groups, instance => (uint)(instance.First() - I1Char) + 1);
    }

    public override bool CanParseLine(string inputLine) => Regex.IsMatch(inputLine);

    public SirenParser(IPluginLog logger, IDataManagerManager dataManagerManager) : base(logger, dataManagerManager) { }
}

public class FaloopParser : Parsers
{
    // For the format "Raiden [S]: Gamma - Yanxia ( 23.6, 11.4 )"
    protected readonly Regex Regex = new Regex(
        @"(?<world_name>[a-zA-Z0-9'-]+)\s+\[S\]: (?<mark_name>[\w '-]+) - (?<map_name>[\w '-]+)\s+\(?(?<instance>[1-9]?)\)?\s*\(\s*(?<x_coord>[0-9\.]+)\s*,\s*(?<y_coord>[0-9\.]+)\s*\)",
        RegexOptions.Compiled);


    public override Result<MarkData, string> Parse(string inputLine)
    {
        var groups = Regex.Matches(inputLine).First().Groups;
        LogParse("Faloop", inputLine, groups);
        return CreateMark(groups, instance => (uint)(instance.First() - '0'));
    }

    public override bool CanParseLine(string inputLine) => Regex.IsMatch(inputLine);

    public FaloopParser(IPluginLog logger, IDataManagerManager dataManagerManager) : base(logger, dataManagerManager) { }
}

public class BearParser : Parsers
{
    // For the format "Labyrinthos ( 16.5 , 16.8 ) Storsie"
    protected readonly Regex Regex = new Regex(
        @"(?<map_name>[\\D'\\s-]+)\\s*(?<instance>[1-9]?)\\s*\\(\\s*(?<x_coord>[0-9\\.]+)\\s*,\\s*(?<y_coord>[0-9\\.]+)\\s*\\)\\s*(?<mark_name>\\w[\\w -]+)",
        RegexOptions.Compiled);

    public override Result<MarkData, string> Parse(string inputLine)
    {
        var groups = Regex.Matches(inputLine).First().Groups;
        LogParse("Bear", inputLine, groups);
        return CreateMark(groups, instance => (uint)(instance.First() - '0'));
    }

    public override bool CanParseLine(string inputLine) => Regex.IsMatch(inputLine);

    public BearParser(IPluginLog logger, IDataManagerManager dataManagerManager) : base(logger, dataManagerManager) { }
}
