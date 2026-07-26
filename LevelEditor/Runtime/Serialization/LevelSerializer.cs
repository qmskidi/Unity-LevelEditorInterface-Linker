using System;
using System.Collections.Generic;
using System.Reflection;
using LevelEditor.Runtime.Core;
using LevelEditor.Runtime.Serialization;
using UnityEngine;

namespace LevelEditor.Runtime.Serialization
{
    /// <summary>
    /// 序列化引擎：扫描场景中所有 PlacedObject，通过反射收集
    /// [LevelSerialize] / [LevelSerializeRef] 标注的字段，输出 LevelData。
    /// 仅在保存时调用，不在帧更新中运行，反射开销可接受。
    /// </summary>
    public static class LevelSerializer
    {
        // 支持的值类型集合
        private static readonly HashSet<Type> k_SupportedTypes = new HashSet<Type>
        {
            typeof(bool), typeof(int), typeof(float), typeof(string),
            typeof(Vector3), typeof(Color)
            // Enum 通过 field.FieldType.IsEnum 单独判断
        };

        /// <summary>
        /// 序列化当前场景中所有 PlacedObject，返回 LevelData。
        /// </summary>
        public static LevelData Serialize(string levelName)
        {
            var data = new LevelData
            {
                levelName = levelName,
                version   = "1.0",
                savedAt   = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

#if UNITY_2023_1_OR_NEWER
            var placedObjects = UnityEngine.Object.FindObjectsByType<PlacedObject>(FindObjectsSortMode.None);
#else
            var placedObjects = UnityEngine.Object.FindObjectsOfType<PlacedObject>();
#endif

            foreach (var po in placedObjects)
            {
                var objData = new PlacedObjectData
                {
                    levelId    = po.LevelId,
                    prefabKey  = po.PrefabKey,
                    position    = SerializableVector3.FromVector3(po.transform.position),
                    eulerAngles = SerializableVector3.FromVector3(po.transform.eulerAngles),
                    scale       = SerializableVector3.FromVector3(po.transform.localScale)
                };

                // 递归扫描根节点及所有子节点的 Component
                CollectComponents(po.transform, objData, po);

                data.objects.Add(objData);
            }

            return data;
        }

        private static void CollectComponents(Transform t, PlacedObjectData objData, PlacedObject ownerPO)
        {
            foreach (var comp in t.GetComponents<Component>())
            {
                if (comp == null) continue;

                var compData = SerializeComponent(comp, ownerPO);
                if (compData != null)
                    objData.components.Add(compData);
            }

            for (int i = 0; i < t.childCount; i++)
                CollectComponents(t.GetChild(i), objData, ownerPO);
        }

        private static ComponentData SerializeComponent(Component comp, PlacedObject ownerPO)
        {
            var type = comp.GetType();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            ComponentData compData = null; // 懒创建：只有至少一个字段时才新建

            foreach (var field in fields)
            {
                // 值类型字段
                if (field.IsDefined(typeof(LevelSerializeAttribute), true))
                {
                    compData ??= new ComponentData { typeName = type.FullName };
                    var entry = SerializeValueField(field, comp);
                    if (entry != null)
                        compData.fields.Add(entry);
                }
                // 引用类型字段
                else if (field.IsDefined(typeof(LevelSerializeRefAttribute), true))
                {
                    compData ??= new ComponentData { typeName = type.FullName };
                    var entry = SerializeRefField(field, comp);
                    if (entry != null)
                        compData.fields.Add(entry);
                }
            }

            return compData;
        }

        private static FieldEntry SerializeValueField(FieldInfo field, Component comp)
        {
            var value = field.GetValue(comp);
            var type  = field.FieldType;

            string strValue;
            try
            {
                if (type == typeof(bool))        strValue = ((bool)value).ToString();
                else if (type == typeof(int))    strValue = ((int)value).ToString();
                else if (type == typeof(float))  strValue = ((float)value).ToString("R");
                else if (type == typeof(string)) strValue = (string)value ?? string.Empty;
                else if (type == typeof(Vector3))
                {
                    var v = (Vector3)value;
                    strValue = $"{v.x:R},{v.y:R},{v.z:R}";
                }
                else if (type == typeof(Color))
                {
                    var c = (Color)value;
                    strValue = $"{c.r:R},{c.g:R},{c.b:R},{c.a:R}";
                }
                else if (type.IsEnum)
                {
                    strValue = value.ToString();
                }
                else
                {
                    Debug.LogWarning($"[LevelSerializer] 字段 '{field.Name}' 类型 '{type.Name}' 不支持序列化，已跳过。");
                    return null;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LevelSerializer] 序列化字段 '{field.Name}' 失败：{e.Message}");
                return null;
            }

            return new FieldEntry
            {
                fieldName = field.Name,
                typeName  = type.FullName,
                value     = strValue,
                isRef     = false
            };
        }

        private static FieldEntry SerializeRefField(FieldInfo field, Component comp)
        {
            var fieldType = field.FieldType;
            var rawValue  = field.GetValue(comp);

            // ── 数组/List 引用 ────────────────────────────────────────────────
            if (IsRefCollection(fieldType, out var elemType))
            {
                var entry = new FieldEntry
                {
                    fieldName = field.Name,
                    typeName  = fieldType.FullName,
                    isRef     = true
                };

                var elements = rawValue as System.Collections.IEnumerable;
                if (elements != null)
                {
                    foreach (var elem in elements)
                    {
                        var refComp = elem as Component;
                        if (refComp == null) { entry.refValues.Add(string.Empty); continue; }

                        var targetPO = refComp.GetComponent<PlacedObject>();
                        if (targetPO == null)
                        {
                            Debug.LogWarning($"[LevelSerializer] 数组字段 '{field.Name}' 的元素 '{refComp.gameObject.name}' 未携带 PlacedObject 组件，写入空 GUID。");
                            entry.refValues.Add(string.Empty);
                        }
                        else
                        {
                            entry.refValues.Add(targetPO.LevelId);
                        }
                    }
                }
                return entry;
            }

            // ── 单引用 ────────────────────────────────────────────────────────
            var refValue = rawValue as Component;
            if (refValue == null)
            {
                return new FieldEntry
                {
                    fieldName = field.Name,
                    typeName  = fieldType.FullName,
                    value     = string.Empty,
                    isRef     = true
                };
            }

            var po = refValue.GetComponent<PlacedObject>();
            if (po == null)
            {
                Debug.LogWarning($"[LevelSerializer] 字段 '{field.Name}' 引用的对象 '{refValue.gameObject.name}' 未携带 PlacedObject 组件，无法序列化引用，已跳过。");
                return null;
            }

            return new FieldEntry
            {
                fieldName = field.Name,
                typeName  = fieldType.FullName,
                value     = po.LevelId,
                isRef     = true
            };
        }

        /// <summary>
        /// 判断类型是否为引用集合（T[] 或 List&lt;T&gt;，其中 T 是 Component 子类），
        /// 并输出元素类型。
        /// </summary>
        private static bool IsRefCollection(Type type, out Type elemType)
        {
            // T[]
            if (type.IsArray)
            {
                elemType = type.GetElementType();
                return elemType != null && typeof(Component).IsAssignableFrom(elemType);
            }
            // List<T>
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
