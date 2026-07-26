using LevelEditor.Editor.Connections;
using LevelEditor.Editor.Placement;
using LevelEditor.Runtime.Core;
using LevelEditor.Runtime.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LevelEditor.Editor.UI
{
    /// <summary>
    /// SceneView 左下角常驻可折叠操作面板。
    /// 替代右键菜单，提供放置预制体 / 选中对象操作两个区域。
    /// 由 LevelEditorOrchestrator 在 duringSceneGui 中调用。
    /// </summary>
    public static class LevelEditorSidePanel
    {
        private const string k_FoldPlacePref  = "LevelEditor.SidePanel.FoldPlace";
        private const string k_FoldObjPref    = "LevelEditor.SidePanel.FoldObj";
        private const float  k_PanelWidth     = 200f;
        private const float  k_MarginBottom   = 28f;  // 避开 SceneView 底部状态栏
        private const float  k_MarginLeft     = 8f;

        private static bool s_FoldPlace = false;
        private static bool s_FoldObj   = false;
        private static Vector2 s_PlaceScroll;

        private static LevelEditorPrefabRegistry s_Registry;

        // ── 入口 ─────────────────────────────────────────────────────────────

        public static void OnSceneGui(SceneView sceneView)
        {
            // 确保注册表已加载
            if (s_Registry == null) LoadRegistry();

            Handles.BeginGUI();
            try
            {
                DrawPanel(sceneView);
            }
            finally
            {
                Handles.EndGUI();
            }
        }

        // ── 面板绘制 ──────────────────────────────────────────────────────────

        private static void DrawPanel(SceneView sceneView)
        {
            // 先计算内容高度，再确定面板起始 Y
            float placeHeight = s_FoldPlace ? 0f : CalcPlaceHeight();
            float objHeight   = s_FoldObj   ? 0f : CalcObjHeight();
            float headerPlace = 22f;
            float headerObj   = 22f;
            float totalH      = headerPlace + placeHeight + headerObj + objHeight + 8f;

            float svH  = sceneView.position.height;
            float posY = svH - k_MarginBottom - totalH;
            posY = Mathf.Max(4f, posY);

            Rect panelRect = new Rect(k_MarginLeft, posY, k_PanelWidth, totalH);
            GUI.Box(panelRect, GUIContent.none, EditorStyles.helpBox);

            GUILayout.BeginArea(panelRect);
            try
            {
                DrawPlaceSection(placeHeight);
                DrawObjSection(objHeight);
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        // ── 放置区域 ──────────────────────────────────────────────────────────

        private static void DrawPlaceSection(float contentHeight)
        {
            // 折叠标题
            if (GUILayout.Button(
                    (s_FoldPlace ? "▶ " : "▼ ") + "放置预制体",
                    EditorStyles.boldLabel,
                    GUILayout.Height(20f)))
            {
                s_FoldPlace = !s_FoldPlace;
                EditorPrefs.SetBool(k_FoldPlacePref, s_FoldPlace);
            }

            if (s_FoldPlace) return;

            s_PlaceScroll = GUILayout.BeginScrollView(
                s_PlaceScroll,
                GUILayout.Height(contentHeight));

            if (s_Registry != null && s_Registry.Entries.Length > 0)
            {
                string lastCat = null;
                foreach (var entry in s_Registry.Entries)
                {
                    if (entry?.prefab == null) continue;

                    string cat = string.IsNullOrEmpty(entry.category) ? "未分类" : entry.category;
                    if (cat != lastCat)
                    {
                        EditorGUILayout.LabelField(cat, EditorStyles.miniLabel);
                        lastCat = cat;
                    }

                    var captured = entry;
                    if (GUILayout.Button(entry.key, GUILayout.Height(20f)))
                        ScenePlacementTool.BeginPlace(captured);
                }
            }
            else
            {
                GUILayout.Label("未配置注册表", EditorStyles.centeredGreyMiniLabel);
            }

            if (GUILayout.Button("打开预制体面板…", EditorStyles.miniButton))
                PrefabPaletteWindow.ShowWindow();

            GUILayout.EndScrollView();
        }

        // ── 对象操作区域 ──────────────────────────────────────────────────────

        private static void DrawObjSection(float contentHeight)
        {
            var go = Selection.activeGameObject;
            var po = go != null ? go.GetComponent<PlacedObject>() : null;

            string title = po != null ? $"▼ {go.name}" : "▼ 选中对象操作";
            if (s_FoldObj)
                title = title.Replace("▼", "▶");

            if (GUILayout.Button(title, EditorStyles.boldLabel, GUILayout.Height(20f)))
            {
                s_FoldObj = !s_FoldObj;
                EditorPrefs.SetBool(k_FoldObjPref, s_FoldObj);
            }

            if (s_FoldObj) return;

            if (po == null)
            {
                GUILayout.Label("未选中 PlacedObject", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            // 调整参数
            if (GUILayout.Button("调整参数", GUILayout.Height(22f)))
            {
                // 在面板左侧弹出 PopupWindow
                Rect r = GUILayoutUtility.GetLastRect();
                r = GUIUtility.GUIToScreenRect(
                    new Rect(k_MarginLeft + k_PanelWidth + 4f, r.y, 0f, 0f));
                PopupWindow.Show(r, new ContextMenu.SceneParamPopup(go));
            }

            // 管理引用
            if (GUILayout.Button("管理引用", GUILayout.Height(22f)))
                RefDragHandler.EnterConnectionMode(po);

            GUILayout.Space(4f);

            // 删除
            GUI.color = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("删除对象", GUILayout.Height(22f)))
            {
                Undo.DestroyObjectImmediate(go);
                EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }
            GUI.color = Color.white;
        }

        // ── 高度估算 ──────────────────────────────────────────────────────────

        private static float CalcPlaceHeight()
        {
            if (s_Registry == null || s_Registry.Entries.Length == 0)
                return 40f;

            int count = 0;
            foreach (var e in s_Registry.Entries)
                if (e?.prefab != null) count++;

            // 每条目 20px + 分类标题若干 + 面板按钮
            return Mathf.Clamp(count * 22f + 30f, 40f, 180f);
        }

        private static float CalcObjHeight()
        {
            var go = Selection.activeGameObject;
            if (go == null || go.GetComponent<PlacedObject>() == null)
                return 24f;
            return 80f; // 三个按钮
        }

        // ── 注册表加载 ────────────────────────────────────────────────────────

        private static void LoadRegistry()
        {
            s_Registry = AssetDatabase.LoadAssetAtPath<LevelEditorPrefabRegistry>(
                "Assets/LevelEditor/Data/LevelEditorPrefabRegistry.asset");

            if (s_Registry == null)
            {
                var guids = AssetDatabase.FindAssets("t:LevelEditorPrefabRegistry");
                if (guids.Length > 0)
                    s_Registry = AssetDatabase.LoadAssetAtPath<LevelEditorPrefabRegistry>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }
    }
}
