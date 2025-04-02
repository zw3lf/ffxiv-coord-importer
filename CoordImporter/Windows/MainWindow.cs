using CoordImporter.Managers;
using CSharpFunctionalExtensions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text;
using CoordImporter.Models;
using Dalamud.Game.Text;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using DitzyExtensions.Collection;
using DitzyExtensions.Functional;
using Serilog.Configuration;
using XIVHuntUtils.Managers;
using XIVHuntUtils.Models;

namespace CoordImporter.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private IPluginLog Logger;
    private IChatGui Chat;
    private Importer Importer;
    private HuntHelperManager HuntHelperManager;
    private IHuntManager HuntManager;
    private ITravelManager TravelManager;
    private IDataManagerManager DataManagerManager;

    private string textBuffer = string.Empty;

    public MainWindow(
        IPluginLog logger,
        IChatGui chat,
        Importer importer,
        HuntHelperManager huntHelperManager,
        IHuntManager huntManager,
        ITravelManager travelManager,
        IDataManagerManager dataManagerManager
    ) : base("Coordinate Importer")
    {
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(100, 85),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        Logger = logger;
        Chat = chat;
        Importer = importer;
        HuntHelperManager = huntHelperManager;
        HuntManager = huntManager;
        TravelManager = travelManager;
        DataManagerManager = dataManagerManager;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.Spacing();
        if (ImGui.Button("Import"))
        {
            PerformImport(textBuffer);
        }

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowUpFromBracket))
        {
            ImportToHuntHelper(textBuffer);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Import to Hunt Helper");
        }

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowsUpDown))
        {
            SortByAetheryte(textBuffer);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(160 * ImGuiHelpers.GlobalScale);
            ImGui.Text("Sort the list of marks by proximity to the nearest aetheryte. Does not change the order of maps, just the marks within each map.");
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }

        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - ImGuiHelpers.GetButtonSize("Clean").X);
        if (ImGui.Button("Clean"))
        {
            textBuffer = "";
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Clear the paste window of text");

        ImGui.Text("Paste Coordinates:");
        ImGui.Indent(10);

        Vector2 availableSpace = ImGui.GetContentRegionAvail();
        Vector2 padding = new Vector2(10, 10);

        Vector2 dynamicSize = new Vector2(
            availableSpace.X - padding.X,
            availableSpace.Y - padding.Y
        );

        ImGui.InputTextMultiline("##", ref textBuffer, 16384, dynamicSize, ImGuiInputTextFlags.None);
    }

    private void PerformImport(string payload)
    {
        Importer
            .ParsePayload(payload)
            .ForEach(markDataResult =>
            {
                markDataResult.Match(
                    markData => Chat.Print(new XivChatEntry
                        { Type = XivChatType.Echo, Name = "", Message = new SeString(CreateMapLink(markData)) }),
                    error => Chat.PrintError(error)
                );
            });
    }

    private void ImportToHuntHelper(string payload)
    {
        var marks = Importer
            .ParsePayload(payload)
            .Select(result => result.Match(
                markData => Maybe.From(markData),
                error =>
                {
                    Chat.PrintError(error);
                    return Maybe.None;
                }
            ))
            .Choose()
            .ToImmutableList();

        Logger.Debug(string.Join(", ", marks));

        HuntHelperManager
            .ImportTrainList(marks)
            .Execute(error => Chat.PrintError(error));
    }

    private void SortByAetheryte(string payload)
    {
        var parseResults = Importer
            .ParsePayload(payload)
            .SelectResults()
            .SelectResults(markData => HuntManager
                .FindNearestSpawn2d(markData.TerritoryId, markData.Position)
                .ToResult<Vector3, string>($"no spawn point found for {markData.MarkName} ({markData.Position})")
                .Bind(spawnPoint => TravelManager
                    .FindNearestTravelNode3d(markData.TerritoryId, spawnPoint)
                    .ToResult<TravelNode, string>(
                        $"no travel node found for territory {markData.TerritoryId} ({markData.Position})")
                    .Map(travelNode =>
                        new SortData(markData, spawnPoint, travelNode, ComputeDistance(spawnPoint, travelNode)))))
            .ForEachError(error => Chat.PrintError(error));

        if (parseResults.Errors.AsList().IsNotEmpty()) return;

        var territoryInstanceOrder = new List<TerritoryInstance>();
        var seenTerritoryInstances = new HashSet<TerritoryInstance>();
        var marksByTerritoryInstance = new MultiDict<TerritoryInstance, SortData>();
        parseResults.Value.ForEach(mark =>
        {
            var territoryId = mark.MarkData.TerritoryId;
            if (!seenTerritoryInstances.Contains(new TerritoryInstance(territoryId, mark.MarkData.Instance)))
            {
                Logger.Info($"Adding {territoryId} tid and instanceid {mark.MarkData.Instance}");
                territoryInstanceOrder.Add(new TerritoryInstance(territoryId, mark.MarkData.Instance));
                seenTerritoryInstances.Add(new TerritoryInstance(territoryId, mark.MarkData.Instance));
            }

            marksByTerritoryInstance.Add(new TerritoryInstance(territoryId, mark.MarkData.Instance), mark);
            Logger.Info($"Added {mark} to dict");
        });
        Logger.Info($"{territoryInstanceOrder.ToString()}");
        var newText = new StringBuilder();
        var pathMessage = new TextPayload("optimal hunt path:\n");
        var pathPayloads = new List<Payload>();
        pathPayloads.Add(pathMessage);
        territoryInstanceOrder.ForEach(territoryInstance =>
        {
            Logger.Info($"honk {territoryInstance}");
            var instancesForTerritory = seenTerritoryInstances.Where(ti =>
                ti.TerritoryId == territoryInstance.TerritoryId).OrderBy(x => x.InstanceId);
            Vector3? previousPos = null;
                var territoryPathSegments = marksByTerritoryInstance[territoryInstance]
                    .Sort((a, b) => Math.Sign(a.DistanceFromNearestAetheryte - b.DistanceFromNearestAetheryte))
                    .Select(mark =>
                    {
                        var payloadSegments = new List<List<Payload>>();
                        var fromAetheryte = previousPos is null || (previousPos - mark.SpawnPoint).Value.Length() >
                            mark.DistanceFromNearestAetheryte;
                        List<Payload> travelPayloads = new List<Payload>();
                        if (fromAetheryte)
                        {

                            travelPayloads.Add(new TextPayload("teleport("));
                            travelPayloads.AddRange(CreateAetheryteMapLinkPayload(mark.TravelNode.StartingAetheryte));
                            travelPayloads.Add(new TextPayload(")\n"));
                            if (!mark.TravelNode.IsAetheryte)
                            {
                                travelPayloads.Add(new TextPayload($"{mark.TravelNode.Path}"));

                            }
                        }
                        payloadSegments.Add(travelPayloads);

                        var markPos = mark.MarkData.Position;

                        payloadSegments.Add(CreateMapLink(mark.MarkData));
                        previousPos = mark.SpawnPoint;

                        newText.AppendLine(mark.MarkData.RawText);

                        return payloadSegments;
                    })
                    .AsList();

                territoryPathSegments.ForEach((markSegment, i) =>
                {
                    pathPayloads.Add(new TextPayload("\n"));

                    markSegment.ForEach((informationSegment, j) =>
                    {
                        if (j == markSegment.Count - 1)
                        {
                            pathPayloads.Add(new TextPayload("┗ "));
                        }
                        else if (0 < j)
                        {
                            pathPayloads.Add(new TextPayload("┣ "));
                        }
                        pathPayloads.AddRange(informationSegment);
                    });
                });
        });

        Chat.Print(new XivChatEntry
        {
            Type = XivChatType.Echo, Name = "", Message = new SeString(pathPayloads)
        });
        textBuffer = newText.ToString();
    }

// This is a custom version of Dalamud's CreateMapLink method. It includes the mark name and the instance ID
    private List<Payload> CreateMapLink(MarkData markData)
    {
        var mapLinkPayload =
            new MapLinkPayload(markData.TerritoryId, markData.MapId, markData.Position.X, markData.Position.Y);
        var text = mapLinkPayload.PlaceName + markData.Instance.AsInstanceIcon() + " " +
                   mapLinkPayload.CoordinateString;

        List<Payload> payloads = new List<Payload>()
        {
            mapLinkPayload,
            new TextPayload(text),
            new TextPayload($" ({markData.MarkName})"),
            RawPayload.LinkTerminator
        };
        payloads.InsertRange(1, SeString.TextArrowPayloads);
        return payloads;
    }

    private static float ComputeDistance(Vector3 spawnPoint, TravelNode travelNode) =>
        (spawnPoint - travelNode.Position).Length() + travelNode.DistanceModifier;

    private List<Payload> CreateAetheryteMapLinkPayload(Aetheryte aetheryte)
    {
        var asdf = DataManagerManager.GetMapDataByName(aetheryte.Territory.Name()).Match(
            Result.Success<MapData, TextPayload>,
            () => new TextPayload("(unclickable)")).Map(map =>
            new MapLinkPayload(aetheryte.Territory.Id(), map.RowId, aetheryte.Position.X, aetheryte.Position.Y));

        List<Payload> bleh = new List<Payload>();
        if (asdf.IsSuccess)
        {
            bleh.AddRange(SeString.TextArrowPayloads);
            bleh.Add(asdf.Value);
        }
        else
        {
            bleh.Add(asdf.Error);
        }

        bleh.AddRange(new List<Payload>()
        {
            new TextPayload($"{aetheryte.Name}"),
            RawPayload.LinkTerminator
        });
        return bleh;
    }
}

internal record SortData(
    MarkData MarkData,
    Vector3 SpawnPoint,
    TravelNode TravelNode,
    float DistanceFromNearestAetheryte
);

internal record TerritoryInstance(
    uint TerritoryId,
    uint? InstanceId
);
