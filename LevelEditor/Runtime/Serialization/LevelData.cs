using System;
using System.Collections.Generic;
using UnityEngine;

namespace LevelEditor.Runtime.Serialization
{
    /// <summary>
    /// 轻量 Vector3 序列化代理，避免 JsonUtility 对 Vector3 的依赖问题。
    /// </summary>
    [Serializable]
    public sealed class SerializableVector3
    {
        public float x;
        public float y;
        public float z;

        public SerializableVector3() { }

        public SerializableVector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static SerializableVector3 FromVector3(Vector3 v) =>
            new SerializableVector3(v.x, v.y, v.z);

        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    /// <summary>
    /// 单个字段的序列化条目。
    /// value 存放值类型的字符串表示，或单引用目标对象的 GUID（isRef=true 时）。
    /// refValues 存放数组/List 引用各元素的 GUID 列表（非空时表示数组引用）。
    /// </summary>
    [Serializable]
    public sealed class FieldEntry
    {
        /// <summary>字段名（FieldInfo.Name）</summary>
        public string fieldName;

        /// <summary>字段完整类型名（Type.AssemblyQualifiedName 简化版，仅存 FullName）</summary>
        public string typeName;

        /// <summary>序列化值：值类型时为字符串化结果，单引用类型时为目标 PlacedObject 的 GUID</summary>
        public string value;

        /// <summary>true 表示此字段是 [LevelSerializeRef]，value 或 refValues 为 GUID</summary>
        public bool isRef;

        /// <summary>数组/List 引用各元素的 GUID 列表；非空时为数组引用，忽略 value</summary>
        public List<string> refValues = new List<string>();
    }

    /// <summary>
    /// 单个 Component 的序列化数据。
    /// </summary>
    [Serializable]
    public sealed class ComponentData
    {
        /// <summary>Component 的完整类型名（Type.FullName）</summary>
        public string typeName;

        public List<FieldEntry> fields = new List<FieldEntry>();
    }

    /// <summary>
    /// 场景中单个已放置对象的完整序列化数据。
    /// </summary>
    [Serializable]
    public sealed class PlacedObjectData
    {
        /// <summary>PlacedObject 的 GUID，用于跨对象引用绑定</summary>
        public string levelId;

        /// <summary>预制体注册表 Key，加载时用于查找对应预制体</summary>
        public string prefabKey;

        public SerializableVector3 position   = new SerializableVector3();
        public SerializableVector3 eulerAngles = new SerializableVector3();
        public SerializableVector3 scale       = new SerializableVector3(1, 1, 1);

        public List<ComponentData> components = new List<ComponentData>();
    }

    /// <summary>
    /// 关卡根数据，对应一份 JSON 文件。
    /// </summary>
    [Serializable]
    public sealed class LevelData
    {
        public string levelName;
        public string version  = "1.0";
        public string savedAt;

        public List<PlacedObjectData> objects = new List<PlacedObjectData>();
    }
}
