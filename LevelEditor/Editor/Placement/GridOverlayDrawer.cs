using LevelEditor.Runtime.Core;
using UnityEditor;
using UnityEngine;

namespace LevelEditor.Editor.Placement
{
    /// <summary>
    /// SceneView 格点网格线绘制工具。
    /// 在 LevelEditorOrchestrator.OnSceneGui 中每帧调用 Draw()。
    /// 格线以 SceneView 摄像机的 XZ 投影位置为中心动态偏移，跟随视角滚动。
    /// </summary>
    public static class GridOverlayDrawer
    {
        public static void Draw(SceneView sceneView, Editor.Core.LevelEditorSettings settings)
        {
            if (settings == null) return;
            if (!settings.showGrid) return;

            float cellSize  = Mathf.Max(0.1f, settings.cellSize);
            int   range     = Mathf.Max(1, settings.gridRange);
            Color gridColor = settings.gridColor;
            Vector3 origin  = settings.gridOrigin;

            // 以摄像机在 XZ 平面的投影为中心，确保格线随视角移动
            Vector3 camPos = sceneView.camera.transform.position;
            float centerX = Mathf.Round((camPos.x - origin.x) / cellSize) * cellSize + origin.x;
            float centerZ = Mathf.Round((camPos.z - origin.z) / cellSize) * cellSize + origin.z;
            float baseY   = origin.y;

            Handles.color = gridColor;

            // 绘制平行于 Z 轴的竖线
            for (int i = -range; i <= range; i++)
            {
                float x = centerX + i * cellSize;
                Vector3 start = new Vector3(x, baseY, centerZ - range * cellSize);
                Vector3 end   = new Vector3(x, baseY, centerZ + range * cellSize);
                Handles.DrawLine(start, end);
            }

            // 绘制平行于 X 轴的横线
            for (int j = -range; j <= range; j++)
            {
                float z = centerZ + j * cellSize;
                Vector3 start = new Vector3(centerX - range * cellSize, baseY, z);
                Vector3 end   = new Vector3(centerX + range * cellSize, baseY, z);
                Handles.DrawLine(start, end);
            }

            Handles.color = Color.white; // 重置颜色避免影响其他 Handles 绘制
        }
    }
}
