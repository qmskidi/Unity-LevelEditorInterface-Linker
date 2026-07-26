using System;
using System.Collections.Generic;
using LevelEditor.Runtime.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LevelEditor.Editor.Connections
{
    /// <summary>
    /// 引用连线拖拽交互处理器。
    ///
    /// 交互模型：
    ///   1. 右键菜单"管理引用" → EnterConnectionMode()，源对象端口显示
    ///   2. 点击端口 → 扫描场景，找出所有持有匹配组件类型的 PlacedObject，
    ///      在其顶部显示「目标连接点」（白色圆圈）
    ///   3. 拖拽虚线跟随鼠标；靠近目标连接点时高亮（绿色放大）
    ///   4. 松手 → 若在目标连接点附近则赋值引用，否则取消
    ///   5. Escape 或点击空白退出
    ///
    /// Repaint 帧内绝不调用 PickGameObject（避免嵌套渲染断言）。
    /// </summary>
    public static class RefDragHandler
    {
        // ── 候选目标结构 ───────────────────────────────────────────────────────

        private struct TargetCandidate
        {
            public PlacedObject PlacedObj;
            public Component    Comp;

            public Vector3 GetWorldPosition()
            {
                var renderers = PlacedObj.GetComponentsInChildren<Renderer>();
                Bounds bounds = new Bounds(PlacedObj.transform.position, Vector3.one);
                if (renderers.Length > 0)
                {
                    bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                        bounds.Encapsulate(renderers[i].bounds);
                }
                // 包围盒顶面中心，略高于顶面
                return new Vector3(bounds.center.x, bounds.max.y + 0.25f, bounds.center.z);
            }
        }

        // ── 状态 ───────────────────────────────────────────────────────────────

        private static bool           s_IsInConnectionMode;
        private static PlacedObject   s_SourceObject;
        private static ConnectionPort s_SelectedPort;
        private static bool           s_IsDragging;
        private static Vector2        s_DragMousePos;

        // 候选目标列表（选中端口时一次性扫描，拖拽期间只读）
        private static readonly List<TargetCandidate> s_Candidates = new List<TargetCandidate>();

        // 当前鼠标最近的候选目标（仅在 MouseDrag/Move 时更新）
        private static int s_NearestIndex = -1;

        // 屏幕像素距离阈值：鼠标在此范围内视为"悬停在目标连接点上"
        private const float k_SnapPixels = 28f;

        // GUIUtility.hotControl 占位 id
        private static int s_DragControlId;

        public static bool IsInConnectionMode => s_IsInConnectionMode;

        // ── 公开接口 ───────────────────────────────────────────────────────────

        public static void EnterConnectionMode(PlacedObject source)
        {
            s_IsInConnectionMode = true;
            s_SourceObject       = source;
            s_SelectedPort       = null;
            s_IsDragging         = false;
            s_NearestIndex       = -1;
            s_Candidates.Clear();
            s_DragControlId      = 0;

            Selection.activeGameObject = source.gameObject;
            RefConnectionDrawer.InvalidateCache();
            SceneView.RepaintAll();
        }

        public static void ExitConnectionMode()
        {
            s_IsInConnectionMode = false;
            s_SourceObject       = null;
            s_SelectedPort       = null;
            s_IsDragging         = false;
            s_NearestIndex       = -1;
            s_Candidates.Clear();

            if (GUIUtility.hotControl == s_DragControlId && s_DragControlId != 0)
                GUIUtility.hotControl = 0;
            s_DragControlId = 0;

            SceneView.RepaintAll();
        }

        // ── SceneGui 回调 ──────────────────────────────────────────────────────

        public static void OnSceneGui(SceneView sceneView)
        {
            if (!s_IsInConnectionMode) return;

            var evt      = Event.current;
            var ports    = RefConnectionDrawer.Ports;
            int controlId = GUIUtility.GetControlID(FocusType.Passive);

            // ── Escape 退出 ──────────────────────────────────────────────────
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            {
                ExitConnectionMode();
                evt.Use();
                return;
            }

            // ── Repaint：纯绘制，不调用任何拾取 API ─────────────────────────
            if (evt.type == EventType.Repaint)
            {
                DrawHighlightedPorts(ports);
                if (s_IsDragging && s_SelectedPort != null)
                {
                    DrawCandidates();
                    DrawDragPreview(s_DragMousePos);
                }
                return;
            }

            // ── MouseDown（左键）：端口点击 → 收集候选目标 ──────────────────
            if (!s_IsDragging && evt.type == EventType.MouseDown && evt.button == 0)
            {
                if (TrySelectPort(ports, evt.mousePosition))
                {
                    s_IsDragging   = true;
                    s_DragMousePos = evt.mousePosition;
                    s_NearestIndex = -1;
                    CollectCandidates();          // 扫描场景，找匹配目标
                    s_DragControlId       = controlId;
                    GUIUtility.hotControl = controlId; // 占据热控件
                    evt.Use();
                }
                else
                {
                    ExitConnectionMode();
                }
                return;
            }

            // ── MouseDrag / MouseMove：更新鼠标位置 + 最近候选 ──────────────
            if (s_IsDragging && (evt.type == EventType.MouseDrag ||
                                  evt.type == EventType.MouseMove))
            {
                s_DragMousePos = evt.mousePosition;
                s_NearestIndex = FindNearestCandidate(evt.mousePosition);
                sceneView.Repaint();
                evt.Use();
                return;
            }

            // ── MouseUp（左键）：赋值并退出 ─────────────────────────────────
            if (s_IsDragging && evt.type == EventType.MouseUp && evt.button == 0)
            {
                int idx = FindNearestCandidate(evt.mousePosition);
                if (idx >= 0)
                    AssignReference(s_Candidates[idx]);
                else
                    ClearReference(); // 未命中目标 → 清空引用

                ExitConnectionMode();
                evt.Use();
            }
        }

        // ── 候选目标收集 ──────────────────────────────────────────────────────

        private static void CollectCandidates()
        {
            s_Candidates.Clear();
            if (s_SelectedPort == null) return;

            var fieldType = s_SelectedPort.Field.FieldType;

#if UNITY_2023_1_OR_NEWER
            var allPOs = UnityEngine.Object.FindObjectsByType<PlacedObject>(FindObjectsSortMode.None);
#else
            var allPOs = UnityEngine.Object.FindObjectsOfType<PlacedObject>();
#endif
            foreach (var po in allPOs)
            {
                if (po == s_SelectedPort.Source) continue; // 跳过自身
                var comp = po.GetComponentInChildren(fieldType);
                if (comp != null)
                    s_Candidates.Add(new TargetCandidate { PlacedObj = po, Comp = comp });
            }
        }

        // ── 最近候选查找（屏幕像素距离）────────────────────────────────────

        private static int FindNearestCandidate(Vector2 mouseScreenPos)
        {
            int   bestIdx  = -1;
            float bestDist = k_SnapPixels;

            for (int i = 0; i < s_Candidates.Count; i++)
            {
                Vector3 worldPos  = s_Candidates[i].GetWorldPosition();
                Vector2 screenPos = HandleUtility.WorldToGUIPoint(worldPos);
                float   dist      = Vector2.Distance(mouseScreenPos, screenPos);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx  = i;
                }
            }
            return bestIdx;
        }

        // ── 绘制 ─────────────────────────────────────────────────────────────

        private static void DrawHighlightedPorts(IReadOnlyList<ConnectionPort> ports)
        {
            var camFwd = Camera.current != null
                ? Camera.current.transform.forward
                : Vector3.forward;

            foreach (var port in ports)
            {
                bool  isSelected = port == s_SelectedPort;
                float radius     = isSelected ? 0.22f : 0.18f;
                Color color      = isSelected
                    ? Color.Lerp(port.PortColor, Color.white, 0.5f)
                    : port.PortColor;

                Handles.color = color;
                Handles.DrawSolidDisc(port.GetWorldPosition(), camFwd, radius);
            }
            Handles.color = Color.white;
        }

        /// <summary>绘制所有候选目标连接点（白色圆圈；最近的放大并变绿）</summary>
        private static void DrawCandidates()
        {
            var camFwd = Camera.current != null
                ? Camera.current.transform.forward
                : Vector3.forward;

            for (int i = 0; i < s_Candidates.Count; i++)
            {
                Vector3 pos       = s_Candidates[i].GetWorldPosition();
                bool    isNearest = i == s_NearestIndex;
                float   radius    = isNearest ? 0.20f : 0.13f;
                Color   color     = isNearest
                    ? new Color(0.3f, 1f, 0.4f, 1f)   // 绿色高亮
                    : new Color(1f, 1f, 1f, 0.7f);     // 半透明白色

                // 外圈（空心）
                Handles.color = color;
                Handles.DrawWireDisc(pos, camFwd, radius);

                // 最近目标额外画实心小点和包围盒高亮
                if (isNearest)
                {
                    Handles.DrawSolidDisc(pos, camFwd, radius * 0.35f);
                    DrawBoundsHighlight(s_Candidates[i].PlacedObj, color);
                }

                // 候选对象名称标签
                Handles.Label(pos + Vector3.up * (radius + 0.1f),
                    s_Candidates[i].PlacedObj.name,
                    new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = { textColor = color }
                    });
            }
            Handles.color = Color.white;
        }

        private static void DrawBoundsHighlight(PlacedObject po, Color color)
        {
            var renderers = po.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            Handles.color = color;
            Handles.DrawWireCube(bounds.center, bounds.size * 1.05f);
        }

        private static void DrawDragPreview(Vector2 mouseScreenPos)
        {
            if (s_SelectedPort == null) return;

            Vector3 portWorldPos = s_SelectedPort.GetWorldPosition();

            // 若有最近候选，线拉到候选点；否则拉到鼠标投影
            Vector3 targetWorldPos;
            if (s_NearestIndex >= 0)
            {
                targetWorldPos = s_Candidates[s_NearestIndex].GetWorldPosition();
            }
            else
            {
                Ray   ray = HandleUtility.GUIPointToWorldRay(mouseScreenPos);
                float t   = ray.direction.y != 0f
                    ? (portWorldPos.y - ray.origin.y) / ray.direction.y
                    : 100f;
                targetWorldPos = ray.origin + ray.direction * Mathf.Max(0.1f, t);
            }

            Handles.color = s_NearestIndex >= 0
                ? new Color(0.3f, 1f, 0.4f, 0.9f)
                : new Color(1f, 1f, 1f, 0.6f);
            Handles.DrawDottedLine(portWorldPos, targetWorldPos, 4f);
            Handles.color = Color.white;
        }

        // ── 赋值 ─────────────────────────────────────────────────────────────

        private static void AssignReference(TargetCandidate target)
        {
            if (s_SelectedPort == null) return;

            Undo.RecordObject(s_SelectedPort.Owner, $"连接引用 {s_SelectedPort.FieldName}");

            if (s_SelectedPort.IsAddSlot)
            {
                // 数组"+"槽：追加元素
                AppendToCollection(s_SelectedPort, target.Comp);
            }
            else if (s_SelectedPort.IsArrayPort)
            {
                // 数组已有槽：替换元素
                SetCollectionElement(s_SelectedPort, target.Comp);
            }
            else
            {
                // 单引用
                s_SelectedPort.Field.SetValue(s_SelectedPort.Owner, target.Comp);
            }

            EditorSceneManager.MarkSceneDirty(s_SelectedPort.Source.gameObject.scene);
            EditorUtility.SetDirty(s_SelectedPort.Owner);
            RefConnectionDrawer.InvalidateCache();
        }

        private static void ClearReference()
        {
            if (s_SelectedPort == null) return;

            Undo.RecordObject(s_SelectedPort.Owner, $"清空引用 {s_SelectedPort.FieldName}");

            if (s_SelectedPort.IsAddSlot)
            {
                // "+"槽未命中：不做任何操作
            }
            else if (s_SelectedPort.IsArrayPort)
            {
                // 数组槽未命中：移除该元素
                RemoveFromCollection(s_SelectedPort);
            }
            else
            {
                // 单引用：置 null
                s_SelectedPort.Field.SetValue(s_SelectedPort.Owner, null);
            }

            EditorSceneManager.MarkSceneDirty(s_SelectedPort.Source.gameObject.scene);
            EditorUtility.SetDirty(s_SelectedPort.Owner);
            RefConnectionDrawer.InvalidateCache();
        }

        // ── 集合操作工具 ─────────────────────────────────────────────────────

        private static void AppendToCollection(ConnectionPort port, Component comp)
        {
            var raw       = port.Field.GetValue(port.Owner);
            var fieldType = port.Field.FieldType;

            if (fieldType.IsArray)
            {
                var elemType = fieldType.GetElementType();
                var old      = raw as System.Array ?? Array.CreateInstance(elemType, 0);
                var arr      = Array.CreateInstance(elemType, old.Length + 1);
                Array.Copy(old, arr, old.Length);
                arr.SetValue(comp, old.Length);
                port.Field.SetValue(port.Owner, arr);
            }
            else
            {
                // List<T>
                var list = raw ?? Activator.CreateInstance(fieldType);
                fieldType.GetMethod("Add").Invoke(list, new object[] { comp });
                port.Field.SetValue(port.Owner, list);
            }
        }

        private static void SetCollectionElement(ConnectionPort port, Component comp)
        {
            var raw = port.Field.GetValue(port.Owner);
            if (raw is System.Collections.IList list && port.ArrayElementIndex < list.Count)
                list[port.ArrayElementIndex] = comp;
        }

        private static void RemoveFromCollection(ConnectionPort port)
        {
            var raw       = port.Field.GetValue(port.Owner);
            var fieldType = port.Field.FieldType;
            int idx       = port.ArrayElementIndex;

            if (fieldType.IsArray)
            {
                var old      = raw as System.Array;
                if (old == null || idx >= old.Length) return;
                var elemType = fieldType.GetElementType();
                var arr      = Array.CreateInstance(elemType, old.Length - 1);
                int dst = 0;
                for (int i = 0; i < old.Length; i++)
                    if (i != idx) arr.SetValue(old.GetValue(i), dst++);
                port.Field.SetValue(port.Owner, arr);
            }
            else
            {
                if (raw is System.Collections.IList list && idx < list.Count)
                    list.RemoveAt(idx);
            }
        }

        // ── 端口选择 ─────────────────────────────────────────────────────────

        private static bool TrySelectPort(IReadOnlyList<ConnectionPort> ports, Vector2 mousePos)
        {
            const float k_PickRadius = 0.25f;

            foreach (var port in ports)
            {
                Vector3 worldPos  = port.GetWorldPosition();
                Vector3 screenPos = HandleUtility.WorldToGUIPoint(worldPos);
                float   dist      = Vector2.Distance(mousePos, screenPos);

                if (dist < k_PickRadius / HandleUtility.GetHandleSize(worldPos) * 60f)
                {
                    s_SelectedPort = port;
                    return true;
                }
            }
            return false;
        }
    }
}
