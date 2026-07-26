using LevelEditor.Runtime.Data;
using LevelEditor.Runtime.Serialization;
using UnityEngine;

namespace LevelEditor.Runtime
{
    /// <summary>
    /// Runtime 侧关卡加载入口，供正式游戏（Build 后）还原关卡使用。
    /// Editor 侧的保存/加载请使用 LevelSaveLoadEditor（位于 Editor/ 目录）。
    /// </summary>
    public static class LevelLoader
    {
        /// <summary>
        /// 从 TextAsset（JSON）加载关卡并实例化所有对象。
        /// </summary>
        /// <param name="jsonAsset">放置于 Resources 或 Addressables 中的关卡 JSON 资产</param>
        /// <param name="registry">预制体注册表，需与保存时使用的同一份</param>
        public static void LoadLevel(TextAsset jsonAsset, LevelEditorPrefabRegistry registry)
        {
            if (jsonAsset == null)
            {
                Debug.LogError("[LevelLoader] jsonAsset 为 null，无法加载关卡。");
                return;
            }
            if (registry == null)
            {
                Debug.LogError("[LevelLoader] registry 为 null，无法加载关卡。");
                return;
            }

            LevelData data;
            try
            {
                data = JsonUtility.FromJson<LevelData>(jsonAsset.text);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LevelLoader] JSON 解析失败：{e.Message}");
                return;
            }

            if (data == null)
            {
                Debug.LogError("[LevelLoader] JSON 解析结果为 null。");
                return;
            }

            // 第一遍：实例化所有对象，还原值类型字段
            var dict = LevelDeserializer.InstantiateAll(data, registry);

            // 第二遍：绑定引用字段
            LevelDeserializer.BindReferences(data, dict);

            Debug.Log($"[LevelLoader] 关卡 '{data.levelName}' 加载完成，共 {dict.Count} 个对象。");
        }
    }
}
