using System;
using UnityEngine;

namespace LevelEditor.Runtime.Data
{
    /// <summary>
    /// 预制体注册表条目：关卡编辑器面板中一个可放置的预制体。
    /// </summary>
    [Serializable]
    public sealed class PrefabEntry
    {
        /// <summary>全局唯一 Key，序列化时写入 PlacedObjectData.prefabKey</summary>
        public string key;

        /// <summary>对应的预制体资产</summary>
        public GameObject prefab;

        /// <summary>面板显示图标（可为 null，面板降级显示名字）</summary>
        public Texture2D icon;

        /// <summary>分类名，用于面板 Toolbar 过滤</summary>
        public string category;
    }

    /// <summary>
    /// ScriptableObject 预制体注册表。
    /// 在 Assets/LevelEditor/Data/ 下创建一个实例（LevelEditorPrefabRegistry.asset）。
    /// Editor 和 Runtime 均可引用此注册表查找预制体。
    /// </summary>
    [CreateAssetMenu(
        menuName = "关卡编辑器/预制体注册表",
        fileName = "LevelEditorPrefabRegistry")]
    public sealed class LevelEditorPrefabRegistry : ScriptableObject
    {
        [SerializeField]
        private PrefabEntry[] m_Entries = Array.Empty<PrefabEntry>();

        /// <summary>所有注册的预制体条目（只读视图）</summary>
        public PrefabEntry[] Entries => m_Entries;

        /// <summary>
        /// 通过 key 查找条目，未找到返回 null（不抛异常）。
        /// </summary>
        public PrefabEntry FindByKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var entry in m_Entries)
            {
                if (entry != null && entry.key == key)
                    return entry;
            }
            return null;
        }
    }
}
