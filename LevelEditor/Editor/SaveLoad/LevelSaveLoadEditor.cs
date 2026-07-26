using System.IO;
using LevelEditor.Runtime.Core;
using LevelEditor.Runtime.Data;
using LevelEditor.Runtime.Serialization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LevelEditor.Editor.SaveLoad
{
    /// <summary>
    /// 关卡保存 / 加载（Editor 侧）。
    ///
    ///   SaveLevel(name)  — 序列化当前场景 PlacedObject → JSON → Assets/Levels/
    ///   LoadLevel(path)  — 读取 JSON → PrefabUtility.InstantiatePrefab → 两遍绑定引用
    ///
    /// 与 Runtime/LevelLoader.cs 的区别：
    ///   LoadLevel 使用 PrefabUtility.InstantiatePrefab，保持 Prefab 连接；
    ///   Runtime 侧使用 Object.Instantiate（无 Prefab 连接，正式游戏用）。
    /// </summary>
    public static class LevelSaveLoadEditor
    {
        private const string k_LevelsDir = "Assets/Levels";

        // ── 保存 ─────────────────────────────────────────────────────────────

        public static void SaveLevel(string levelName)
        {
            if (string.IsNullOrWhiteSpace(levelName))
            {
                EditorUtility.DisplayDialog("保存失败", "关卡名称不能为空。", "OK");
                return;
            }

            // 序列化
            LevelData data = LevelSerializer.Serialize(levelName);

            if (data.objects.Count == 0)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "保存确认",
                    "当前场景中没有 PlacedObject，将保存空关卡。继续？",
                    "继续", "取消");
                if (!proceed) return;
            }

            // 确保输出目录存在
            if (!Directory.Exists(k_LevelsDir))
                Directory.CreateDirectory(k_LevelsDir);

            string filePath = $"{k_LevelsDir}/{levelName}.json";
            string json     = JsonUtility.ToJson(data, prettyPrint: true);

            File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);

            AssetDatabase.Refresh();

            Debug.Log($"[LevelSaveLoadEditor] 关卡已保存：{filePath}（共 {data.objects.Count} 个对象）");

            EditorUtility.DisplayDialog(
                "保存成功",
                $"已写入：{filePath}\n共 {data.objects.Count} 个对象。",
                "OK");
        }

        // ── 加载 ─────────────────────────────────────────────────────────────

        public static void LoadLevel(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                EditorUtility.DisplayDialog("加载失败", $"文件不存在：\n{filePath}", "OK");
                return;
            }

            // 读取并解析 JSON
            string   json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            LevelData data;
            try
            {
                data = JsonUtility.FromJson<LevelData>(json);
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("加载失败", $"JSON 解析错误：\n{ex.Message}", "OK");
                return;
            }

            if (data == null || data.objects == null)
            {
                EditorUtility.DisplayDialog("加载失败", "JSON 文件格式无效。", "OK");
                return;
            }

            // 加载预制体注册表
            var registry = LoadRegistry();
            if (registry == null)
            {
                EditorUtility.DisplayDialog(
                    "加载失败",
                    "未找到 LevelEditorPrefabRegistry。\n请先在 Assets/LevelEditor/Data/ 下创建注册表。",
                    "OK");
                return;
            }

            // 询问是否清空现有 PlacedObject
            var existing = Object.FindObjectsOfType<PlacedObject>();
            if (existing.Length > 0)
            {
                bool clear = EditorUtility.DisplayDialog(
                    "加载关卡",
                    $"场景中已有 {existing.Length} 个 PlacedObject。\n" +
                    "是否先删除现有对象再加载？",
                    "删除并加载", "追加加载");

                if (clear)
                    DestroyExistingPlacedObjects(existing);
            }

            // 第一遍：实例化所有对象（Editor 侧用 PrefabUtility）
            var guidMap = InstantiateAllEditor(data, registry);

            // 第二遍：绑定引用
            LevelDeserializer.BindReferences(data, guidMap);

            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log($"[LevelSaveLoadEditor] 关卡加载完成：{filePath}（共 {guidMap.Count} 个对象）");

            EditorUtility.DisplayDialog(
                "加载成功",
                $"已加载：{data.levelName}\n共 {guidMap.Count} 个对象。",
                "OK");
        }

        // ── 私有工具 ──────────────────────────────────────────────────────────

        private static void DestroyExistingPlacedObjects(PlacedObject[] objects)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("清空现有关卡对象");
            int group = Undo.GetCurrentGroup();

            foreach (var po in objects)
            {
                if (po != null)
                    Undo.DestroyObjectImmediate(po.gameObject);
            }

            Undo.CollapseUndoOperations(group);
        }

        private static System.Collections.Generic.Dictionary<string, PlacedObject>
            InstantiateAllEditor(LevelData data, LevelEditorPrefabRegistry registry)
        {
            var guidMap = new System.Collections.Generic.Dictionary<string, PlacedObject>();

            // 用于整组 Undo 支持
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName($"加载关卡 {data.levelName}");
            int group = Undo.GetCurrentGroup();

            foreach (var objData in data.objects)
            {
                if (objData == null) continue;

                var entry = registry.FindByKey(objData.prefabKey);
                if (entry == null || entry.prefab == null)
                {
                    Debug.LogWarning($"[LevelSaveLoadEditor] 未找到预制体 '{objData.prefabKey}'，跳过该对象。");
                    continue;
                }

                // 使用 PrefabUtility 保持 Prefab 连接
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab);
                if (instance == null) continue;

                Undo.RegisterCreatedObjectUndo(instance, $"实例化 {objData.prefabKey}");

                // 还原 Transform
                instance.transform.position    = objData.position.ToVector3();
                instance.transform.eulerAngles = objData.eulerAngles.ToVector3();
                instance.transform.localScale  = objData.scale.ToVector3();
                instance.name = entry.prefab.name;

                // 确保有 PlacedObject 组件
                var po = instance.GetComponent<PlacedObject>();
                if (po == null)
                    po = Undo.AddComponent<PlacedObject>(instance);

                // 注入 GUID（通过反射写入 private 字段）
                var levelIdField = typeof(PlacedObject).GetField(
                    "m_LevelId",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                levelIdField?.SetValue(po, objData.levelId);

                po.Reinitialize(objData.prefabKey);

                // 还原值字段
                LevelDeserializer.RestoreValueFieldsStatic(objData, instance);

                if (!string.IsNullOrEmpty(objData.levelId))
                    guidMap[objData.levelId] = po;
            }

            Undo.CollapseUndoOperations(group);

            return guidMap;
        }

        private static LevelEditorPrefabRegistry LoadRegistry()
        {
            var reg = AssetDatabase.LoadAssetAtPath<LevelEditorPrefabRegistry>(
                "Assets/LevelEditor/Data/LevelEditorPrefabRegistry.asset");

            if (reg == null)
            {
                var guids = AssetDatabase.FindAssets("t:LevelEditorPrefabRegistry");
                if (guids.Length > 0)
                    reg = AssetDatabase.LoadAssetAtPath<LevelEditorPrefabRegistry>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            return reg;
        }
    }
}
