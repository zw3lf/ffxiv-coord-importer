using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CoordImporter.Parser;
using CSharpFunctionalExtensions;
using Dalamud;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace CoordImporter.Managers;

public class DataManagerManager : IDataManagerManager
{
    private readonly IDataManager dataManager;

    private IReadOnlyDictionary<string, uint> MobIdsByName { get; }
    private IReadOnlyDictionary<string, MapData> MapDataByName { get; }

    public DataManagerManager(IDataManager dataManager)
    {
        this.dataManager = dataManager;

        MobIdsByName = LoadMobIds();
        MapDataByName = LoadMapData();
    }

    public ExcelSheet<T>? GetExcelSheet<T>() where T : ExcelRow => dataManager.GetExcelSheet<T>();

    public ExcelSheet<T>? GetExcelSheet<T>(ClientLanguage clientLanguage) where T : ExcelRow =>
        dataManager.GetExcelSheet<T>(clientLanguage);

    public Maybe<uint> GetMobIdByName(string mobName)
    {
        if (MobIdsByName.TryGetValue(mobName.ToLowerInvariant(), out var mobId)) return mobId;
        return Maybe.None;
    }

    public Maybe<MapData> GetMapDataByName(string mapName)
    {
        if (MapDataByName.TryGetValue(mapName.ToLowerInvariant(), out var map)) return map;
        return Maybe.None;
    }

    private IReadOnlyDictionary<string, uint> LoadMobIds() =>
        (Enum.GetValuesAsUnderlyingType<ClientLanguage>() as ClientLanguage[])!
        .Select(
            clientLanguage =>
            {
                Plugin.Logger.Verbose($"Loading mark names for language: {clientLanguage}");

                return dataManager.GetExcelSheet<BNpcName>(clientLanguage)
                    .AsMaybe(
                        () => Plugin.Logger.Verbose($"Could not find BNpcName sheet for language: {clientLanguage}")
                    )
                    .Select(nameSheet => nameSheet as IEnumerable<BNpcName>)
                    .GetValueOrDefault(new List<BNpcName>())
                    .Select(
                        name => ((uint RowId, string Name))(name.RowId, name.Singular.ToString().ToLowerInvariant())
                    )
                    .ForEach(
                        name =>
                            Plugin.Logger.Verbose("Found mobId [{0}] for name: {1}", name.RowId, name.Name)
                    );
            }
        )
        .Flatten()
        .DistinctBy(name => name.Name)
        .ToImmutableDictionary(
            name => name.Name,
            name => name.RowId
        );

    private IReadOnlyDictionary<string, MapData> LoadMapData()
    {
        // This should support names of maps in all available languages. We create a dictionary where the key is the
        // name of the map (in whatever language) and the value is the map data we care about.
        var mapDict = new Dictionary<string, MapData>();

        var clientLanguages = (Enum.GetValuesAsUnderlyingType<ClientLanguage>() as ClientLanguage[])!;
        foreach (var clientLanguage in clientLanguages)
        {
            Plugin.Logger.Verbose($"Loading map data for language: {clientLanguage}");
            var territorySheet = dataManager.GetExcelSheet<TerritoryType>(clientLanguage);
            var placeSheet = dataManager.GetExcelSheet<PlaceName>(clientLanguage);
            if (territorySheet == null || placeSheet == null) continue;

            foreach (var territory in territorySheet)
            {
                var placeNameValue = placeSheet.GetRow(territory.PlaceName.Row);
                if (placeNameValue == null)
                {
                    Plugin.Logger.Verbose("Failed to load PlaceName");
                    continue;
                }

                var placeName = placeNameValue.Name.ToString().ToLowerInvariant();

                var mapData = new MapData(territory.RowId, territory.Map.Row);

                Plugin.Logger.Verbose($"Adding map with name {placeName} with language {clientLanguage}");
                if (!mapDict.TryAdd(placeName, mapData))
                {
                    Plugin.Logger.Verbose(
                        $"Attempted to add map with name {placeName} for language {clientLanguage} but it already existed"
                    );
                }
            }
        }

        return mapDict.ToImmutableDictionary();
    }
}
