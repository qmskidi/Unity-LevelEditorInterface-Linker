using System;
using System.Collections.Generic;
using System.Reflection;
using LevelEditor.Runtime.Core;
using LevelEditor.Runtime.Serialization;
using UnityEditor;
using UnityEngine;

namespace LevelEditor.Editor.Connections
{
    /// <summary>
    /// SceneView 连线绘制系统。
    /// 当 Selection 中有 PlacedObject 时，自动扫描其 [LevelSerializeRef] 字段并绘制 Bezier 曲线。
    /// 由 LevelEditorOrchestrator 在 duringSceneGui 中调用 OnSceneGui()。
    /// </summary>
    public static class RefConnectionDrawer
    {
        // 上次选中对象的端口缓存，避免每帧反射
        private static readonly List<ConnectionPort> s_Ports = new List<ConnectionPort>();
        private static GameObject[] s_LastSelection;

        /// <summary>获取当前缓存的端口列表（供 RefDragHandler 使用）</summary>
        public static IReadOnlyList<ConnectionPort> Ports => s_Ports;

        public static void OnSceneGui(SceneView sceneView)
        {
            RefreshPortsIfSelectionChanged();
            DrawAllConnections();
        }

        // ── 端口扫描 ──────────────────────────────────────────────────────────

        private static void RefreshPortsIfSelectionChanged()
        {
            var current = Selection.gameObjects;

            // 简单引用比较：数量或内容不同则刷新
            bool changed = s_LastSelection == null || s_LastSelection.Length != current.Length;
            if (!changed)
            {
                for (int i = 0; i < current.Length; i++)
                    if (current[i] != s_LastSelection[i]) { changed = true; break; }
            }

            if (!changed) return;

            s_LastSelection = (GameObject[])current.Clone();
            s_Ports.Clear();

            int portIndex  = 0; // 字段颜色索引（同字段共用同色）
            int visualSlot = 0; // 每个端口独占一个视觉槽位
            foreach (var go in current)
            {
                if (go == null) continue;
                var po = go.GetComponent<PlacedObject>();
                if (po == null) continue;

                // 递归扫描根节点及子节点
                ScanTransformForPorts(po, go.transform, ref portIndex, ref visualSlot);
            }
        }

        private static void ScanTransformForPorts(PlacedObject po, Transform t, ref int portIndex, ref int visualSlot)
        {
            foreach (var comp in t.GetComponents<Component>())
            {
                if (comp == null) continue;
                foreach (var field in comp.GetType().GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (!field.IsDefined(typeof(LevelSerializeRefAttribute), true)) continue;

                    var fieldType = field.FieldType;

                    // 判断是否为数组/List 引用
                    if (IsRefCollection(fieldType, out _))
                    {
                        // 为每个已有元素生成端口，每个占独立视觉槽
                        var raw = field.GetValue(comp) as System.Collections.IList;
                        int count = raw?.Count ?? 0;
                        for (int i = 0; i < count; i++)
                            s_Ports.Add(new ConnectionPort(po, comp, field, portIndex, visualSlot++, i));

                        // 额外添加一个"+"槽
                        s_Ports.Add(new ConnectionPort(po, comp, field, portIndex, visualSlot++, int.MaxValue));

                        portIndex++;
                    }
                    else
                    {
                        // 单引用端口
                        s_Ports.Add(new ConnectionPort(po, comp, field, portIndex++, visualSlot++));
                    }
                }
            }

            for (int i = 0; i < t.childCount; i++)
                ScanTransformForPorts(po, t.GetChild(i), ref portIndex, ref visualSlot);
        }

        // ── 连线绘制 ──────────────────────────────────────────────────────────

        private static void DrawAllConnections()
        {
            if (s_Ports.Count == 0) return;

            foreach (var port in s_Ports)
            {
                Vector3 portWorldPos = port.GetWorldPosition();

                // 绘制端口圆点
                Handles.color = port.PortColor;
                Handles.DrawSolidDisc(portWorldPos,
                    Camera.current != null ? Camera.current.transform.forward : Vector3.forward,
                    0.12f);

                // 绘制字段名标签
                GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal  = { textColor = port.PortColor },
                    fontSize = 10
                };
                Handles.Label(portWorldPos + Vector3.right * 0.2f, port.DisplayName, labelStyle);

                // "+" 添加槽：跳过连线绘制
                if (port.IsAddSlot)
                {
                    Handles.color = Color.white;
                    continue;
                }

                // 绘制连线
                var target = port.GetTarget();
                if (target != null)
                {
                    Vector3 targetPos = target.transform.position;

                    // 切线：竖直向上，让曲线呈弧形
                    Vector3 tangentA = portWorldPos + Vector3.up * 2f;
                    Vector3 tangentB = targetPos    + Vector3.up * 2f;

                    Handles.DrawBezier(
                        portWorldPos, targetPos,
                        tangentA, tangentB,
                        port.PortColor, null, 2f);

                    // 目标端小圆点
                    Handles.color = port.PortColor * new Color(1, 1, 1, 0.6f);
                    Handles.DrawSolidDisc(targetPos,
                        Camera.current != null ? Camera.current.transform.forward : Vector3.forward,
                        0.08f);
                }

                Handles.color = Color.white;
            }
        }

        /// <summary>强制刷新端口缓存（外部调用，如引用被修改后）</summary>
        public static void InvalidateCache()
        {
            s_LastSelection = null;
        }

        private static bool IsRefCollection(Type type, out Type elemType)
        {
            if (type.IsArray)
            {
                elemType = type.GetElementType();
                return elemType != null && typeof(Component).IsAssignableFrom(elemType);
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                elemType = type.GetGenericArguments()[0];
                return typeof(Component).IsAssignableFrom(elemType);
            }
            elemType = null;
            return false;
        }
    }
}
