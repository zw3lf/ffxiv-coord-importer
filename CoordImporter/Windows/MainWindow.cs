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
        if (ImGui.Button("Import", new Vector2(80, 24)))
        {
            plugin.EchoString(textBuffer);
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
}
