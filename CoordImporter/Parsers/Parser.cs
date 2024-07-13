using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using CoordImporter.Managers;
using CoordImporter.Models;
using CSharpFunctionalExtensions;
using Dalamud.Plugin.Services;

namespace CoordImporter.Parsers;

public interface ITrackerParser
{
    public static readonly Result<MarkData, string> ParseErrorIgnoreMark =
        Result.Failure<MarkData, string>("IGNORE");

    public bool CanParseLine(string inputLine);

    public Result<MarkData, string> Parse(string inputLine);
}

public abstract class Parser : ITrackerParser
{
    private IPluginLog Logger { get; init; }
    private IDataManagerManager DataManagerManager { get; init; }

    protected Parser(IPluginLog logger, IDataManagerManager dataManagerManager)
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
