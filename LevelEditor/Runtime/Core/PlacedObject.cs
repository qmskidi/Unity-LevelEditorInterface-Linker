using System;
using UnityEngine;

namespace LevelEditor.Runtime.Core
{
    /// <summary>
    /// 挂在每个由关卡编辑器放置的对象上，携带 GUID 和预制体 Key。
    /// OnEnable / OnDisable 时自动向 LevelIDRegistry 注册/注销，供序列化引用绑定使用。
    /// </summary>
    public sealed class PlacedObject : MonoBehaviour
    {
        [SerializeField]
        private string m_LevelId = string.Empty;

        [SerializeField]
        private string m_PrefabKey = string.Empty;

        /// <summary>全局唯一 ID，在对象首次 Awake 时自动生成（若为空）。</summary>
        public string LevelId => m_LevelId;

        /// <summary>预制体注册表 Key，由 ScenePlacementTool 或 Deserializer 赋值。</summary>
        public string PrefabKey
        {
            get => m_PrefabKey;
            set => m_PrefabKey = value;
        }

        private void Awake()
        {
            if (string.IsNullOrEmpty(m_LevelId))
                m_LevelId = Guid.NewGuid().ToString();
        }

        private void OnEnable()
        {
            LevelIDRegistry.Instance?.Register(this);
        }

        private void OnDisable()
        {
            LevelIDRegistry.Instance?.Unregister(this);
        }

        /// <summary>
        /// 加载关卡时由 LevelDeserializer 调用：设置 PrefabKey，保留原 GUID（已由 JSON 还原）。
        /// </summary>
        public void Reinitialize(string prefabKey)
        {
            m_PrefabKey = prefabKey;
            // GUID 已通过 JSON 反序列化还原到 m_LevelId，不重新生成
        }

        /// <summary>
        /// 仅供编辑器工具调用：强制替换 GUID（如 Inspector "重置 GUID" 按钮）。
        /// </summary>
        public void ResetGuid()
        {
            m_LevelId = Guid.NewGuid().ToString();
        }
    }
}
