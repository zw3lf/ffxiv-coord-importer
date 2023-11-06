using CoordImporter.Parser;
using CSharpFunctionalExtensions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace CoordImporter.Windows;

public class MainWindow : Window, IDisposable
{
    private string textBuffer = string.Empty;

    public MainWindow() : base("Coordinate Importer")
    {
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(100, 65),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.Spacing();
        if (ImGui.Button("Import", new Vector2(80, 24)))
        {
            PerformImport(textBuffer);
        }
        ImGui.SameLine();
        if (ImGui.Button(FontAwesomeIcon.ArrowUpFromBracket.ToString()))
        {
            ImportToHuntHelper(textBuffer);
        }
        ImGui.SameLine();
        ImGui.Dummy(new Vector2(24.0f, 0.0f));
        ImGui.SameLine();
        if (ImGui.Button("Clean", new Vector2(80, 24)))
        {
            textBuffer = "";
        }
        ImGui.Text("Paste Coordinates:");
        ImGui.Indent(10);

        Vector2 availableSpace = ImGui.GetContentRegionAvail();
        Vector2 padding = new Vector2(10, 10);

        Vector2 dynamicSize = new Vector2(
            availableSpace.X - padding.X,
            availableSpace.Y - padding.Y
        );

        ImGui.InputTextMultiline("", ref textBuffer, 16384, dynamicSize, ImGuiInputTextFlags.None);
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
