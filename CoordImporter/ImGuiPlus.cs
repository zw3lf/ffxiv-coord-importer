using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace CoordImporter;

public static class ImGuiPlus
{
    public static void WithScaledFont(float scale, Action contents)
    {
        var font = ImGui.GetFont();
        var originalScale = font.Scale;
        font.Scale *= scale;
        try
        {
            ImGui.PushFont(font);
            contents();
        } finally
        {
            font.Scale = originalScale;
            ImGui.PopFont();
        }
    }

    public static void ParagraphSpace()
    {
        ImGui.Dummy(new Vector2(0, .5f * ImGui.GetFontSize()));
    }

    public static void Separator()
    {
        var dummySize = new Vector2(0, ImGui.GetStyle().FramePadding.Y);
        ImGui.Dummy(dummySize);
        ImGui.Separator();
        ImGui.Dummy(dummySize);
    }

    public static void WideSeparator()
    {
        ParagraphSpace();
        Separator();
        ParagraphSpace();
    }

    public static void CreateTooltip(string text, float width = 12f, bool hoverRequired = false)
    {
        if (hoverRequired && !ImGui.IsItemHovered()) return;

        using var _ = ImRaii.Tooltip();
        using var __ = ImRaii.TextWrapPos(ImGui.GetFontSize() * width);
        ImGui.TextUnformatted(text);
    }

    public static void Heading(string text, float scale = 1.25f, bool centered = false)
    {
        WithScaledFont(scale, () =>
        {
            if (centered) ImGuiHelpers.CenteredText(text);
            else ImGui.Text(text);
        });
    }

    public static bool ClickableHelpMarker(string helpText, float width = 20f) =>
        ClickableHelpMarker(() => ImGui.TextUnformatted(helpText), width);

    public static bool ClickableHelpMarker(Action tooltipContents, float width = 20f)
    {
        ImGui.SameLine();

        WithScaledFont(.6f, () => ImGui.TextDisabled(FontAwesomeIcon.Question.ToIconString()));

        var clicked = ImGui.IsItemClicked();

        if (ImGui.IsItemHovered())
        {
            using var _ = ImRaii.Tooltip();
            using var __ = ImRaii.TextWrapPos(ImGui.GetFontSize() * width);
            tooltipContents();
        }

        return clicked;
    }

    public static void DrawTab(string label, Action contentAction, Vector2 size = default)
    {
        using var item = ImRaii.TabItem(label);
        if (!item) return;

        using var child = ImRaii.Child("tab_content", size: size);
        if (!child) return;

        contentAction();
    }

    // copied from ImGuiComponents.IconButtonWithText(), removing the button aspect.
    public static (bool active, bool hovered, bool clicked) SelectableWithIcon(
        FontAwesomeIcon icon, string text, ImGuiMouseButton mouseButton = ImGuiMouseButton.Left)
    {
        var textStr = text;
        if (textStr.Contains('#'))
        {
            textStr = textStr[..textStr.IndexOf('#', StringComparison.Ordinal)];
        }

        ImGui.Selectable($"##{textStr}");

        var active = ImGui.IsItemActive();
        var hovered = ImGui.IsItemHovered();
        var clicked = ImGui.IsItemClicked(mouseButton);

        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            ImGui.SameLine();
            ImGui.Text(icon.ToIconString());
        }

        ImGui.SameLine();
        ImGui.Text(textStr);

        return (active, hovered, clicked);
    }

    public static void Table(
        string id,
        int numColumns,
        Vector2 outerSize = default,
        Action? contents = null
    ) =>
        ImRaii.Table(id, numColumns, ImGuiTableFlags.None, outerSize).Contain(contents);

    public static void Contain(this ImRaii.IEndObject context, Action? contents)
    {
        using var visible = context;
        if (!visible) return;
        contents?.Invoke();
    }
}
