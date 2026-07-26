using System.Reflection;
using LevelEditor.Runtime.Core;
using UnityEngine;

namespace LevelEditor.Editor.Connections
{
    /// <summary>
    /// 单个引用端口的数据描述。
    /// 对应 PlacedObject 上某个 Component 的一个 [LevelSerializeRef] 字段。
    /// </summary>
    public sealed class ConnectionPort
    {
        // 预设端口颜色（按字段索引循环使用）
        private static readonly Color[] k_PortColors =
        {
            new Color(1.00f, 0.35f, 0.35f), // 红
            new Color(0.35f, 1.00f, 0.50f), // 绿
            new Color(0.35f, 0.70f, 1.00f), // 蓝
            new Color(1.00f, 0.90f, 0.25f), // 黄
            new Color(0.85f, 0.35f, 1.00f), // 紫
            new Color(0.25f, 1.00f, 0.90f), // 青
        };

        // ── 数据 ────────────────────────────────────────────────────────────

        /// <summary>端口所属的 PlacedObject</summary>
        public PlacedObject Source     { get; }

        /// <summary>端口所属的 Component</summary>
        public Component    Owner      { get; }

        /// <summary>对应的字段</summary>
        public FieldInfo    Field      { get; }

        /// <summary>字段名（显示用）</summary>
        public string       FieldName  => Field.Name;

        /// <summary>端口颜色（按索引分配）</summary>
        public Color        PortColor  { get; }

        /// <summary>字段在该对象所有端口中的顺序索引（用于垂直排布）</summary>
        public int          PortIndex  { get; }

        /// <summary>该端口在所有端口列表中的唯一视觉槽位（用于 Z 轴间距，每个端口独占一槽）</summary>
        public int          VisualSlot { get; }

        /// <summary>
        /// 数组元素索引：
        ///   -1 = 单引用字段（非数组）
        ///   ≥0 = 数组中第 N 个元素
        ///   int.MaxValue = 数组"添加新元素"槽（+ 按钮）
        /// </summary>
        public int          ArrayElementIndex { get; }

        /// <summary>是否为数组/List 字段的端口</summary>
        public bool         IsArrayPort => ArrayElementIndex != -1;

        /// <summary>是否为"添加新元素"槽</summary>
        public bool         IsAddSlot   => ArrayElementIndex == int.MaxValue;

        // ── 构造 ────────────────────────────────────────────────────────────

        /// <summary>单引用端口（arrayElementIndex = -1）</summary>
        public ConnectionPort(PlacedObject source, Component owner, FieldInfo field, int portIndex, int visualSlot)
            : this(source, owner, field, portIndex, visualSlot, -1) { }

        /// <summary>数组元素端口（arrayElementIndex ≥ 0）或添加槽（int.MaxValue）</summary>
        public ConnectionPort(PlacedObject source, Component owner, FieldInfo field, int portIndex, int visualSlot, int arrayElementIndex)
        {
            Source             = source;
            Owner              = owner;
            Field              = field;
            PortIndex          = portIndex;
            VisualSlot         = visualSlot;
            ArrayElementIndex  = arrayElementIndex;
            PortColor          = k_PortColors[portIndex % k_PortColors.Length];
        }

        // ── 位置计算 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 计算端口在世界坐标中的位置。
        /// 偏移到包围盒顶面右侧（+X 方向），避开中心的移动浮标；
        /// 多端口沿 Z 轴依次排列。
        /// </summary>
        public Vector3 GetWorldPosition()
        {
            var renderers = Source.GetComponentsInChildren<Renderer>();
            Bounds bounds = new Bounds(Source.transform.position, Vector3.one);

            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
            }

            float spacing  = 0.4f;
            // X：右移到包围盒右边缘再外扩 0.5，远离中心浮标
            float xOffset  = bounds.max.x + 0.5f;
            // Y：顶面略上方
            float y        = bounds.max.y + 0.2f;
            // Z：每个端口独占唯一视觉槽位，保证数组元素互相分开
            float z        = bounds.center.z + VisualSlot * spacing;

            return new Vector3(xOffset, y, z);
        }

        /// <summary>获取当前引用目标（可为 null）；数组字段取对应元素，添加槽返回 null</summary>
        public Component GetTarget()
        {
            if (ArrayElementIndex < 0)
                return Field.GetValue(Owner) as Component;

            if (IsAddSlot) return null;

            var raw = Field.GetValue(Owner);
            if (raw is System.Collections.IList list && ArrayElementIndex < list.Count)
                return list[ArrayElementIndex] as Component;

            return null;
        }

        /// <summary>标签显示名：数组元素加下标，添加槽显示 "+"</summary>
        public string DisplayName =>
            IsAddSlot          ? $"{Field.Name} [+]" :
            ArrayElementIndex >= 0 ? $"{Field.Name} [{ArrayElementIndex}]" :
                                  Field.Name;
    }
}
