using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using CoordImporter.Models;
using CoordImporter.Windows;
using CSharpFunctionalExtensions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using DitzyExtensions;
using DitzyExtensions.Collection;
using DitzyExtensions.Functional;
using XIVHuntUtils.Managers;
using XIVHuntUtils.Models;
using static CoordImporter.Utils;
using SeStringPayloads = System.Collections.Generic.List<Dalamud.Game.Text.SeStringHandling.Payload>;
using TerritoryInstance = (uint territory, uint? instance);

namespace CoordImporter.Managers;

public class SortManager
{
    private const char WideSpace = '\u3000';

    private readonly IPluginLog logger;
    private readonly IChatGui chat;
    private readonly Importer importer;
    private readonly IHuntManager huntManager;
    private readonly ITravelManager travelManager;
    private readonly IDataManagerManager dataManagerManager;
    private readonly CiConfiguration config;

    public SortManager(
        IPluginLog logger,
        IChatGui chat,
        Importer importer,
        IHuntManager huntManager,
        ITravelManager travelManager,
        IDataManagerManager dataManagerManager,
        CiConfiguration config
    )
    {
        this.logger = logger;
        this.chat = chat;
        this.importer = importer;
        this.huntManager = huntManager;
        this.travelManager = travelManager;
        this.dataManagerManager = dataManagerManager;
        this.config = config;
    }

    public string SortEntries(string payload)
    {
        var parseResults = importer
            .ParsePayload(payload)
            .SelectResults()
            .ForEachError(error => chat.PrintError(error));

        if (parseResults.Errors.IsNotEmpty()) return payload;

        var marks = parseResults.Value.AsList();

        var sortedMarks = config
            .ActiveSortOrder
            .AsEnumerable()
            .Reverse()
            .Reduce((acc, criterion) => SortMarks(criterion, acc), marks);

        return sortedMarks.Select(mark => mark.RawText).Join("\n");
    }

    private IList<MarkData> SortMarks(SortCriteria criterion, IList<MarkData> marks)
    {
        if (criterion == SortCriteria.Patch)
        {
            return marks.OrderBy(markData => config
                .PatchSortOrder
                .IndexOf(markData
                    .TerritoryId
                    .AsTerritory()
                    .ContainingPatch()
                )
            ).AsList();
        }

        if (criterion == SortCriteria.Map)
        {
            return marks.OrderBy(markData =>
            {
                var patch = markData
                    .TerritoryId
                    .AsTerritory()
                    .ContainingPatch();
                return config
                    .TerritorySortOrder[patch]
                    .IndexOf(markData.TerritoryId.AsTerritory());
            }).AsList();
        }

        if (criterion == SortCriteria.Instance)
        {
            return marks.OrderBy(markData => markData.Instance ?? 1).AsList();
        }

        if (criterion == SortCriteria.Aetheryte)
        {
            return SortByAetheryte(marks);
        }

        if (criterion == SortCriteria.IsMultiInstance)
        {
            var territoryInstances = marks
                .GroupBy(mark => mark.TerritoryId)
                .Select(g => (
                    g.Key,
                    1 < g.DistinctBy(mark => mark.Instance).Count() ? 0 : 1
                ))
                .AsDict();
            return marks.OrderBy(mark => territoryInstances[mark.TerritoryId]).AsList();
        }

        throw new ArgumentOutOfRangeException(nameof(criterion), criterion, null);
    }

    private IList<MarkData> SortByAetheryte(IList<MarkData> marks)
    {
        logger.Info("Sorting marks by nearest aetheryte...");

        var sortData = GetTravelData(marks).ForEachError(error => chat.PrintError(error));

        if (sortData.HasErrors) return marks;

        var sortedMarks = sortData.Value
            .OrderBy(mark => mark.DistanceFromNearestAetheryte)
            .AsEnumerable();

        if (config.AetheryteSortInstances) sortedMarks = AetheryteSortInstances(sortedMarks);
        if (config.AetheryteKeepMapsTogether) sortedMarks = AetheryteKeepMapsTogether(sortedMarks);
        if (config.AetheryteCompleteMaps) sortedMarks = AetheryteCompleteMaps(sortedMarks);

        return sortedMarks.Select(sortData => sortData.MarkData).AsList();
    }

    private IEnumerable<SortData> AetheryteCompleteMaps(IEnumerable<SortData> marks)
    {
        return marks
            .GroupBy(sortData => (sortData.MarkData.TerritoryId, sortData.MarkData.Instance ?? 1))
            .SelectMany(group => group);
    }

    private IEnumerable<SortData> AetheryteKeepMapsTogether(IEnumerable<SortData> marks)
    {
        return marks
            .GroupBy(sortData => sortData.MarkData.TerritoryId)
            .SelectMany(group => group);
    }

    private IEnumerable<SortData> AetheryteSortInstances(IEnumerable<SortData> marks)
    {
        var markList = marks.AsMutableList();
        var territoryIxs = markList
            .Select((mark, i) => (mark.MarkData.TerritoryId, i))
            .AsMultiDict();

        territoryIxs
            .Keys
            .SelectMany(territoryId =>
            {
                var ixs = territoryIxs[territoryId];
                return ixs
                    .Select(i => markList[i])
                    .OrderBy(mark => mark.MarkData.Instance ?? 1)
                    .ZipWith(ixs);
            })
            .ForEachEntry((mark, i) => markList[i] = mark);

        return markList;
    }

    public void PrintOptimalPath(string payload)
    {
        var parseResults = importer
            .ParsePayload(payload)
            .SelectResults()
            .ForEachError(error => chat.PrintError(error));

        if (parseResults.Errors.IsNotEmpty()) return;

        var marks = GetTravelData(parseResults.Value);

        if (marks.Errors.IsNotEmpty()) return;

        TerritoryInstance? prevTerritoryInstance = null;
        Vector3? prevPos = null;
        var segments = new List<List<SeStringPayloads>>();
        var territoryInstanceSegments = new List<SeStringPayloads>();
        marks.Value.ForEach(mark =>
            {
                var markTravelSegments = new List<SeStringPayloads>();
                var travelSegment = new SeStringPayloads();

                if (prevTerritoryInstance != mark.TerritoryInstance())
                {
                    prevPos = null;
                    if (prevTerritoryInstance != null)
                    {
                        segments.Add(territoryInstanceSegments);
                        territoryInstanceSegments = [];
                    }
                }

                var fromAetheryte =
                    prevPos is null
                    || (prevPos.Value - mark.SpawnPoint).Length() > mark.DistanceFromNearestAetheryte;
                if (fromAetheryte)
                {
                    travelSegment.Add(new TextPayload("teleport to "));
                    travelSegment.AddRange(
                        dataManagerManager.CreateAetheryteMapLink(
                            mark.TravelNode.StartingAetheryte,
                            mark.MarkData.Instance
                        )
                    );
                    markTravelSegments.Add(travelSegment);
                    if (!mark.TravelNode.IsAetheryte)
                    {
                        markTravelSegments.Add([new TextPayload($"{mark.TravelNode.Path}")]);
                    }
                }

                markTravelSegments.Add(CreateMapLink(mark.MarkData));
                prevTerritoryInstance = mark.TerritoryInstance();
                prevPos = mark.SpawnPoint;

                territoryInstanceSegments.AddRange(markTravelSegments);
            })
            .AsList();
        segments.Add(territoryInstanceSegments);

        var pathPayloads = segments
            .SelectMany((territoryPathSegments, territoryIndex) =>
                territoryPathSegments.Select((pathSegment, segmentIndex) =>
                    {
                        var treePrefix = new StringBuilder();

                        var isLastTerritory = territoryIndex == segments.Count - 1;
                        var isFirstTerritorySegment = segmentIndex == 0;
                        var isLastTerritorySegment = segmentIndex == territoryPathSegments.Count - 1;

                        if (isLastTerritory)
                            treePrefix.Append(isFirstTerritorySegment ? '┗' : WideSpace);
                        else
                            treePrefix.Append(isFirstTerritorySegment ? '┣' : '┃');

                        if (isFirstTerritorySegment)
                            treePrefix.Append('┳');
                        else if (isLastTerritorySegment)
                            treePrefix.Append('┗');
                        else
                            treePrefix.Append('┣');
                        treePrefix.Append(' ');

                        var newSegment = new SeStringPayloads();
                        newSegment.Add(new TextPayload(treePrefix.ToString()));
                        newSegment.AddRange(pathSegment);
                        return newSegment;
                    })
                    .Concat(territoryIndex == segments.Count - 1 ? [] : [[new TextPayload("┃")]])
            )
            .Reduce((pathSegments, segment) =>
            {
                pathSegments.AddRange(segment);
                pathSegments.Add(new NewLinePayload());
                return pathSegments;
            }, new SeStringPayloads
            {
                new NewLinePayload(),
                new TextPayload("optimal hunt path:"),
                new NewLinePayload()
            });

        chat.Print(new XivChatEntry
        {
            Type = XivChatType.Echo, Name = "", Message = new SeString(pathPayloads)
        });
    }

    private AccumulatedResults<IEnumerable<SortData>, string> GetTravelData(IEnumerable<MarkData> marks)
    {
        return marks.SelectResults(markData => huntManager
            .FindNearestSpawn2d(markData.TerritoryId, markData.Position)
            .ToResult<Vector3, string>($"no spawn point found for {markData.MarkName} ({markData.Position})")
            .Bind(spawnPoint => travelManager
                .FindNearestTravelNode3d(markData.TerritoryId, spawnPoint)
                .ToResult<TravelNode, string>(
                    $"no travel node found for territory {markData.TerritoryId} ({markData.MapName}), mark {markData.MarkName} ({markData.Position})")
                .Map(travelNode =>
                    new SortData(markData, spawnPoint, travelNode, ComputeDistance(spawnPoint, travelNode))
                )
            )
        );
    }

    private static float ComputeDistance(Vector3 spawnPoint, TravelNode travelNode) =>
        (spawnPoint - travelNode.Position).Length() + travelNode.DistanceModifier;

    internal record SortData(
        MarkData MarkData,
        Vector3 SpawnPoint,
        TravelNode TravelNode,
        float DistanceFromNearestAetheryte
    )
    {
        public TerritoryInstance TerritoryInstance() => MarkData.TerritoryInstance();
    };

    public enum SortCriteria
    {
        Patch,
        Map,
        Aetheryte,
        Instance,
        IsMultiInstance,
    }
}
