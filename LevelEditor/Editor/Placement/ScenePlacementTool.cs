using LevelEditor.Runtime.Core;
using LevelEditor.Runtime.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LevelEditor.Editor.Placement
{
    /// <summary>
    /// SceneView 内预制体放置工具。
    /// 激活后显示半透明 Ghost 跟随鼠标，XZ 吸附格点，Y 轴 Raycast 贴地，左键确认放置，右键/Escape 取消。
    /// 由 LevelEditorOrchestrator 在 duringSceneGui 中调用 OnSceneGui()。
    /// </summary>
    public static class ScenePlacementTool
    {
        // ── 状态 ───────────────────────────────────────────────────────────────

        private static bool         s_IsPlacing;
        private static PrefabEntry  s_CurrentEntry;
        private static GameObject   s_Ghost;
        private static float        s_YOffset;      // 手动 Y 轴偏移（叠加在 Raycast 贴地之上）

        // Ghost 专用层，避免 Raycast 打到自身
        private const string k_GhostLayerName = "Ignore Raycast";

        public static bool IsPlacing => s_IsPlacing;

        // ── 公开接口 ───────────────────────────────────────────────────────────

        /// <summary>开始放置指定预制体条目，进入 Ghost 跟随模式。</summary>
        public static void BeginPlace(PrefabEntry entry)
        {
            if (entry?.prefab == null)
            {
                Debug.LogWarning("[ScenePlacementTool] entry 或 prefab 为 null，无法开始放置。");
                return;
            }

            CancelPlace(); // 清理可能存在的旧 Ghost

            s_CurrentEntry = entry;
            s_Ghost = Object.Instantiate(entry.prefab);
            s_Ghost.name = $"[Ghost] {entry.prefab.name}";

            // 禁用 Ghost 上所有 Collider，设置为 Ignore Raycast 层
            int ignoreLayer = LayerMask.NameToLayer(k_GhostLayerName);
            SetLayerRecursive(s_Ghost, ignoreLayer);
            DisableColliders(s_Ghost);

            // 半透明材质覆盖
            ApplyGhostMaterial(s_Ghost);

            // 标记为非持久化，避免误保存
            s_Ghost.hideFlags = HideFlags.DontSave;

            s_IsPlacing = true;
        }

        /// <summary>取消放置，销毁 Ghost。</summary>
        public static void CancelPlace()
        {
            if (s_Ghost != null)
                Object.DestroyImmediate(s_Ghost);

            s_Ghost        = null;
            s_CurrentEntry = null;
            s_IsPlacing    = false;
            s_YOffset      = 0f;
        }

        /// <summary>由 Orchestrator 在 duringSceneGui 中每帧调用。</summary>
        public static void OnSceneGui(SceneView sceneView)
        {
            if (!s_IsPlacing || s_Ghost == null) return;

            var evt      = Event.current;
            var settings = Core.LevelEditorSettings.Instance;
            float cellSize  = settings != null ? settings.cellSize  : 1f;
            float snapAngle = settings != null ? settings.snapAngle : 45f;
            float yStep     = settings != null ? settings.yOffsetStep : 0.25f;
            var   origin    = settings != null ? settings.gridOrigin : Vector3.zero;
            int   placeMask = settings != null ? (int)settings.placementLayerMask : ~0;

            // 每帧更新 Ghost 位置
            UpdateGhostPosition(evt, cellSize, origin, placeMask);

            // 滚轮：Shift+滚轮 → 调整 Y 轴偏移；普通滚轮 → 旋转 Y 轴
            if (evt.type == EventType.ScrollWheel)
            {
                if (evt.shift)
                {
                    // Shift+滚轮：上移 / 下移（步长从格点配置读取）
                    // delta.y 在按住 Shift 时可能被系统转到 delta.x，取两轴中绝对值更大的那个
                    float scrollDelta = Mathf.Abs(evt.delta.x) > Mathf.Abs(evt.delta.y)
                        ? evt.delta.x
                        : evt.delta.y;
                    s_YOffset += scrollDelta > 0 ? -yStep : yStep;
                    // 允许负值（嵌入地面），不做钳制
                }
                else
                {
                    // 普通滚轮：旋转 Y 轴（角度吸附）
                    float delta = evt.delta.y > 0 ? snapAngle : -snapAngle;
                    var euler   = s_Ghost.transform.eulerAngles;
                    euler.y     = LevelGrid3D.SnapAngle(euler.y + delta, snapAngle);
                    s_Ghost.transform.eulerAngles = euler;
                }
                evt.Use();
            }

            // 左键确认放置
            if (evt.type == EventType.MouseDown && evt.button == 0 && !evt.alt)
            {
                ConfirmPlace();
                evt.Use();
                return;
            }

            // 右键或 Escape 取消
            if ((evt.type == EventType.MouseDown && evt.button == 1) ||
                (evt.type == EventType.KeyDown   && evt.keyCode == KeyCode.Escape))
            {
                CancelPlace();
                evt.Use();
                return;
            }

            // 持续重绘 SceneView，使 Ghost 跟随鼠标流畅
            sceneView.Repaint();
        }

        // ── 私有工具 ───────────────────────────────────────────────────────────

        private static void UpdateGhostPosition(Event evt, float cellSize, Vector3 origin, int layerMask)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);

            // Raycast 贴地（排除 Ignore Raycast 层）
            int mask = layerMask & ~(1 << LayerMask.NameToLayer(k_GhostLayerName));
            Vector3 basePos;
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, mask))
            {
                basePos = LevelGrid3D.SnapXZ(hit.point, cellSize, origin);
            }
            else
            {
                // 没有命中地面：投影到 Y=origin.y 平面
                float t = ray.direction.y != 0f
                    ? (origin.y - ray.origin.y) / ray.direction.y
                    : 0f;
                Vector3 worldPos = t > 0f
                    ? ray.origin + ray.direction * t
                    : ray.origin + ray.direction * 10f;
                basePos = LevelGrid3D.SnapXZ(worldPos, cellSize, origin);
            }

            // 叠加手动 Y 偏移
            s_Ghost.transform.position = basePos + new Vector3(0f, s_YOffset, 0f);
        }

        private static void ConfirmPlace()
        {
            if (s_CurrentEntry?.prefab == null || s_Ghost == null) return;

            // 先把需要的引用保存到局部变量，CancelPlace() 会清空静态字段
            var        prefab = s_CurrentEntry.prefab;
            string     key    = s_CurrentEntry.key;
            Vector3    pos    = s_Ghost.transform.position;
            Quaternion rot    = s_Ghost.transform.rotation;

            CancelPlace(); // 销毁 Ghost，清空 s_CurrentEntry / s_Ghost

            // 使用 PrefabUtility 保留预制体连接
            var go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (go == null)
            {
                Debug.LogWarning($"[ScenePlacementTool] PrefabUtility.InstantiatePrefab 返回 null，key='{key}'");
                return;
            }

            go.transform.SetPositionAndRotation(pos, rot);

            // 添加 PlacedObject 标记组件
            var po = go.GetComponent<PlacedObject>();
            if (po == null) po = go.AddComponent<PlacedObject>();
            po.PrefabKey = key;

            // 注册 Undo
            Undo.RegisterCreatedObjectUndo(go, $"放置 {key}");

            // 标记场景脏
            EditorSceneManager.MarkSceneDirty(go.scene);

            // 自动选中放置的对象
            Selection.activeGameObject = go;
        }

        private static void ApplyGhostMaterial(GameObject root)
        {
            // 构建半透明材质（URP Lit，Transparent 模式）
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard"); // 回退到 Standard

            Material ghostMat = null;
            if (shader != null)
            {
                ghostMat = new Material(shader);
                if (shader.name.Contains("Universal"))
                {
                    // URP 透明设置
                    ghostMat.SetFloat("_Surface", 1f);      // 1 = Transparent
                    ghostMat.SetFloat("_Blend", 0f);        // Alpha blend
                    ghostMat.SetFloat("_AlphaClip", 0f);
                    ghostMat.renderQueue = 3000;
                    ghostMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    ghostMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    ghostMat.SetInt("_ZWrite", 0);
                    ghostMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                }
                else
                {
                    // Standard 透明设置
                    ghostMat.SetFloat("_Mode", 3f);
                    ghostMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    ghostMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    ghostMat.SetInt("_ZWrite", 0);
                    ghostMat.DisableKeyword("_ALPHATEST_ON");
                    ghostMat.EnableKeyword("_ALPHABLEND_ON");
                    ghostMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    ghostMat.renderQueue = 3000;
                }
                ghostMat.color = new Color(0.4f, 0.8f, 1f, 0.4f); // 半透明蓝色
            }

            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = ghostMat;
                renderer.materials = mats;
            }
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        private static void DisableColliders(GameObject go)
        {
            foreach (var col in go.GetComponentsInChildren<Collider>())
                col.enabled = false;
        }
    }
}
