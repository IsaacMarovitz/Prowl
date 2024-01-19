using ImGuiNET;
using Prowl.Runtime;

namespace Prowl.Editor.PropertyDrawers;

public class PropertyDrawerSystemVector2 : PropertyDrawer<System.Numerics.Vector2> {

    protected override bool Draw(string label, ref System.Numerics.Vector2 v2, float width)
    {
        bool changed = false;
        DrawLabel(label, ref width);

        ImGui.PushItemWidth(width / 2 - 13.5f);
        ImGui.Text("X");
        ImGui.SameLine();
        changed |= ImGui.DragFloat("##X", ref v2.X, 1);
        ImGui.SameLine();
        ImGui.Text("Y");
        ImGui.SameLine();
        changed |= ImGui.DragFloat("##Y", ref v2.Y, 1);

        ImGui.PopItemWidth();
        ImGui.Columns(1);
        return changed;
    }

}

public class PropertyDrawerVector2 : PropertyDrawer<Vector2> {

    protected override bool Draw(string label, ref Vector2 v2, float width)
    {
        bool changed = false;
        DrawLabel(label, ref width);

        ImGui.PushItemWidth(width / 2 - 13.5f);
        ImGui.Text("X");
        ImGui.SameLine();
        changed |= GUIHelper.DragDouble("##X", ref v2.x, 0.01f);
        ImGui.SameLine();
        ImGui.Text("Y");
        ImGui.SameLine();
        changed |= GUIHelper.DragDouble("##Y", ref v2.y, 0.01f);

        ImGui.PopItemWidth();
        ImGui.Columns(1);
        return changed;
    }

}
