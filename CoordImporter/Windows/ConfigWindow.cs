using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using DitzyExtensions;
using DitzyExtensions.Collection;
using DitzyExtensions.Functional;
using XIVHuntUtils.Models;
using static CoordImporter.Managers.SortManager;

namespace CoordImporter.Windows;

public class ConfigWindow : Window, IDisposable
{
    private static readonly FontAwesomeIcon SelectableIcon = FontAwesomeIcon.GripLines;
    private static readonly CiConfiguration DefaultConfig = new CiConfiguration().Initialize();

    private readonly IList<Patch> staticPatchOrder = EnumExtensions.GetEnumValues<Patch>().Reverse().AsList();

    private readonly IPluginLog logger;
    private readonly CiConfiguration config;

    public ConfigWindow(IPluginLog logger, CiConfiguration config) : base("Coord Importer Settings")
    {
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(100, 85),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.logger = logger;
        this.config = config;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public override void OnClose()
    {
        config.Save();
    }

    public void OpenConfigWindow()
    {
        IsOpen = true;
    }

    public override void Draw()
    {
        using var bar = ImRaii.TabBar("ci settings");
        if (!bar) return;

        ImGuiPlus.DrawTab("General", DrawGeneralSettings);
        ImGuiPlus.DrawTab("Sorting", DrawSorting);
    }

    private void DrawSorting()
    {
        DrawSortSelection();
        if (config.ActiveSortOrder.Contains(SortCriteria.Aetheryte))
        {
            ImGuiPlus.WideSeparator();
            DrawAetheryteSortSettings();
        }

        if (config.ActiveSortOrder.Contains(SortCriteria.Patch))
        {
            ImGuiPlus.WideSeparator();
            DrawPatchOrderSelection();
        }

        if (config.ActiveSortOrder.Contains(SortCriteria.Map))
        {
            ImGuiPlus.WideSeparator();
            DrawMapOrderSelection();
        }
    }

    private void DrawGeneralSettings()
    {
        ImGuiPlus.Heading("General Settings");
        DrawCheckboxWithTooltip(
            "Print optimal route",
            ref config.PrintOptimalPath,
            "Whether to just print simple map links or the optimal route to the chat window when importing. The optimal route specifies which aetherytes or shortcuts to go through to reach each mark, but does not change the order in which marks are visited."
        );
    }

    private void DrawSortSelection()
    {
        ImGui.TextWrapped(
            """
            Configure the behavior when the sort button is pressed. Criteria higher in the active list take precedence over lower criteria (e.g. if Patch is above Instance then marks will be grouped by patch, and then within each patch group they will be sorted by instance).
            """
        );
        
        ImGuiPlus.ParagraphSpace();
        ImRaii.TreeNode("Available sort criteria:").Contain(() =>
        {
            DrawCriteriaDescription(
                "Patch",
                "Sort marks in patch order, which is configurable once Patch sorting is enabled."
            );
            DrawCriteriaDescription(
                "Map",
                "Sort marks in map order, which is configurable once Map sorting is enabled."
            );
            DrawCriteriaDescription(
                "Instance",
                "Sort marks in ascending instance order. Maps with no instance have an effective instance of 0, for sorting."
            );
            DrawCriteriaDescription(
                "IsMultiInstance",
                "Sort maps by whether they have multiple instances in the import list. Maps with marks in multiple instances will be placed ahead of maps with marks in only a single instance. This is unrelated to the number of instances currently in existence in the game."
            );
            DrawCriteriaDescription(
                "Aetheryte",
                "Sort marks by their proximity to the nearest aetheryte. This takes shortcuts into account, such as the ferryman in the Ruby Sea. Configuration of this sorting method is available once Aetheryte sorting is enabled."
            );
        });

        ImGuiPlus.ParagraphSpace();
        var style = ImGui.GetStyle();
        var tableSize = (.5f * (ImGui.GetWindowSize() - 2 * style.WindowPadding)) with { Y = 0 };
        ImGuiPlus.Table("active", 1, tableSize, () =>
        {
            ImGui.TableSetupColumn("Active Sort Criteria");
            ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
            ImGui.TableSetColumnIndex(0);
            using (ImRaii.PushId("active"))
            {
                ImGui.TableHeader(ImGui.TableGetColumnName());
                ImGuiPlus.CreateTooltip(
                    "Drag the criteria below to reorder them. Right clicking on one disables it.",
                    hoverRequired: true
                );
            }

            var clicked = DrawOrderableList(config.ActiveSortOrder, x => x.ToString(), ImGuiMouseButton.Right);
            clicked?.Run(i => config.ActiveSortOrder.RemoveAt(i));
        });

        ImGui.SameLine();
        ImGuiPlus.Table("inactive", 1, tableSize, () =>
        {
            ImGui.TableSetupColumn("Inactive Sort Criteria");
            ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
            ImGui.TableSetColumnIndex(0);
            using (ImRaii.PushId("inactive"))
            {
                ImGui.TableHeader(ImGui.TableGetColumnName());
                ImGuiPlus.CreateTooltip("Click on criteria to add them to the active list.", hoverRequired: true);
            }

            EnumExtensions
                .GetEnumValues<SortCriteria>()
                .Except(config.ActiveSortOrder)
                .ForEach(criterion =>
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    if (ImGui.Selectable(criterion.ToString())) config.ActiveSortOrder.Add(criterion);
                });
        });

        if (ImGui.Button("Reset Sort Order"))
        {
            config.ActiveSortOrder = new List<SortCriteria>(DefaultConfig.ActiveSortOrder);
        }
    }

    private void DrawAetheryteSortSettings()
    {
        ImGuiPlus.Heading("Aetheryte sort settings");
        ImGui.TextWrapped("These settings control the behavior of the Aetheryte sort criteria.");
        ImGuiPlus.ParagraphSpace();
        DrawCheckboxWithTooltip(
            "Complete maps",
            ref config.AetheryteCompleteMaps,
            "When sorting by aetheryte, the hunt route will never jump to a new map until all marks on the current map have been hunted. (default: enabled)"
        );
        DrawCheckboxWithTooltip(
            "Complete instances",
            ref config.AetheryteKeepMapsTogether,
            "When sorting by aetheryte, the hunt route will go through all instances of a map before jumping to a new map. (default: enabled)"
        );
        DrawCheckboxWithTooltip(
            "Order instances",
            ref config.AetheryteSortInstances,
            "When sorting by aetheryte, the hunt route will always go through instances in numerical order. (default: enabled)"
        );
    }

    private void DrawPatchOrderSelection()
    {
        ImGuiPlus.Heading("Patch sort order");
        ImGui.TextWrapped("Configure the order in which patches are sorted.");

        ImGuiPlus.ParagraphSpace();
        ImGuiPlus.Table("patch order", 1, contents: () =>
        {
            ImGui.TableSetupColumn("patches");
            DrawOrderableList(config.PatchSortOrder, p => p.ToString());
        });

        if (ImGui.Button("Reset patch order"))
        {
            config.PatchSortOrder = new List<Patch>(DefaultConfig.PatchSortOrder);
        }
    }

    private void DrawMapOrderSelection()
    {
        ImGuiPlus.Heading("Map sort order");
        ImGui.TextWrapped("Configure the order in which maps are sorted.");

        ImGuiPlus.ParagraphSpace();
        using (var bar = ImRaii.TabBar("territory_tabs"))
        {
            if (bar)
            {
                staticPatchOrder.ForEach(p =>
                    ImGuiPlus.DrawTab(
                        p.ToString(),
                        DrawTerritorySelectionTab(p),
                        new Vector2(0, ImGui.GetFontSize() * 8)
                    )
                );
            }
        }

        if (ImGui.Button("Reset map order"))
        {
            config
                .TerritorySortOrder
                .SelectEntries((patch, _) => (patch, new List<Territory>(DefaultConfig.TerritorySortOrder[patch])))
                .AsMutableDict();
        }
    }

    private Action DrawTerritorySelectionTab(Patch p)
    {
        return () =>
        {
            ImGuiPlus.Table($"territory order {p.ToString()}", 1, contents: () =>
            {
                ImGui.TableSetupColumn($"territory#{p.ToString()}");
                DrawOrderableList(config.TerritorySortOrder[p], t => t.Name());
            });
        };
    }

    private int? DrawOrderableList<T>(
        IList<T> ls, Func<T, string> itemLabel, ImGuiMouseButton mouseButton = ImGuiMouseButton.Left)
    {
        int? clickedEntry = null;
        for (var n = 0; n < ls.Count; n++)
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);

            var item = ls[n];
            var (active, hovered, clicked) = ImGuiPlus.SelectableWithIcon(SelectableIcon, itemLabel(item), mouseButton);

            if (clicked) clickedEntry = n;

            if (active && !hovered)
            {
                var nextN = n + MathF.Sign(ImGui.GetMouseDragDelta().Y);
                if (0 <= nextN && nextN < ls.Count)
                {
                    ls[n] = ls[nextN];
                    ls[nextN] = item;
                    ImGui.ResetMouseDragDelta();
                }
            }
        }

        return clickedEntry;
    }

    private void DrawCheckboxWithTooltip(ImU8String label, ref bool value, string tooltip)
    {
        ImGui.Checkbox(label, ref value);
        ImGuiPlus.CreateTooltip(tooltip, 24, true);
    }

    private void DrawCriteriaDescription(string criteria, string description)
    {
        ImGuiHelpers.ScaledDummy(new Vector2(4, 0));
        ImGui.SameLine();
        ImGui.TextWrapped(criteria);

        ImGuiHelpers.ScaledDummy(new Vector2(12, 0));
        ImGui.SameLine();
        ImGui.TextWrapped(description);
    }
}
