using LevelEditor.Editor.Connections;
using LevelEditor.Editor.Placement;
using LevelEditor.Editor.UI;
using LevelEditor.Runtime.Core;
using LevelEditor.Runtime.Data;
using UnityEditor;
using UnityEngine;

namespace LevelEditor.Editor.ContextMenu
{
    /// <summary>
    /// SceneView 右键菜单系统。
    ///
    /// 使用 EventType.ContextClick：
    ///   Unity 在右键单击（无拖拽）松开后才发出此事件，此时旋转状态机已清理完毕，
    ///   光标已恢复，且处于 GUI 帧内可安全调用 PickGameObject。
    ///   右键拖拽旋转视角时不会触发此事件，两者天然不冲突。
    /// </summary>
    public static class SceneContextMenu
    {
        public static void OnSceneGui(SceneView sceneView)
        {
            if (ScenePlacementTool.IsPlacing) return;

            var evt = Event.current;
            if (evt.type != EventType.ContextClick) return;

            // ContextClick 处于 GUI 帧内，可直接调用 PickGameObject
            GameObject   picked = HandleUtility.PickGameObject(evt.mousePosition, false);
            PlacedObject po     = null;
            if (picked != null)
            {
                po = picked.GetComponent<PlacedObject>();
                if (po == null) po = picked.GetComponentInParent<PlacedObject>();
                if (po == null) po = picked.GetComponentInChildren<PlacedObject>();
            }

            var menu = new GenericMenu();
            if (po != null)
                BuildObjectMenu(menu, po, evt.mousePosition);
            else
                BuildPlaceMenu(menu);

            menu.ShowAsContext();
            evt.Use();
        }

        // ── 对象操作菜单 ──────────────────────────────────────────────────────

        private static void BuildObjectMenu(GenericMenu menu, PlacedObject po, Vector2 mousePos)
        {
            Rect mouseRect = new Rect(GUIUtility.GUIToScreenPoint(mousePos), Vector2.zero);

            menu.AddItem(new GUIContent("调整参数"), false, () =>
            {
                PopupWindow.Show(mouseRect, new SceneParamPopup(po.gameObject));
            });

            menu.AddItem(new GUIContent("管理引用"), false, () =>
            {
                RefDragHandler.EnterConnectionMode(po);
            });

            menu.AddSeparator(string.Empty);

            menu.AddItem(new GUIContent("删除对象"), false, () =>
            {
                Undo.DestroyObjectImmediate(po.gameObject);
            });

            menu.AddSeparator(string.Empty);

            menu.AddItem(new GUIContent("── 打开预制体面板"), false,
                PrefabPaletteWindow.ShowWindow);
        }

        // ── 放置菜单 ──────────────────────────────────────────────────────────

        private static void BuildPlaceMenu(GenericMenu menu)
        {
            var registry = LoadRegistry();

            if (registry != null && registry.Entries.Length > 0)
            {
                foreach (var entry in registry.Entries)
                {
                    if (entry?.prefab == null) continue;

                    string category = string.IsNullOrEmpty(entry.category) ? "未分类" : entry.category;
                    var captured = entry;
                    menu.AddItem(new GUIContent($"{category}/{entry.key}"), false, () =>
                    {
                        ScenePlacementTool.BeginPlace(captured);
                    });
                }
                menu.AddSeparator(string.Empty);
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("（未配置预制体注册表）"));
                menu.AddSeparator(string.Empty);
            }

            menu.AddItem(new GUIContent("── 打开预制体面板"), false,
                PrefabPaletteWindow.ShowWindow);
        }

        // ── 工具 ──────────────────────────────────────────────────────────────

        private static LevelEditorPrefabRegistry LoadRegistry()
        {
            var reg = AssetDatabase.LoadAssetAtPath<LevelEditorPrefabRegistry>(
                "Assets/LevelEditor/Data/LevelEditorPrefabRegistry.asset");

            if (reg == null)
            {
                var guids = AssetDatabase.FindAssets("t:LevelEditorPrefabRegistry");
                if (guids.Length > 0)
                    reg = AssetDatabase.LoadAssetAtPath<LevelEditorPrefabRegistry>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            return reg;
        }
    }
}
