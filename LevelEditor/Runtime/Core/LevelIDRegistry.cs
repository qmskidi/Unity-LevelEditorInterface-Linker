using System.Collections.Generic;
using UnityEngine;

namespace LevelEditor.Runtime.Core
{
    /// <summary>
    /// 单例 MonoBehaviour，维护场景内所有 PlacedObject 的 GUID → 实例 映射。
    /// Runtime 加载关卡时两遍绑定引用依赖此注册表；Editor 端也可复用。
    /// </summary>
    public sealed class LevelIDRegistry : MonoBehaviour
    {
        private static LevelIDRegistry s_Instance;

        /// <summary>当前场景中的 Registry 单例；Scene 加载时由对象的 Awake 设置。</summary>
        public static LevelIDRegistry Instance => s_Instance;

        private readonly Dictionary<string, PlacedObject> m_Map =
            new Dictionary<string, PlacedObject>();

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Debug.LogWarning("[LevelIDRegistry] 场景中存在多个 LevelIDRegistry，将销毁多余实例。");
                Destroy(gameObject);
                return;
            }
            s_Instance = this;
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
                s_Instance = null;
        }

        /// <summary>注册 PlacedObject。重复注册同一 GUID 会覆盖（警告后更新）。</summary>
        public void Register(PlacedObject po)
        {
            if (po == null) return;
            string id = po.LevelId;
            if (string.IsNullOrEmpty(id)) return;

            if (m_Map.TryGetValue(id, out var existing) && existing != po)
                Debug.LogWarning($"[LevelIDRegistry] GUID 冲突：{id}，旧对象 '{existing?.name}' 被新对象 '{po.name}' 覆盖。");

            m_Map[id] = po;
        }

        /// <summary>注销 PlacedObject。</summary>
        public void Unregister(PlacedObject po)
        {
            if (po == null) return;
            string id = po.LevelId;
            if (string.IsNullOrEmpty(id)) return;

            if (m_Map.TryGetValue(id, out var existing) && existing == po)
                m_Map.Remove(id);
        }

        /// <summary>
        /// 通过 GUID 查找 PlacedObject。未找到返回 null（不抛异常）。
        /// </summary>
        public PlacedObject Resolve(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            m_Map.TryGetValue(guid, out var result);
            return result;
        }

        /// <summary>当前注册数量，供调试用。</summary>
        public int Count => m_Map.Count;
    }
}
