using CoordImporter.Parser;
using CSharpFunctionalExtensions;
using Dalamud;
using Dalamud.Plugin.Services;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace CoordImporter.Managers;

public static class DataManagerExtensions
{
    #region mark name data
    private static IReadOnlyDictionary<string, uint>? MobIdsByName;
    public static IReadOnlyDictionary<string, uint> GetMobIdsByName(this IDataManager dataManager) =>
        MobIdsByName ??= LoadMobIds();

    public static uint? GetMobIdByName(this IDataManager dataManager, string name)
    {
        var valueGotten = dataManager.GetMobIdsByName().TryGetValue(
            name.ToLowerInvariant(),
            out var mobId
        );
        if (valueGotten) return mobId;
        return null;
    }

    private static IReadOnlyDictionary<string, uint> LoadMobIds() =>
        (Enum.GetValuesAsUnderlyingType<ClientLanguage>() as ClientLanguage[])!
        .Select(clientLanguage =>
        {
            Plugin.Logger.Verbose($"Loading mark names for language: {clientLanguage}");

            return Plugin
                .DataManager.GetExcelSheet<BNpcName>(clientLanguage)
                .AsMaybe(() => Plugin.Logger.Verbose($"Could not find BNpcName sheet for language: {clientLanguage}"))
                .Select(nameSheet => nameSheet as IEnumerable<BNpcName>)
                .GetValueOrDefault(new List<BNpcName>())
                .Select(name => ((uint RowId, string Name))(name.RowId, name.Singular.ToString().ToLowerInvariant()))
                .ForEach(name =>
                        Plugin.Logger.Verbose("Found mobId [{0}] for name: {1}", name.RowId, name.Name)
                );
        })
        .Flatten()
        .DistinctBy(name => name.Name)
        .ToImmutableDictionary(
            name => name.Name,
            name => name.RowId
        );
    #endregion

    #region map data
    private static IReadOnlyDictionary<string, MapData>? MapDataByName;
    public static IReadOnlyDictionary<string, MapData> GetMapDataByName(this IDataManager dataManager) =>
        MapDataByName ??= LoadMapData();

    private static IReadOnlyDictionary<string, MapData> LoadMapData()
    {
        // This should support names of maps in all available languages. We create a dictionary where the key is the
        // name of the map (in whatever language) and the value is the map data we care about.
        var mapDict = new Dictionary<string, MapData>();

        var clientLanguages = (Enum.GetValuesAsUnderlyingType<ClientLanguage>() as ClientLanguage[])!;
        foreach (var clientLanguage in clientLanguages)
        {
            Plugin.Logger.Verbose($"Loading map data for language: {clientLanguage}");
            var territorySheet = Plugin.DataManager.GetExcelSheet<TerritoryType>(clientLanguage);
            var placeSheet = Plugin.DataManager.GetExcelSheet<PlaceName>(clientLanguage);
            if (territorySheet == null || placeSheet == null) continue;

            foreach (var territory in territorySheet)
            {
                var placeNameValue = placeSheet.GetRow(territory.PlaceName.Row);
                if (placeNameValue == null)
                {
                    Plugin.Logger.Verbose("Failed to load PlaceName");
                    continue;
                }
                var placeName = placeNameValue.Name.ToString();

                var mapData = new MapData(territory.RowId, territory.Map.Row);

                Plugin.Logger.Verbose($"Adding map with name {placeName} with language {clientLanguage}");
                if (!mapDict.TryAdd(placeName, mapData))
                {
                    Plugin.Logger.Verbose(
                        $"Attempted to add map with name {placeName} for language {clientLanguage} but it already existed");
                }
            }
        }

        return mapDict.ToImmutableDictionary();
    }
    #endregion
}
