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
using static CoordImporter.Utils;
using SeStringPayloads = System.Collections.Generic.List<Dalamud.Game.Text.SeStringHandling.Payload>;

namespace CoordImporter.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private const char WideSpace = '\u3000';

    private IPluginLog Logger;
    private IChatGui Chat;
    private Importer Importer;
    private HuntHelperManager HuntHelperManager;
    private IHuntManager HuntManager;
    private ITravelManager TravelManager;
    private IDataManagerManager DataManagerManager;
    private ConfigWindow ConfigWindow;
    private SortManager SortManager;
    private CiConfiguration Config;

    private string textBuffer = string.Empty;

    public MainWindow(
        IPluginLog logger,
        IChatGui chat,
        Importer importer,
        HuntHelperManager huntHelperManager,
        IHuntManager huntManager,
        ITravelManager travelManager,
        IDataManagerManager dataManagerManager,
        ConfigWindow configWindow,
        SortManager sortManager,
        CiConfiguration config
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
        ConfigWindow = configWindow;
        SortManager = sortManager;
        this.Config = config;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.Spacing();
        if (ImGui.Button("Import"))
        {
            if (Config.PrintOptimalPath)
            {
                SortManager.PrintOptimalPath(textBuffer);
            }
            else
            {
                PerformImport(textBuffer);
            }
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
            textBuffer = SortManager.SortEntries(textBuffer);
        }

        if (ImGui.IsItemHovered())
            ImGuiPlus.CreateTooltip(
                "Sort the list of marks. Sorting can be configured in the Coord Importer settings."
            );

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Cog))
        {
            ConfigWindow.OpenConfigWindow();
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
}

internal record TerritoryInstance(
    uint TerritoryId,
    uint? InstanceId
);
