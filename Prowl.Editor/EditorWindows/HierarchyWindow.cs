using HexaEngine.ImGuiNET;
using Prowl.Icons;
using Prowl.Runtime;
using Prowl.Editor.ImGUI.Widgets;
using Prowl.Runtime.SceneManagement;
using Silk.NET.Input;
using static Prowl.Editor.EditorConfiguration;

namespace Prowl.Editor.EditorWindows;

public class HierarchyWindow : EditorWindow
{
    protected override ImGuiWindowFlags Flags => ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse;

    public HierarchyWindow() : base()
    {
        Title = FontAwesome6.FolderTree + " Hierarchy";
        SelectHandler.OnSelectObject += (obj) => {
            // Reset ping timer on selection changed
            pingTimer = 0;
            pingedGO = null;
        };
    }

    private string _searchText = "";
    private List<GameObject> m_RenamingEntities = null;
    public static SelectHandler<WeakReference> SelectHandler { get; private set; } = new((item) => !item.IsAlive || (item.Target is EngineObject eObj && eObj.IsDestroyed), (a, b) => ReferenceEquals(a.Target, b.Target));

    private const float PingDuration = 3f;
    private static float pingTimer = 0;
    private static WeakReference pingedGO;

    public static void Ping(GameObject go)
    {
        pingTimer = PingDuration;
        pingedGO = new WeakReference(go);
    }

    protected override void Draw()
    {
        pingTimer -= Time.deltaTimeF;
        if (pingTimer < 0) pingTimer = 0;

        SelectHandler.StartFrame();

        ImGuiTableFlags tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.ContextMenuInBody | ImGuiTableFlags.BordersInner | ImGuiTableFlags.ScrollY;

        float lineHeight = ImGui.GetTextLineHeight();
        float contentWidth = ImGui.GetContentRegionAvail().X;
        const float addButtonSize = 48f;
        System.Numerics.Vector2 padding = ImGui.GetStyle().FramePadding;

        float filterCursorPosX = ImGui.GetCursorPosX();
        GUIHelper.Search("##searchBox", ref _searchText, contentWidth - addButtonSize - 7);

        ImGui.SameLine();

        ImGui.SetCursorPosX(contentWidth - addButtonSize);
        if (ImGui.Button("  " + FontAwesome6.Plus + " Add  "))
            ImGui.OpenPopup("SceneHierarchyContextWindow");

        if (string.IsNullOrEmpty(_searchText)) {
            ImGui.SameLine();
            ImGui.SetCursorPosX(filterCursorPosX + ImGui.GetFontSize() * 0.5f);
            ImGui.TextUnformatted(FontAwesome6.MagnifyingGlass + " Search...");
        }

        if (ImGui.BeginTable("HierarchyTable", 4, tableFlags)) {
            if (ImGui.BeginPopupContextWindow("SceneHierarchyContextWindow", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems)) {
                DrawContextMenu();
                ImGui.EndPopup();
            }

            ImGui.TableSetupColumn("  Label", ImGuiTableColumnFlags.NoHide | ImGuiTableColumnFlags.NoClip, contentWidth);
            ImGui.TableSetupColumn(" " + FontAwesome6.Tag, ImGuiTableColumnFlags.WidthFixed, lineHeight * 1.0f);
            ImGui.TableSetupColumn(" " + FontAwesome6.LayerGroup, ImGuiTableColumnFlags.WidthFixed, lineHeight * 1.0f);
            ImGui.TableSetupColumn(" " + FontAwesome6.Eye, ImGuiTableColumnFlags.WidthFixed, lineHeight * 1.0f);

            ImGui.TableSetupScrollFreeze(0, 1);

            ImGui.TableNextRow(ImGuiTableRowFlags.Headers, ImGui.GetFrameHeight());
            for (int column = 0; column < 4; ++column) {
                ImGui.TableSetColumnIndex(column);
                string columnName = ImGui.TableGetColumnNameS(column);
                ImGui.PushID(column);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + padding.Y);
                ImGui.TableHeader(columnName);
                ImGui.PopID();
            }

            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(0f, 0f));
            int id = 0;
            for (int i = 0; i < SceneManager.AllGameObjects.Count; i++) {
                var go = SceneManager.AllGameObjects[i];
                if (go.parent == null)
                    DrawEntityNode(ref id, go, 0, false);
            }
            ImGui.PopStyleVar(2);

            if (ImGui.BeginPopup("RenameGameObjects")) {
                ImGui.SetKeyboardFocusHere();
                string name = m_RenamingEntities[0].Name;
                if (ImGui.InputText("##Tag", ref name, 0x100)) {
                    m_RenamingEntities.ForEach((go) => { go.Name = name; });
                }
                ImGui.EndPopup();
            } else {
                m_RenamingEntities = null;
            }

            ImGui.EndTable();

            if (!SelectHandler.SelectedThisFrame && ImGui.IsItemClicked(0))
                SelectHandler.Clear();

            if (EditorApplication.IsHotkeyDown("Duplicate", new Hotkey() { Key = Key.D, Ctrl = true }))
                DuplicateSelected();

            HandleDragnDrop(null); // Into window
        }
    }

    void DrawEntityNode(ref int index, GameObject entity, uint depth, bool isPartOfPrefab)
    {
        if (entity == null) return;
        if (entity.hideFlags.HasFlag(HideFlags.Hide) || entity.hideFlags.HasFlag(HideFlags.HideAndDontSave)) return;

        if (!string.IsNullOrEmpty(_searchText) && !entity.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)) {
            for (int i = 0; i < entity.children.Count; i++)
                DrawEntityNode(ref index, entity.children[i], depth, isPartOfPrefab);
            return;
        }

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        bool isPrefab = entity.AssetID != Guid.Empty;
        bool isSelected = SelectHandler.IsSelected(new WeakReference(entity));

        ImGuiTreeNodeFlags flags = CalculateFlags(entity, isSelected);

        int colPushCount = 0;
        if (isSelected) {
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(ImGuiCol.HeaderActive));
            ImGui.PushStyleColor(ImGuiCol.Header, ImGui.GetColorU32(ImGuiCol.HeaderActive));
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, ImGui.GetColorU32(ImGuiCol.HeaderActive));
            colPushCount += 2;
        } else if (isPrefab)
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new System.Numerics.Vector4(0.3f, 0.0f, 0.3f, 1.0f)));
        else if (isPartOfPrefab)
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new System.Numerics.Vector4(0.3f, 0.0f, 0.3f, 0.5f)));

        if (entity.enabledInHierarchy == false) {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
            colPushCount += 1;
        }

        // if were pinging we need to open the tree to the pinged object
        bool isPingedEntity = false;
        if (pingTimer > 0 && pingedGO != null && pingedGO.Target is GameObject go)
        {
            if (entity.IsParentOf(go))
                ImGui.SetNextItemOpen(true); // Open down to the pinged object
            else if (entity.InstanceID == go.InstanceID)
                isPingedEntity = true; // This is the pinged entity
        }

        var opened = ImGui.TreeNodeEx(entity.Name + "##" + entity.InstanceID, flags);
        ImGui.PopStyleColor(colPushCount);

        if (isPingedEntity)
        {
            if (pingTimer > PingDuration - 1f)
                ImGui.ScrollToItem(ImGuiScrollFlags.None);
            GUIHelper.ItemRect(1f, 0.8f, 0.0f, 0.8f, MathF.Sin(pingTimer) * 1f, 3f, 2.5f);
            GUIHelper.ItemRect(1f, 0.8f, 0.0f, 0.8f, MathF.Sin(pingTimer) * 6f, 3f, 2.5f);
        }

        // Select
        if (!ImGui.IsItemToggledOpen())
        {
            SelectHandler.HandleSelectable(index++, new WeakReference(entity));
            if (SelectHandler.Count == 1 && ImGui.IsMouseDoubleClicked(0) && ImGui.IsItemHovered())
            {
                m_RenamingEntities = [entity];
                ImGui.OpenPopup("RenameGameObjects");
            }
        }

        DrawGameObjectContextMenu(entity);

        // Drag Drop
        HandleDragnDrop(entity);

        ImGui.TableNextColumn();
        DrawTagIcon(entity);
        ImGui.TableNextColumn();
        DrawLayerIcon(entity);
        ImGui.TableNextColumn();
        DrawVisibilityToggle(entity);

        // Open
        if (opened)
        {
            for (int i = 0; i < entity.children.Count; i++)
                DrawEntityNode(ref index, entity.children[i], depth + 1, isPartOfPrefab || isPrefab);

            if (opened && entity.children.Count > 0)
                ImGui.TreePop();
        }
    }

    private static ImGuiTreeNodeFlags CalculateFlags(GameObject entity, bool isSelected)
    {
        return (isSelected ? ImGuiTreeNodeFlags.Selected : ImGuiTreeNodeFlags.None)
            | ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanFullWidth | ImGuiTreeNodeFlags.FramePadding
            | (entity.children.Count == 0 ? ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen : 0);
    }

    private void HandleDragnDrop(GameObject? entity)
    {
        // GameObject from Hierarchy
        if (DragnDrop.ReceiveReference<GameObject>(out var go)) {
            go.SetParent(entity); // null is root
            SelectHandler.SetSelection(new WeakReference(go));
        }
        // GameObject from Assets
        if (DragnDrop.ReceiveAsset<GameObject>(out var original)) {
            GameObject clone = (GameObject)EngineObject.Instantiate(original.Res!, true);
            clone.AssetID = Guid.Empty; // Remove AssetID so it's not a Prefab - These are just GameObjects like Models
            clone.SetParent(entity); // null is root
            SelectHandler.SetSelection(new WeakReference(clone));
        }

        // Prefab from Assets
        if (DragnDrop.ReceiveAsset<Prefab>(out var prefab))
            SelectHandler.SetSelection(new WeakReference(prefab.Res.Instantiate()));

        // Scene from Assets
        if (DragnDrop.ReceiveAsset<Scene>(out var scene))
            SceneManager.LoadScene(scene);

        // Offer GameObject up from Hierarchy for Drag And Drop
        if (entity != null) DragnDrop.OfferReference(entity);
    }

    private static void DrawTagIcon(GameObject entity)
    {
        ImGui.Text(" " + FontAwesome6.Tag);
        if (ImGui.IsItemHovered()) {
            GUIHelper.ItemRectFilled(1f, 1f, 1f, 0.25f);
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                ImGui.OpenPopup("GameObjecTags##" + entity.InstanceID);
        }
        GUIHelper.Tooltip("Tag: " + TagLayerManager.tags[entity.tagIndex]);

        if (ImGui.BeginPopup("GameObjecTags##" + entity.InstanceID)) {
            ImGui.Combo("##Tag", ref entity.tagIndex, TagLayerManager.tags.ToArray(), TagLayerManager.tags.Count);
            ImGui.EndPopup();
        }
    }

    private static void DrawLayerIcon(GameObject entity)
    {
        ImGui.Text(" " + FontAwesome6.LayerGroup);
        if (ImGui.IsItemHovered()) {
            GUIHelper.ItemRectFilled(1f, 1f, 1f, 0.25f);
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                ImGui.OpenPopup("GameObjectLayers##" + entity.InstanceID);
        }
        GUIHelper.Tooltip("Layer: " + TagLayerManager.layers[entity.layerIndex]);

        if (ImGui.BeginPopup("GameObjectLayers##" + entity.InstanceID)) {
            ImGui.Combo("##Layers", ref entity.layerIndex, TagLayerManager.layers.ToArray(), TagLayerManager.layers.Count);
            ImGui.EndPopup();
        }
    }

    private static void DrawVisibilityToggle(GameObject entity)
    {
        ImGui.Text(" " + (entity.enabled ? FontAwesome6.Eye : FontAwesome6.EyeSlash));
        if (ImGui.IsItemHovered()) {
            GUIHelper.ItemRectFilled(1f, 1f, 1f, 0.25f);
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                entity.enabled = !entity.enabled;
        }
        GUIHelper.Tooltip("Visibility: " + (entity.enabled ? "Visible" : "Hidden"));
    }

    void DrawContextMenu(GameObject context = null)
    {
        if (ImGui.MenuItem("New Gameobject")) {
            GameObject go = new GameObject("New Gameobject");
            if (context != null)
                go.SetParent(context);
            SelectHandler.SetSelection(new WeakReference(go));
            ImGui.CloseCurrentPopup();
        }

        MenuItem.DrawMenuRoot("Template");
    }

    void DrawGameObjectContextMenu(GameObject entity)
    {
        if (ImGui.BeginPopupContextItem()) {
            SelectHandler.SelectIfNot(new WeakReference(entity));

            DrawContextMenu(entity);

            ImGui.Separator();

            if (ImGui.MenuItem("Rename")) {
                m_RenamingEntities = new();
                SelectHandler.Foreach((go) => {
                    m_RenamingEntities.Add(go.Target as GameObject);
                });
#warning This rename looks like it fails because of https://github.com/ocornut/imgui/issues/6462
                ImGui.OpenPopup("RenameGameObjects");
            }

            if (ImGui.MenuItem("Duplicate"))
                DuplicateSelected();

            if (ImGui.MenuItem("Delete", "Del"))
                entity.Destroy();

            if (SelectHandler.Count > 0 && ImGui.MenuItem("Delete All")) {
                SelectHandler.Foreach((go) => {
                    (go.Target as GameObject).Destroy();
                });
                SelectHandler.Clear();
            }

            ImGui.Separator();

            if (SelectHandler.Count > 0 && ImGui.MenuItem("Align With View")) {
                SelectHandler.Foreach((go) => {
                    Camera cam = ViewportWindow.LastFocusedCamera;
                    (go.Target as GameObject).transform.position = cam.GameObject.transform.position;
                    (go.Target as GameObject).transform.rotation = cam.GameObject.transform.rotation;
                });
            }

            if (SelectHandler.Count == 1 && ImGui.MenuItem("Align View With")) {
                Camera cam = ViewportWindow.LastFocusedCamera;
                cam.GameObject.transform.position = entity.transform.position;
                cam.GameObject.transform.rotation = entity.transform.rotation;
            }

            ImGui.EndPopup();
        }
    }

    public void DuplicateSelected()
    {
        var newGO = new List<WeakReference>();
        SelectHandler.Foreach((go) => {
            // Duplicating, Easiest way to duplicate is to Serialize then Deserialize
            var serialized = TagSerializer.Serialize(go.Target);
            var deserialized = TagSerializer.Deserialize<GameObject>(serialized);
            deserialized.SetParent((go.Target as GameObject).parent);
            newGO.Add(new WeakReference(deserialized));
        });
        SelectHandler.Clear();
        SelectHandler.SetSelection(newGO.ToArray());
    }
}