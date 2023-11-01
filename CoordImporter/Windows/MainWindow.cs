using CoordImporter.Managers;
using CoordImporter.Parser;
using CSharpFunctionalExtensions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Dalamud.Interface.Utility;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;

namespace CoordImporter.Windows;

public class MainWindow : Window, IDisposable
{
    private string textBuffer = string.Empty;
    private HuntHelperManager huntHelperManager;

    public MainWindow(HuntHelperManager huntHelperManager) : base("Coordinate Importer")
    {
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(100, 65) * ImGuiHelpers.GlobalScale,
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.huntHelperManager = huntHelperManager;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var windowSize = ImGui.GetWindowContentRegionMax() - ImGui.GetWindowContentRegionMin();
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
        ImGui.Dummy(new Vector2(float.Max(windowSize.X - 2.5f * ImGuiHelpers.GetButtonSize("Clean").X, 50.0f * ImGuiHelpers.GlobalScale), 0.0f));
        ImGui.SameLine();
        if (ImGui.Button("Clean"))
        {
            textBuffer = "";
        }
        ImGui.Text("Paste Coordinates:");

        Vector2 padding = new Vector2(10, 10);
        ImGuiHelpers.ScaledIndent(padding.X);
        Vector2 availableSpace = ImGui.GetContentRegionAvail();

        ImGui.InputTextMultiline("", ref textBuffer, 16384, availableSpace - padding, ImGuiInputTextFlags.None);
    }

    private void PerformImport(string payload)
    {
        Importer
            .ParsePayload(payload)
            .ForEach(markDataResult =>
            {
                markDataResult.Match(
                    markData => Plugin.Chat.Print(CreateMapLink(markData)),
                    error => Plugin.Chat.PrintError(error)
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
                    Plugin.Chat.PrintError(error);
                    return Maybe.None;
                }
            ))
            .Choose()
            .ToImmutableList();
        
        Plugin.Logger.Debug(string.Join(", ", marks));

        huntHelperManager
            .ImportTrainList(marks)
            .Execute(error => Plugin.Chat.PrintError(error));
    }
    
    // This is a custom version of Dalamud's CreateMapLink method. It includes the mark name and the instance ID
    private SeString CreateMapLink(MarkData markData)
    {
        var mapLinkPayload = new MapLinkPayload(markData.TerritoryId, markData.MapId, markData.Position.X, markData.Position.Y);
        var text = mapLinkPayload.PlaceName + markData.Instance.AsInstanceIcon() + " " + mapLinkPayload.CoordinateString;

        List<Payload> payloads = new List<Payload>()
        {
            mapLinkPayload,
            new TextPayload(text),
            new TextPayload($" ({markData.MarkName})"),
            RawPayload.LinkTerminator
        };
        payloads.InsertRange(1, SeString.TextArrowPayloads);
        return new SeString(payloads);
    }
}
