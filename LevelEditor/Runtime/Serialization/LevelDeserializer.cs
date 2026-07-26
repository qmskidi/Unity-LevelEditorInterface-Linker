using System;
using System.Collections.Generic;
using System.Reflection;
using LevelEditor.Runtime.Core;
using LevelEditor.Runtime.Data;
using LevelEditor.Runtime.Serialization;
using UnityEngine;

namespace LevelEditor.Runtime.Serialization
{
    /// <summary>
    /// 反序列化引擎：两遍加载。
    /// 第一遍：按 prefabKey 实例化所有对象，赋 Transform，建立 GUID→PlacedObject 字典。
    /// 第二遍：遍历所有 isRef 字段，通过 GUID 字典绑定 Component 引用。
    /// </summary>
    public static class LevelDeserializer
    {
        /// <summary>
        /// Runtime 侧第一遍：实例化所有 PlacedObject（使用 Object.Instantiate）。
        /// Editor 侧应使用 LevelSaveLoadEditor 内的重载（PrefabUtility.InstantiatePrefab）。
        /// </summary>
        /// <returns>GUID → PlacedObject 字典，供第二遍引用绑定使用</returns>
        public static Dictionary<string, PlacedObject> InstantiateAll(
            LevelData data,
            LevelEditorPrefabRegistry registry)
        {
            var dict = new Dictionary<string, PlacedObject>();

            foreach (var objData in data.objects)
            {
                var entry = registry.FindByKey(objData.prefabKey);
                if (entry == null)
                {
                    Debug.LogWarning($"[LevelDeserializer] 未找到 prefabKey='{objData.prefabKey}' 对应的预制体，跳过该对象。");
                    continue;
                }

                var go = UnityEngine.Object.Instantiate(entry.prefab);
                go.name = entry.prefab.name;

                // 还原 Transform
                go.transform.position   = objData.position.ToVector3();
                go.transform.eulerAngles = objData.eulerAngles.ToVector3();
                go.transform.localScale  = objData.scale.ToVector3();

                // 确保有 PlacedObject 组件，并还原 GUID
                var po = go.GetComponent<PlacedObject>();
                if (po == null) po = go.AddComponent<PlacedObject>();

                // 用反射写入序列化的 GUID（绕过 Awake 的自动生成）
                var field = typeof(PlacedObject).GetField("m_LevelId",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                field?.SetValue(po, objData.levelId);

                po.Reinitialize(objData.prefabKey);

                // 还原 [LevelSerialize] 值类型字段（先于引用绑定）
                RestoreValueFields(go, objData);

                dict[objData.levelId] = po;
            }

            return dict;
        }

        /// <summary>
        /// 第二遍：用 GUID 字典绑定所有 [LevelSerializeRef] 引用字段。
        /// </summary>
        public static void BindReferences(
            LevelData data,
            Dictionary<string, PlacedObject> dict)
        {
            foreach (var objData in data.objects)
            {
                if (!dict.TryGetValue(objData.levelId, out var po)) continue;

                foreach (var compData in objData.components)
                {
                    var compType = FindType(compData.typeName);
                    if (compType == null)
                    {
                        Debug.LogWarning($"[LevelDeserializer] 未找到类型 '{compData.typeName}'，跳过。");
                        continue;
                    }

                    var comp = po.GetComponentInChildren(compType);
                    if (comp == null) continue;

                    foreach (var fieldEntry in compData.fields)
                    {
                        if (!fieldEntry.isRef) continue;

                        var fieldInfo = compType.GetField(fieldEntry.fieldName,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (fieldInfo == null) continue;

                        var fieldType = fieldInfo.FieldType;

                        // ── 数组/List 引用 ────────────────────────────────────
                        if (fieldEntry.refValues != null && fieldEntry.refValues.Count > 0)
                        {
                            BindRefCollection(comp, fieldInfo, fieldType, fieldEntry.refValues, dict);
                            continue;
                        }

                        // ── 单引用 ────────────────────────────────────────────
                        if (string.IsNullOrEmpty(fieldEntry.value))
                        {
                            fieldInfo.SetValue(comp, null);
                            continue;
                        }

                        if (!dict.TryGetValue(fieldEntry.value, out var targetPO))
                        {
                            Debug.LogWarning($"[LevelDeserializer] 字段 '{fieldEntry.fieldName}' 引用 GUID '{fieldEntry.value}' 未找到对应对象，跳过。");
                            continue;
                        }

                        var targetComp = targetPO.GetComponentInChildren(fieldType);
                        if (targetComp == null)
                        {
                            Debug.LogWarning($"[LevelDeserializer] 目标对象 '{targetPO.name}' 上未找到类型 '{fieldType.Name}' 的组件。");
                            continue;
                        }

                        fieldInfo.SetValue(comp, targetComp);
                    }
                }
            }
        }

        // ── 工具（公开给 Editor 侧使用）─────────────────────────────────────

        /// <summary>
        /// 公开版值字段还原，供 Editor 侧（LevelSaveLoadEditor）调用。
        /// </summary>
        public static void RestoreValueFieldsStatic(PlacedObjectData objData, GameObject go)
            => RestoreValueFields(go, objData);

        // ── 私有工具 ──────────────────────────────────────────────────────────

        private static void RestoreValueFields(GameObject go, PlacedObjectData objData)
        {
            foreach (var compData in objData.components)
            {
                var compType = FindType(compData.typeName);
                if (compType == null) continue;

                // 搜索根节点及子节点
                var comp = go.GetComponentInChildren(compType);
                if (comp == null) continue;

                foreach (var fieldEntry in compData.fields)
                {
                    if (fieldEntry.isRef) continue; // 值类型阶段跳过引用字段

                    var fieldInfo = compType.GetField(fieldEntry.fieldName,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fieldInfo == null) continue;

                    try
                    {
                        var restored = ParseValue(fieldEntry.value, fieldInfo.FieldType);
                        if (restored != null)
                            fieldInfo.SetValue(comp, restored);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[LevelDeserializer] 还原字段 '{fieldEntry.fieldName}' 失败：{e.Message}");
                    }
                }
            }
        }

        private static object ParseValue(string raw, Type type)
        {
            if (type == typeof(bool))   return bool.Parse(raw);
            if (type == typeof(int))    return int.Parse(raw);
            if (type == typeof(float))  return float.Parse(raw);
            if (type == typeof(string)) return raw;
            if (type == typeof(Vector3))
            {
                var parts = raw.Split(',');
                return new Vector3(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]));
            }
            if (type == typeof(Color))
            {
                var parts = raw.Split(',');
                return new Color(float.Parse(parts[0]), float.Parse(parts[1]),
                                 float.Parse(parts[2]), float.Parse(parts[3]));
            }
            if (type.IsEnum) return Enum.Parse(type, raw);

            Debug.LogWarning($"[LevelDeserializer] 不支持的类型 '{type.Name}'，跳过。");
            return null;
        }

        private static Type FindType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            // 先遍历所有已加载程序集查找
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(typeName);
                if (t != null) return t;
            }
            return null;
        }

        private static void BindRefCollection(
            Component comp,
            FieldInfo fieldInfo,
            Type fieldType,
            List<string> guids,
            Dictionary<string, PlacedObject> dict)
        {
            // 解析元素类型
            Type elemType;
            bool isArray = fieldType.IsArray;
            if (isArray)
                elemType = fieldType.GetElementType();
            else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
                elemType = fieldType.GetGenericArguments()[0];
            else
                return;

            // 构建元素列表
            var resolved = new List<Component>();
            foreach (var guid in guids)
            {
                if (string.IsNullOrEmpty(guid)) { resolved.Add(null); continue; }
                if (!dict.TryGetValue(guid, out var targetPO))
                {
                    Debug.LogWarning($"[LevelDeserializer] 数组字段 '{fieldInfo.Name}' 元素 GUID '{guid}' 未找到，写入 null。");
                    resolved.Add(null);
                    continue;
                }
                var c = targetPO.GetComponentInChildren(elemType);
                if (c == null)
                    Debug.LogWarning($"[LevelDeserializer] 目标 '{targetPO.name}' 上未找到 '{elemType.Name}'，写入 null。");
                resolved.Add(c);
            }

            // 还原为 T[] 或 List<T>
            if (isArray)
            {
                var arr = Array.CreateInstance(elemType, resolved.Count);
                for (int i = 0; i < resolved.Count; i++)
                    arr.SetValue(resolved[i], i);
                fieldInfo.SetValue(comp, arr);
            }
            else
            {
                // 反射构造 List<T> 并逐项 Add
                var list = Activator.CreateInstance(fieldType);
                var addMethod = fieldType.GetMethod("Add");
                foreach (var c in resolved)
                    addMethod.Invoke(list, new object[] { c });
                fieldInfo.SetValue(comp, list);
            }
        }
    }
}
