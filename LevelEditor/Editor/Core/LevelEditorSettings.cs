using UnityEditor;
using UnityEngine;

namespace LevelEditor.Editor.Core
{
    /// <summary>
    /// 关卡编辑器全局配置（Editor 专用 ScriptableObject，不进 Build）。
    /// 通过 Assets/LevelEditor/Data/LevelEditorSettings.asset 实例化。
    /// </summary>
    [CreateAssetMenu(
        menuName = "关卡编辑器/编辑器设置",
        fileName = "LevelEditorSettings")]
    public sealed class LevelEditorSettings : ScriptableObject
    {
        // ── 格点配置 ──────────────────────────────────────────────────────────

        [Header("格点配置")]
        [Tooltip("格点单元格大小（世界单位）")]
        public float cellSize = 1f;

        [Tooltip("格点角度吸附步长（度）")]
        public float snapAngle = 45f;

        [Tooltip("格点显示范围（从中心向外各 N 格）")]
        public int gridRange = 20;

        [Tooltip("格点线颜色")]
        public Color gridColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);

        [Tooltip("格点原点世界坐标")]
        public Vector3 gridOrigin = Vector3.zero;

        [Tooltip("是否显示格点线")]
        public bool showGrid = true;

        // ── 放置配置 ──────────────────────────────────────────────────────────

        [Header("放置配置")]
        [Tooltip("放置 Raycast 检测层（应排除 Ghost 层）")]
        public LayerMask placementLayerMask = ~0; // 默认全部层

        [Tooltip("放置时 Shift+滚轮 调整 Y 轴偏移的步长（世界单位）")]
        public float yOffsetStep = 0.25f;

        // ── 连线配置 ──────────────────────────────────────────────────────────

        [Header("连线配置")]
        [Tooltip("连线端口圆点半径（世界单位）")]
        public float portRadius = 0.15f;

        [Tooltip("连线曲线宽度（像素）")]
        public float connectionLineWidth = 2f;

        // ── 单例访问 ──────────────────────────────────────────────────────────

        private static LevelEditorSettings s_Instance;

        /// <summary>
        /// 从 Assets/LevelEditor/Data/ 加载设置实例。
        /// 若找不到则返回临时默认值（不抛异常）。
        /// </summary>
        public static LevelEditorSettings Instance
        {
            get
            {
                if (s_Instance != null) return s_Instance;

                // 尝试从固定路径加载
                s_Instance = AssetDatabase.LoadAssetAtPath<LevelEditorSettings>(
                    "Assets/LevelEditor/Data/LevelEditorSettings.asset");

                if (s_Instance == null)
                {
                    // 查找项目中任意一个
                    var guids = AssetDatabase.FindAssets("t:LevelEditorSettings");
                    if (guids.Length > 0)
                        s_Instance = AssetDatabase.LoadAssetAtPath<LevelEditorSettings>(
                            AssetDatabase.GUIDToAssetPath(guids[0]));
                }

                if (s_Instance == null)
                    Debug.LogWarning("[LevelEditorSettings] 未找到设置资产，使用临时默认值。请通过菜单 '关卡编辑器/编辑器设置' 创建资产。");

                return s_Instance;
            }
        }
    }
}
