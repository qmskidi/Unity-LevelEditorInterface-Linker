using System;

namespace LevelEditor.Runtime.Serialization
{
    /// <summary>
    /// 标注需要序列化的值类型字段（bool/int/float/string/Vector3/Color/Enum）。
    /// 在任意 MonoBehaviour 的 public 字段上添加此 Attribute 即可接入关卡序列化引擎。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class LevelSerializeAttribute : Attribute { }

    /// <summary>
    /// 标注需要序列化的引用类型字段（Component 子类引用）。
    /// 引用将被序列化为目标对象的 PlacedObject GUID 字符串，加载时两遍绑定还原。
    /// 注意：目标对象必须携带 PlacedObject 组件，否则序列化时会跳过并输出 Warning。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class LevelSerializeRefAttribute : Attribute { }
}
