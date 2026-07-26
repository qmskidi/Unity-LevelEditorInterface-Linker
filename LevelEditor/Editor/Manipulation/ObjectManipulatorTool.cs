using LevelEditor.Runtime.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LevelEditor.Editor.Manipulation
{
    /// <summary>
    /// 对选中 PlacedObject 提供移动（格点吸附）和旋转（角度吸附）手柄。
    /// 手柄仅在 Unity 工具栏选中对应工具时显示（Move / Rotate / Transform），
    /// 不干扰其他工具模式（View、Scale、Rect 等）。
    /// Delete 键删除选中对象。
    /// </summary>
    public static class ObjectManipulatorTool
    {
        public static void OnSceneGui(SceneView sceneView)
        {
            var go = Selection.activeGameObject;
            if (go == null) return;

            var po = go.GetComponent<PlacedObject>();
            if (po == null) return;

            // Delete 键删除（与工具模式无关）
            var evt = Event.current;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Delete)
            {
                Undo.DestroyObjectImmediate(go);
                EditorSceneManager.MarkSceneDirty(go.scene);
                evt.Use();
                return;
            }

            var settings   = Core.LevelEditorSettings.Instance;
            float cellSize  = settings != null ? settings.cellSize  : 1f;
            float snapAngle = settings != null ? settings.snapAngle : 45f;
            var   origin    = settings != null ? settings.gridOrigin : Vector3.zero;

            var tool = Tools.current;

            // 移动手柄：仅在 Move 或 Transform 模式下显示
            if (tool == Tool.Move || tool == Tool.Transform)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(
                    go.transform.position, go.transform.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(go.transform, "移动对象");
                    go.transform.position = LevelGrid3D.SnapXZ(newPos, cellSize, origin);
                    EditorSceneManager.MarkSceneDirty(go.scene);
                }
            }

            // 旋转手柄：仅在 Rotate 或 Transform 模式下显示
            if (tool == Tool.Rotate || tool == Tool.Transform)
            {
                EditorGUI.BeginChangeCheck();
                Quaternion newRot = Handles.RotationHandle(
                    go.transform.rotation, go.transform.position);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(go.transform, "旋转对象");
                    Vector3 euler = newRot.eulerAngles;
                    euler.y = LevelGrid3D.SnapAngle(euler.y, snapAngle);
                    go.transform.rotation = Quaternion.Euler(euler);
                    EditorSceneManager.MarkSceneDirty(go.scene);
                }
            }
        }
    }
}
