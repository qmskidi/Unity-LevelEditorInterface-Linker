using System;
using System.Collections.Generic;
using System.Reflection;
using LevelEditor.Runtime.Serialization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LevelEditor.Editor.ContextMenu
{
    /// <summary>
    /// 参数调整弹窗（PopupWindowContent）。
    /// 右键 PlacedObject → "调整参数" 时弹出，锚定在鼠标位置。
    /// 反射扫描目标对象所有 Component，列出带 [LevelSerialize] 的字段，提供对应 IMGUI 控件。
    /// [LevelSerializeRef] 字段只读显示（引用通过连线操作修改）。
    /// 所有修改通过 Undo.RecordObject 支持撤销。
    /// </summary>
    public sealed class SceneParamPopup : PopupWindowContent
    {
        // ── 内部结构 ──────────────────────────────────────────────────────────

        private struct FieldRecord
        {
            public FieldInfo  field;
            public Component  owner;
            public bool       isRef;
            public string     sourcePath; // 相对于根节点的路径（根节点为空字符串）
        }

        // ── 字段 ──────────────────────────────────────────────────────────────

        private readonly GameObject         m_Target;
        private readonly List<FieldRecord>  m_Fields = new List<FieldRecord>();
        private          Vector2            m_Scroll;

        private const float k_LabelWidth  = 120f;
        private const float k_FieldHeight = 20f;
        private const float k_Padding     = 8f;
        private const float k_WindowWidth = 300f;

        // ── 构造 ──────────────────────────────────────────────────────────────

        public SceneParamPopup(GameObject target)
        {
            m_Target = target;
            ScanFields();
        }

        private void ScanFields()
        {
            if (m_Target == null) return;

            // 递归扫描根节点及所有子节点
            ScanTransform(m_Target.transform, string.Empty);
        }

        private void ScanTransform(Transform t, string path)
        {
            foreach (var comp in t.GetComponents<Component>())
            {
                if (comp == null) continue;
                var type = comp.GetType();

                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    bool isValue = field.IsDefined(typeof(LevelSerializeAttribute),    true);
                    bool isRef   = field.IsDefined(typeof(LevelSerializeRefAttribute), true);

                    if (isValue || isRef)
                        m_Fields.Add(new FieldRecord
                        {
                            field      = field,
                            owner      = comp,
                            isRef      = isRef,
                            sourcePath = path
                        });
                }
            }

            // 递归子节点
            for (int i = 0; i < t.childCount; i++)
            {
                var child     = t.GetChild(i);
                string childPath = string.IsNullOrEmpty(path)
                    ? child.name
                    : $"{path}/{child.name}";
                ScanTransform(child, childPath);
            }
        }

        // ── PopupWindowContent 重写 ───────────────────────────────────────────

        public override Vector2 GetWindowSize()
        {
            float height = k_Padding + 24f; // 总标题

            string lastPath = null;
            foreach (var r in m_Fields)
            {
                // 每遇到新 sourcePath（含根节点切换到子节点）加一行分隔标题
                if (r.sourcePath != lastPath)
                {
                    if (!string.IsNullOrEmpty(r.sourcePath))
                        height += k_FieldHeight + 4f; // 子节点分隔标题行
                    lastPath = r.sourcePath;
                }
                height += k_FieldHeight + 2f;
            }

            height += k_Padding;
            return new Vector2(k_WindowWidth, Mathf.Clamp(height, 60f, 500f));
        }

        public override void OnGUI(Rect rect)
        {
            if (m_Target == null)
            {
                EditorGUILayout.HelpBox("目标对象已被销毁。", MessageType.Warning);
                return;
            }

            float savedLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = k_LabelWidth;

            // 标题
            EditorGUILayout.Space(k_Padding * 0.5f);
            EditorGUILayout.LabelField(
                $"参数调整 — {m_Target.name}",
                EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            // 字段列表（带滚动）
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

            if (m_Fields.Count == 0)
            {
                EditorGUILayout.HelpBox("该对象（含子节点）没有 [LevelSerialize] 或 [LevelSerializeRef] 标注的字段。", MessageType.Info);
            }
            else
            {
                string lastPath = null;
                foreach (var record in m_Fields)
                {
                    // 遇到新的 sourcePath 时绘制分隔标题
                    if (record.sourcePath != lastPath)
                    {
                        if (lastPath != null) EditorGUILayout.Space(4f);

                        string sectionLabel = string.IsNullOrEmpty(record.sourcePath)
                            ? $"[ {m_Target.name} ]"
                            : $"[ {record.sourcePath} ]";
                        EditorGUILayout.LabelField(sectionLabel, EditorStyles.miniLabel);
                        lastPath = record.sourcePath;
                    }

                    DrawField(record);
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUIUtility.labelWidth = savedLabelWidth;
        }

        // ── 私有绘制 ──────────────────────────────────────────────────────────

        private void DrawField(FieldRecord record)
        {
            var field = record.field;
            var owner = record.owner;

            if (record.isRef)
            {
                var fieldType = field.FieldType;

                // 数组/List 引用：逐项显示
                if (IsRefCollection(fieldType))
                {
                    var raw = field.GetValue(owner) as System.Collections.IList;
                    int count = raw?.Count ?? 0;
                    if (count == 0)
                    {
                        EditorGUILayout.LabelField(field.Name, "→ (空数组)");
                    }
                    else
                    {
                        for (int i = 0; i < count; i++)
                        {
                            var elem    = raw[i] as UnityEngine.Object;
                            string disp = elem != null ? $"→ {elem.name}" : "→ 未连接";
                            EditorGUILayout.LabelField($"{field.Name} [{i}]", disp);
                        }
                    }
                    return;
                }

                // 单引用
                var refVal  = field.GetValue(owner) as UnityEngine.Object;
                string display = refVal != null ? $"→ {refVal.name}" : "→ 未连接";
                EditorGUILayout.LabelField(field.Name, display);
                return;
            }

            // 值类型字段：可编辑
            var type  = field.FieldType;
            var value = field.GetValue(owner);

            EditorGUI.BeginChangeCheck();

            object newValue = DrawByType(field.Name, type, value);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(owner, $"修改 {field.Name}");
                field.SetValue(owner, newValue);
                EditorSceneManager.MarkSceneDirty(m_Target.scene);
                EditorUtility.SetDirty(owner);
            }
        }

        private static object DrawByType(string label, Type type, object value)
        {
            if (type == typeof(int))
                return EditorGUILayout.IntField(label, value is int i ? i : 0);

            if (type == typeof(float))
                return EditorGUILayout.FloatField(label, value is float f ? f : 0f);

            if (type == typeof(bool))
                return EditorGUILayout.Toggle(label, value is bool b && b);

            if (type == typeof(string))
                return EditorGUILayout.TextField(label, value as string ?? string.Empty);

            if (type == typeof(Vector3))
                return EditorGUILayout.Vector3Field(label, value is Vector3 v ? v : Vector3.zero);

            if (type == typeof(Color))
                return EditorGUILayout.ColorField(label, value is Color c ? c : Color.white);

            if (type.IsEnum)
                return EditorGUILayout.EnumPopup(label, value as Enum);

            // 不支持的类型：只读显示
            EditorGUILayout.LabelField(label, $"[{type.Name}] 不支持编辑");
            return value;
        }

        private static bool IsRefCollection(System.Type type)
        {
            if (type.IsArray)
            {
                var elem = type.GetElementType();
                return elem != null && typeof(Component).IsAssignableFrom(elem);
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return typeof(Component).IsAssignableFrom(type.GetGenericArguments()[0]);
            return false;
        }
    }
}
