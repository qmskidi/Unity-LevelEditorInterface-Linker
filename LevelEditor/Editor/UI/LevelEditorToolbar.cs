using LevelEditor.Editor.Core;
using LevelEditor.Editor.SaveLoad;
using UnityEditor;
using UnityEngine;

namespace LevelEditor.Editor.UI
{
    /// <summary>
    /// SceneView 右上角浮动工具条。
    /// 在 LevelEditorOrchestrator 的 duringSceneGui 回调内，由 OnSceneGui() 绘制。
    /// 布局：[🟢/🔴 开关] [格点:1.0] [▦格点] | [💾 保存] [📂 加载]
    ///
    /// 所有会打断 GUI 帧的操作（文件对话框、新建 EditorWindow）均通过
    /// EditorApplication.delayCall 延迟到当前 GUI 帧结束后执行。
    /// </summary>
    public static class LevelEditorToolbar
    {
        private const float k_ToolbarWidth  = 380f;
        private const float k_ToolbarHeight = 26f;
        private const float k_Margin        = 8f;

        private static string s_SaveName = "Level_01";

        // ── SceneGui 入口 ────────────────────────────────────────────────────

        public static void OnSceneGui(SceneView sceneView)
        {
            Handles.BeginGUI();
            try
            {
                DrawToolbar(sceneView);
            }
            finally
            {
                Handles.EndGUI();
            }
        }

        // ── 绘制 ─────────────────────────────────────────────────────────────

        private static void DrawToolbar(SceneView sceneView)
        {
            float svWidth = sceneView.position.width;

            Rect toolbarRect = new Rect(
                svWidth - k_ToolbarWidth - k_Margin,
                k_Margin,
                k_ToolbarWidth,
                k_ToolbarHeight);

            GUILayout.BeginArea(toolbarRect, EditorStyles.toolbar);
            GUILayout.BeginHorizontal();
            try
            {
                DrawEnableToggle();
                GUILayout.Space(4f);
                DrawCellSizeField();
                GUILayout.Space(2f);
                DrawGridToggle();
                DrawSeparator();
                DrawSaveButton();
                GUILayout.Space(2f);
                DrawLoadButton();
            }
            finally
            {
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }
        }

        // ── 子控件 ───────────────────────────────────────────────────────────

        private static void DrawEnableToggle()
        {
            bool isEnabled = LevelEditorOrchestrator.IsEnabled;

            Color prev = GUI.color;
            GUI.color = isEnabled
                ? new Color(0.6f, 1.0f, 0.6f)
                : new Color(1.0f, 0.6f, 0.6f);

            string label = isEnabled ? "🟢 已开启" : "🔴 已关闭";
            if (GUILayout.Button(label, EditorStyles.toolbarButton, GUILayout.Width(78f)))
                LevelEditorOrchestrator.IsEnabled = !LevelEditorOrchestrator.IsEnabled;

            GUI.color = prev;
        }

        private static void DrawCellSizeField()
        {
            var settings = LevelEditorSettings.Instance;
            if (settings == null)
            {
                GUILayout.Label("格点:N/A", EditorStyles.miniLabel, GUILayout.Width(60f));
                return;
            }

            GUILayout.Label("格点:", EditorStyles.miniLabel, GUILayout.Width(32f));

            EditorGUI.BeginChangeCheck();
            float newSize = EditorGUILayout.FloatField(
                settings.cellSize,
                EditorStyles.toolbarTextField,
                GUILayout.Width(36f));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(settings, "修改格点大小");
                settings.cellSize = Mathf.Max(0.1f, newSize);
                EditorUtility.SetDirty(settings);
            }
        }

        private static void DrawGridToggle()
        {
            var settings = LevelEditorSettings.Instance;
            if (settings == null) return;

            EditorGUI.BeginChangeCheck();
            bool newShow = GUILayout.Toggle(
                settings.showGrid, "▦ 格点",
                EditorStyles.toolbarButton,
                GUILayout.Width(52f));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(settings, "切换格点显示");
                settings.showGrid = newShow;
                EditorUtility.SetDirty(settings);
                SceneView.RepaintAll();
            }
        }

        private static void DrawSeparator()
        {
            GUILayout.Label("|", EditorStyles.miniLabel, GUILayout.Width(8f));
        }

        private static void DrawSaveButton()
        {
            if (GUILayout.Button("💾 保存", EditorStyles.toolbarButton, GUILayout.Width(56f)))
            {
                // 延迟到 GUI 帧结束后再打开对话框，避免打断 BeginGUI/EndGUI
                string nameCapture = s_SaveName;
                EditorApplication.delayCall += () =>
                {
                    SaveDialogWindow.Show(nameCapture, name =>
                    {
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            s_SaveName = name;
                            LevelSaveLoadEditor.SaveLevel(name);
                        }
                    });
                };
            }
        }

        private static void DrawLoadButton()
        {
            if (GUILayout.Button("📂 加载", EditorStyles.toolbarButton, GUILayout.Width(56f)))
            {
                // 延迟到 GUI 帧结束后再打开文件对话框
                EditorApplication.delayCall += () =>
                {
                    string path = EditorUtility.OpenFilePanel(
                        "加载关卡", "Assets/Levels", "json");
                    if (!string.IsNullOrEmpty(path))
                        LevelSaveLoadEditor.LoadLevel(path);
                };
            }
        }
    }

    // ── 保存名称输入弹窗 ─────────────────────────────────────────────────────

    /// <summary>
    /// 单行文本输入弹窗，用于保存关卡时输入文件名。
    /// 通过 EditorApplication.delayCall 延迟创建，保证不在 GUI 帧内调用。
    /// </summary>
    internal sealed class SaveDialogWindow : EditorWindow
    {
        private string                m_Name;
        private System.Action<string> m_Callback;
        private bool                  m_FocusDone;

        public static void Show(string defaultName, System.Action<string> onConfirm)
        {
            var win = CreateInstance<SaveDialogWindow>();
            win.m_Name       = defaultName;
            win.m_Callback   = onConfirm;
            win.titleContent = new GUIContent("保存关卡");
            win.minSize      = win.maxSize = new Vector2(280f, 76f);
            win.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6f);
            GUILayout.Label("关卡文件名（不含扩展名）：", EditorStyles.miniLabel);

            GUI.SetNextControlName("LevelNameField");
            m_Name = EditorGUILayout.TextField(m_Name);

            if (!m_FocusDone)
            {
                EditorGUI.FocusTextInControl("LevelNameField");
                m_FocusDone = true;
            }

            EditorGUILayout.Space(4f);
            GUILayout.BeginHorizontal();

            bool confirm = GUILayout.Button("确认保存");
            bool cancel  = GUILayout.Button("取消");

            GUILayout.EndHorizontal();

            // Enter / Escape 快捷键在 Layout 事件之外处理，避免嵌套问题
            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Return ||
                    Event.current.keyCode == KeyCode.KeypadEnter)
                    confirm = true;
                else if (Event.current.keyCode == KeyCode.Escape)
                    cancel = true;
            }

            if (confirm) { m_Callback?.Invoke(m_Name); Close(); }
            if (cancel)  { Close(); }
        }
    }
}
