using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace CoordImporter.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    private string textBuffer = new String("");

    public MainWindow(Plugin plugin) : base(
        "Coordinate Importer")
    {
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(100, 50),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.Plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.Spacing();
        ImGui.Text("Paste Coordinates:");
        ImGui.Indent(10);
        ImGui.InputTextMultiline("", ref textBuffer, 16384, new Vector2(250, 250), ImGuiInputTextFlags.None);
        ImGui.Unindent(10);
        if (ImGui.Button("Import", new Vector2(260, 32)))
        {
            Plugin.EchoString(textBuffer);
        }
    }
}
