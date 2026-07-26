using LevelEditor.Editor.Connections;
using LevelEditor.Editor.Placement;
using LevelEditor.Editor.UI;
using UnityEditor;
using UnityEngine;

namespace LevelEditor.Editor.Core
{
    /// <summary>
    /// 关卡编辑器总线（静态类，[InitializeOnLoad]）。
    ///
    /// 职责：
    ///   1. 持有全局 IsEnabled 开关（EditorPrefs 持久化）
    ///   2. 随开关注册 / 注销 SceneView.duringSceneGui
    ///   3. OnSceneGui 中按顺序调用所有子系统
    ///   4. 提供菜单项 Tools/关卡编辑器/启用（Ctrl+Shift+E）
    /// </summary>
    [InitializeOnLoad]
    public static class LevelEditorOrchestrator
    {
        private const string k_PrefKey       = "LevelEditor.IsEnabled";
        private const string k_MenuItemPath  = "Tools/关卡编辑器/启用 %#e";

        // ── 初始化（每次 Unity 启动 / Domain Reload 后执行）──────────────────

        static LevelEditorOrchestrator()
        {
            // 从 EditorPrefs 恢复上次状态（不使用 IsEnabled setter 以避免重复注册）
            if (EditorPrefs.GetBool(k_PrefKey, false))
                RegisterCallbacks();

            // 确保菜单项 checkmark 正确显示
            EditorApplication.delayCall += SyncMenuCheckmark;
        }

        // ── IsEnabled 属性 ────────────────────────────────────────────────────

        public static bool IsEnabled
        {
            get => EditorPrefs.GetBool(k_PrefKey, false);
            set
            {
                bool current = EditorPrefs.GetBool(k_PrefKey, false);
                if (current == value) return;

                EditorPrefs.SetBool(k_PrefKey, value);

                if (value)
                    RegisterCallbacks();
                else
                    UnregisterCallbacks();

                SyncMenuCheckmark();
                SceneView.RepaintAll();
            }
        }

        // ── 菜单项 ────────────────────────────────────────────────────────────

        [MenuItem(k_MenuItemPath)]
        private static void ToggleEnabled()
        {
            IsEnabled = !IsEnabled;
        }

        // ── 回调注册 / 注销 ───────────────────────────────────────────────────

        private static void RegisterCallbacks()
        {
            // 先确保不重复注册
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
        }

        private static void UnregisterCallbacks()
        {
            SceneView.duringSceneGui -= OnSceneGui;

            // 中断正在进行的放置模式
            if (ScenePlacementTool.IsPlacing)
                ScenePlacementTool.CancelPlace();

            // 退出连线模式
            if (RefDragHandler.IsInConnectionMode)
                RefDragHandler.ExitConnectionMode();

            // 失效端口缓存
            RefConnectionDrawer.InvalidateCache();
        }

        // ── SceneGui 主回调 ───────────────────────────────────────────────────

        private static void OnSceneGui(SceneView sceneView)
        {
            var settings = LevelEditorSettings.Instance;

            // 工具条（含开关按钮）无论 IsEnabled 都绘制，使用户可以点击开关
            LevelEditorToolbar.OnSceneGui(sceneView);

            if (!IsEnabled) return;

            // 格点线
            if (settings != null && settings.showGrid)
                GridOverlayDrawer.Draw(sceneView, settings);

            // 左下角常驻操作面板
            LevelEditorSidePanel.OnSceneGui(sceneView);

            // Ghost 放置
            ScenePlacementTool.OnSceneGui(sceneView);

            // 引用连线绘制
            RefConnectionDrawer.OnSceneGui(sceneView);

            // 引用连线拖拽
            RefDragHandler.OnSceneGui(sceneView);
        }

        // ── 菜单 Checkmark 同步 ───────────────────────────────────────────────

        private static void SyncMenuCheckmark()
        {
            Menu.SetChecked(k_MenuItemPath, IsEnabled);
        }
    }
}
