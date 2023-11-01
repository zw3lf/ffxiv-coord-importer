using Dalamud.Interface.Utility;
using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace CoordImporter.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string textBuffer = new String("");

    public MainWindow(Plugin plugin) : base(
        "Coordinate Importer")
    {
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(100, 65),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.Spacing();
        if (ImGui.Button("Import"))
        {
            plugin.EchoString(textBuffer);
        }
        ImGui.SameLine();
        ImGui.Dummy(new Vector2(10, 0)*ImGuiHelpers.GlobalScale);
        ImGui.SameLine();
        if (ImGui.Button("Clean"))
        {
            textBuffer = "";
        }
        ImGui.Text("Paste Coordinates:");

        Vector2 padding = new Vector2(10, 10);
        ImGui.Indent(padding.X);
        Vector2 availableSpace = ImGui.GetContentRegionAvail();

        ImGui.InputTextMultiline("", ref textBuffer, 16384, availableSpace - padding, ImGuiInputTextFlags.None);
    }
}
