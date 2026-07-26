using LevelEditor.Editor.Placement;
using LevelEditor.Runtime.Data;
using UnityEditor;
using UnityEngine;

namespace LevelEditor.Editor.UI
{
    /// <summary>
    /// 极简预制体浏览面板（EditorWindow）。
    /// 提供搜索框、分类 Toolbar、图标网格，点击条目进入 Ghost 放置模式。
    /// 菜单：Tools/关卡编辑器/预制体面板（Ctrl+Shift+P）
    /// </summary>
    public sealed class PrefabPaletteWindow : EditorWindow
    {
        // ── 状态 ──────────────────────────────────────────────────────────────

        private LevelEditorPrefabRegistry m_Registry;
        private string                    m_SearchText   = string.Empty;
        private int                       m_CategoryIndex = 0;
        private string[]                  m_Categories;
        private Vector2                   m_Scroll;

        private const float k_WindowWidth   = 230f;
        private const float k_IconSize      = 64f;
        private const float k_IconSpacing   = 4f;   // 图标间距

        // ── 静态入口 ──────────────────────────────────────────────────────────

        [MenuItem("Tools/关卡编辑器/预制体面板 %#p")]
        public static void ShowWindow()
        {
            var win = GetWindow<PrefabPaletteWindow>("预制体面板");
            win.minSize = new Vector2(k_WindowWidth, 200f);
            win.Show();
        }

        // ── Unity 回调 ────────────────────────────────────────────────────────

        private void OnEnable()
        {
            LoadRegistry();
            BuildCategoryList();
        }

        private void OnGUI()
        {
            if (m_Registry == null)
            {
                EditorGUILayout.HelpBox("未找到预制体注册表。\n请在 Assets/LevelEditor/Data/ 下创建 LevelEditorPrefabRegistry.asset。", MessageType.Warning);
                if (GUILayout.Button("刷新"))
                {
                    LoadRegistry();
                    BuildCategoryList();
                }
                return;
            }

            DrawToolbar();
            EditorGUILayout.Space(4f);
            DrawGrid();
        }

        // ── 绘制 ──────────────────────────────────────────────────────────────

        private void DrawToolbar()
        {
            // 搜索框
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            m_SearchText = EditorGUILayout.TextField(m_SearchText, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20f)))
                m_SearchText = string.Empty;
            EditorGUILayout.EndHorizontal();

            // 分类 Toolbar
            if (m_Categories != null && m_Categories.Length > 1)
            {
                m_CategoryIndex = GUILayout.Toolbar(
                    m_CategoryIndex, m_Categories,
                    EditorStyles.toolbarButton);
            }
        }

        private void DrawGrid()
        {
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

            var entries = FilterEntries();

            // 根据当前窗口宽度动态计算每行列数
            float availableWidth = position.width - 20f; // 减去滚动条宽度
            int iconsPerRow = Mathf.Max(1,
                Mathf.FloorToInt(availableWidth / (k_IconSize + k_IconSpacing)));

            int col = 0;
            EditorGUILayout.BeginHorizontal();
            foreach (var entry in entries)
            {
                if (col == iconsPerRow)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    col = 0;
                }

                DrawEntryButton(entry);
                col++;
            }
            // 末行补空白，保持对齐
            if (col > 0)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            else if (entries.Count == 0)
            {
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawEntryButton(PrefabEntry entry)
        {
            GUIContent content = entry.icon != null
                ? new GUIContent(entry.icon, entry.key)
                : new GUIContent(entry.key);

            if (GUILayout.Button(content,
                GUILayout.Width(k_IconSize),
                GUILayout.Height(k_IconSize)))
            {
                ScenePlacementTool.BeginPlace(entry);
                // 自动聚焦 SceneView
                SceneView.lastActiveSceneView?.Focus();
            }

            // 图标下方显示名字
            GUILayout.Label(entry.key,
                EditorStyles.centeredGreyMiniLabel,
                GUILayout.Width(k_IconSize));
        }

        // ── 工具 ──────────────────────────────────────────────────────────────

        private System.Collections.Generic.List<PrefabEntry> FilterEntries()
        {
            var result = new System.Collections.Generic.List<PrefabEntry>();
            string currentCategory = (m_Categories != null && m_CategoryIndex > 0)
                ? m_Categories[m_CategoryIndex]
                : null; // index 0 = "全部"

            foreach (var entry in m_Registry.Entries)
            {
                if (entry?.prefab == null) continue;

                // 分类过滤
                if (currentCategory != null && entry.category != currentCategory) continue;

                // 搜索过滤（key 包含搜索文本）
                if (!string.IsNullOrEmpty(m_SearchText) &&
                    !entry.key.ToLower().Contains(m_SearchText.ToLower())) continue;

                result.Add(entry);
            }

            return result;
        }

        private void LoadRegistry()
        {
            m_Registry = AssetDatabase.LoadAssetAtPath<LevelEditorPrefabRegistry>(
                "Assets/LevelEditor/Data/LevelEditorPrefabRegistry.asset");

            if (m_Registry == null)
            {
                var guids = AssetDatabase.FindAssets("t:LevelEditorPrefabRegistry");
                if (guids.Length > 0)
                    m_Registry = AssetDatabase.LoadAssetAtPath<LevelEditorPrefabRegistry>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }

        private void BuildCategoryList()
        {
            if (m_Registry == null) { m_Categories = new[] { "全部" }; return; }

            var cats = new System.Collections.Generic.List<string> { "全部" };
            foreach (var entry in m_Registry.Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.category)) continue;
                if (!cats.Contains(entry.category)) cats.Add(entry.category);
            }
            m_Categories = cats.ToArray();
        }
    }
}
