using LevelEditor.Runtime.Core;
using UnityEditor;
using UnityEngine;

namespace LevelEditor.Editor.Inspector
{
    /// <summary>
    /// PlacedObject 的自定义 Inspector。
    /// 显示：GUID、PrefabKey；提供"重置 GUID"和"选中关联对象"按钮。
    /// </summary>
    [CustomEditor(typeof(PlacedObject))]
    public sealed class PlacedObjectInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var po = (PlacedObject)target;

            // 只读 GUID
            EditorGUILayout.LabelField("关卡 ID（GUID）", po.LevelId,
                EditorStyles.textField);

            // 只读 PrefabKey
            EditorGUILayout.LabelField("预制体键", po.PrefabKey ?? "（未设置）",
                EditorStyles.textField);

            EditorGUILayout.Space(6f);

            EditorGUILayout.BeginHorizontal();

            // 重置 GUID 按钮
            GUI.color = new Color(1f, 0.8f, 0.6f);
            if (GUILayout.Button("重置 GUID"))
            {
                bool confirm = EditorUtility.DisplayDialog(
                    "重置 GUID",
                    "重置后该对象的 GUID 将改变，任何引用此对象的 JSON 关卡将无法正确加载。\n确定继续？",
                    "重置", "取消");

                if (confirm)
                {
                    Undo.RecordObject(po, "重置 GUID");
                    po.ResetGuid();
                    EditorUtility.SetDirty(po);
                }
            }

            GUI.color = Color.white;

            // 选中关联对象（在 RefConnectionDrawer 中选中引用了此对象的 PlacedObject）
            if (GUILayout.Button("查找引用此对象者"))
                FindAndSelectReferencers(po);

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);

            // 绘制默认 Inspector 内容（其他字段）
            DrawDefaultInspector();
        }

        private static void FindAndSelectReferencers(PlacedObject target)
        {
            var all = FindObjectsOfType<PlacedObject>();
            var found = new System.Collections.Generic.List<GameObject>();

            foreach (var po in all)
            {
                if (po == target) continue;

                foreach (var comp in po.GetComponents<Component>())
                {
                    if (comp == null) continue;
                    foreach (var field in comp.GetType().GetFields(
                                 System.Reflection.BindingFlags.Public |
                                 System.Reflection.BindingFlags.Instance))
                    {
                        if (!field.IsDefined(typeof(Runtime.Serialization.LevelSerializeRefAttribute), true))
                            continue;

                        var val = field.GetValue(comp) as Component;
                        if (val != null && val.GetComponent<PlacedObject>() == target)
                        {
                            found.Add(po.gameObject);
                            break;
                        }
                    }
                }
            }

            if (found.Count == 0)
            {
                EditorUtility.DisplayDialog("查找结果", "未找到引用此对象的其他 PlacedObject。", "OK");
                return;
            }

            Selection.objects = found.ToArray();
            Debug.Log($"[PlacedObjectInspector] 找到 {found.Count} 个引用 '{target.name}' 的对象，已选中。");
        }
    }
}
