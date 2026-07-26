using LevelEditor.Runtime.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LevelEditor.Editor.Core
{
    /// <summary>
    /// 新建关卡引导器。
    /// 菜单：Tools/关卡编辑器/新建关卡
    /// 在场景内创建必要 Manager 对象（LevelIDRegistry 等），支持整组 Undo 撤销。
    /// </summary>
    public static class LevelManagerBootstrapper
    {
        [MenuItem("Tools/关卡编辑器/新建关卡")]
        public static void CreateNewLevel()
        {
            // 询问是否继续
            bool proceed = EditorUtility.DisplayDialog(
                "新建关卡",
                "将在当前场景中创建关卡管理器对象（LevelIDRegistry 等）。\n" +
                "可通过 Ctrl+Z 整组撤销。\n\n继续？",
                "创建", "取消");

            if (!proceed) return;

            // 使用 Undo Group 包裹所有操作
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("新建关卡管理器");
            int group = Undo.GetCurrentGroup();

            // ── LevelIDRegistry ───────────────────────────────────────────────

            EnsureManagerExists<LevelIDRegistry>("LevelIDRegistry");

            // ── 可在此处追加其他 Manager ──────────────────────────────────────
            // EnsureManagerExists<YourManager>("YourManager");

            // 合并 Undo 记录，使 Ctrl+Z 一次撤销所有 Manager
            Undo.CollapseUndoOperations(group);

            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog(
                "新建关卡",
                "关卡管理器创建完成！\n" +
                "下一步：在 Assets/LevelEditor/Data/ 中配置预制体注册表，\n" +
                "然后使用 Ctrl+Shift+E 开启关卡编辑器。",
                "OK");
        }

        // ── 工具方法 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 若场景中不存在指定 Manager，则创建一个携带该组件的 GameObject。
        /// </summary>
        private static T EnsureManagerExists<T>(string goName) where T : Component
        {
            var existing = Object.FindObjectOfType<T>();
            if (existing != null)
            {
                Debug.Log($"[LevelManagerBootstrapper] {typeof(T).Name} 已存在，跳过创建。");
                return existing;
            }

            var go = new GameObject(goName);
            Undo.RegisterCreatedObjectUndo(go, $"创建 {goName}");
            var comp = go.AddComponent<T>();
            Debug.Log($"[LevelManagerBootstrapper] 已创建 {goName}。");
            return comp;
        }
    }
}
