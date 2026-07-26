using UnityEngine;

namespace LevelEditor.Runtime.Core
{
    /// <summary>
    /// 纯静态格点工具类（无 MonoBehaviour，无 Editor API 依赖）。
    /// 提供 XZ 格点吸附、坐标互转、角度吸附等工具方法。
    /// Runtime 和 Editor 均可使用。
    /// </summary>
    public static class LevelGrid3D
    {
        /// <summary>
        /// 将世界坐标 XZ 吸附到格点，Y 保持不变。
        /// </summary>
        /// <param name="worldPos">输入世界坐标</param>
        /// <param name="cellSize">格点尺寸（默认 1f）</param>
        /// <param name="origin">格点原点世界坐标（默认 Vector3.zero）</param>
        /// <returns>吸附后的世界坐标（Y 值不变）</returns>
        public static Vector3 SnapXZ(Vector3 worldPos, float cellSize, Vector3 origin)
        {
            if (cellSize <= 0f) cellSize = 1f;

            float snappedX = Mathf.Round((worldPos.x - origin.x) / cellSize) * cellSize + origin.x;
            float snappedZ = Mathf.Round((worldPos.z - origin.z) / cellSize) * cellSize + origin.z;

            return new Vector3(snappedX, worldPos.y, snappedZ);
        }

        /// <summary>
        /// 将世界坐标转换为格点整数坐标（Y 轴投影到 XZ 平面）。
        /// </summary>
        public static Vector3Int WorldToCoord(Vector3 worldPos, float cellSize, Vector3 origin)
        {
            if (cellSize <= 0f) cellSize = 1f;

            int cx = Mathf.RoundToInt((worldPos.x - origin.x) / cellSize);
            int cz = Mathf.RoundToInt((worldPos.z - origin.z) / cellSize);

            return new Vector3Int(cx, 0, cz);
        }

        /// <summary>
        /// 将格点整数坐标转换为世界坐标中心点（Y 取 origin.y）。
        /// </summary>
        public static Vector3 CoordToWorld(Vector3Int coord, float cellSize, Vector3 origin)
        {
            return new Vector3(
                coord.x * cellSize + origin.x,
                origin.y,
                coord.z * cellSize + origin.z);
        }

        /// <summary>
        /// 将角度吸附到 step 步长的整数倍（例如 step=45 则吸附到 0/45/90/135...）。
        /// </summary>
        /// <param name="angle">输入角度（度）</param>
        /// <param name="step">吸附步长（度），默认 45°</param>
        public static float SnapAngle(float angle, float step = 45f)
        {
            if (step <= 0f) step = 45f;
            return Mathf.Round(angle / step) * step;
        }
    }
}
