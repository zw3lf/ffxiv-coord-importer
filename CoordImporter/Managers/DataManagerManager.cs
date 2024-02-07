using CoordImporter.Parser;
using CSharpFunctionalExtensions;
using Dalamud;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace CoordImporter.Managers;

public class DataManagerManager : IDataManagerManager
{
    private readonly IPluginLog Logger;
    private readonly IDataManager DataManager;

    private IReadOnlyDictionary<string, uint> MobIdsByName { get; init; }
    private IReadOnlyDictionary<string, MapData> MapDataByName { get; init; }

    public DataManagerManager(IPluginLog logger, IDataManager dataManager)
    {
        Logger = logger;
        DataManager = dataManager;

        MobIdsByName = LoadMobIds();
        MapDataByName = LoadMapData();
    }

    public ExcelSheet<T>? GetExcelSheet<T>() where T : ExcelRow => DataManager.GetExcelSheet<T>();

    public ExcelSheet<T>? GetExcelSheet<T>(ClientLanguage clientLanguage) where T : ExcelRow =>
        DataManager.GetExcelSheet<T>(clientLanguage);

    public Maybe<uint> GetMobIdByName(string mobName)
    {
        // Bear and Siren have the apostrophe in different locations for Li'l Murderer, so just strip it out here
        if (MobIdsByName.TryGetValue(mobName.ToLowerInvariant().Replace("'", ""), out var mobId)) return mobId;
        return Maybe.None;
    }

    public Maybe<MapData> GetMapDataByName(string mapName)
    {
        if (MapDataByName.TryGetValue(mapName.ToLowerInvariant(), out var map)) return map;
        return Maybe.None;
    }

    private IReadOnlyDictionary<string, uint> LoadMobIds() =>
        (Enum.GetValuesAsUnderlyingType<ClientLanguage>() as ClientLanguage[])!
        .Select(clientLanguage =>
        {
            Logger.Verbose($"Loading mark names for language: {clientLanguage}");

            var nmNameIds = DataManager
                .GetExcelSheet<NotoriousMonster>()
                .AsMaybe(() => Logger.Verbose($"Could not find NotoriousMonster sheet for language: {clientLanguage}"))
                .Select(nmSheet => nmSheet as IEnumerable<NotoriousMonster>)
                .GetValueOrDefault(new List<NotoriousMonster>())
                .Select(notoriousMonster => notoriousMonster.BNpcName.Row)
                .ToImmutableHashSet();

            return DataManager
                .GetExcelSheet<BNpcName>(clientLanguage)
                .AsMaybe(() => Logger.Verbose($"Could not find BNpcName sheet for language: {clientLanguage}"))
                .Select(nameSheet => nameSheet as IEnumerable<BNpcName>)
                .GetValueOrDefault(new List<BNpcName>())
                .Where(name => nmNameIds.Contains(name.RowId))
                // Bear and Siren have the apostrophe in different locations for Li'l Murderer, so just strip it out here
                .Select(name => (
                        RowId: name.RowId,
                        Name: name.Singular.ToString().ToLowerInvariant().Replace("'", "")
                ))
                .ForEach(name =>
                             Logger.Verbose("Found mobId [{0}] for name: {1}", name.RowId, name.Name)
                );
        })
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
            Logger.Verbose($"Loading map data for language: {clientLanguage}");
            var territorySheet = DataManager.GetExcelSheet<TerritoryType>(clientLanguage);
            var placeSheet = DataManager.GetExcelSheet<PlaceName>(clientLanguage);
            if (territorySheet == null || placeSheet == null) continue;

            foreach (var territory in territorySheet)
            {
                var placeNameValue = placeSheet.GetRow(territory.PlaceName.Row);
                if (placeNameValue == null)
                {
                    Logger.Verbose("Failed to load PlaceName");
                    continue;
                }
                var placeName = placeNameValue.Name.ToString().ToLowerInvariant();

                var mapData = new MapData(territory.RowId, territory.Map.Row);

                Logger.Verbose($"Adding map with name {placeName} with language {clientLanguage}");
                if (!mapDict.TryAdd(placeName, mapData))
                {
                    Logger.Verbose(
                        $"Attempted to add map with name {placeName} for language {clientLanguage} but it already existed");
                }
            }
            DataManager.GameData.Excel.RemoveSheetFromCache<PlaceName>();
            DataManager.GameData.Excel.RemoveSheetFromCache<TerritoryType>();
        }

        return mapDict.ToImmutableDictionary();
    }
}
