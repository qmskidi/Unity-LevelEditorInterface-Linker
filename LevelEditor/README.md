# LevelEditor — Unity 关卡编辑器框架

> **适用版本**：Unity 2022.3 LTS · URP  
> **仅限 Editor**：所有 `Editor/` 目录下的脚本不进入 Build，Runtime 序列化层可包含在 Build 中。

---

## 目录结构

```
Assets/LevelEditor/
├── Editor/
│   ├── Core/                  # 总线、设置、格点工具
│   │   ├── LevelEditorOrchestrator.cs
│   │   ├── LevelEditorSettings.cs
│   │   └── LevelManagerBootstrapper.cs
│   ├── Placement/             # Ghost 预制体放置工具
│   │   ├── ScenePlacementTool.cs
│   │   └── GridOverlayDrawer.cs
│   ├── Connections/           # 引用连线系统
│   │   ├── RefDragHandler.cs
│   │   ├── RefConnectionDrawer.cs
│   │   └── ConnectionPort.cs
│   ├── UI/                    # SceneView UI & 预制体面板
│   │   ├── LevelEditorToolbar.cs
│   │   ├── LevelEditorSidePanel.cs
│   │   └── PrefabPaletteWindow.cs
│   ├── ContextMenu/           # 参数弹窗
│   │   ├── SceneParamPopup.cs
│   │   └── SceneContextMenu.cs
│   ├── SaveLoad/              # Editor 侧保存 / 加载
│   │   └── LevelSaveLoadEditor.cs
│   ├── Inspector/
│   │   └── PlacedObjectInspector.cs
│   └── Manipulation/
│       └── ObjectManipulatorTool.cs
└── Runtime/
    ├── Core/
    │   ├── PlacedObject.cs    # 场景对象标记组件（携带 GUID）
    │   ├── LevelGrid3D.cs     # 格点工具（Runtime + Editor 均可用）
    │   └── LevelIDRegistry.cs
    ├── Data/
    │   └── LevelEditorPrefabRegistry.cs   # 预制体注册表 ScriptableObject
    ├── Serialization/
    │   ├── LevelSerializeAttribute.cs     # [LevelSerialize] / [LevelSerializeRef]
    │   ├── LevelData.cs                   # JSON 数据结构
    │   ├── LevelSerializer.cs             # 序列化引擎
    │   └── LevelDeserializer.cs           # 反序列化引擎（两遍）
    └── LevelLoader.cs                     # Runtime 侧关卡加载
```

---

## 快速上手

### 1. 创建资产文件

在 `Assets/LevelEditor/Data/` 目录下（右键 → Create）创建：

| 资产 | 菜单路径 | 文件名 |
|------|---------|--------|
| 预制体注册表 | 关卡编辑器 / 预制体注册表 | `LevelEditorPrefabRegistry.asset` |
| 编辑器设置 | 关卡编辑器 / 编辑器设置 | `LevelEditorSettings.asset` |

### 2. 注册可放置预制体

打开 `LevelEditorPrefabRegistry.asset`，在 `Entries` 数组中添加条目：

| 字段 | 说明 |
|------|------|
| `key` | 全局唯一标识符，序列化时写入 JSON，**修改后已保存关卡将无法还原该对象** |
| `prefab` | 对应的 GameObject 预制体资产 |
| `icon` | 可选，面板图标（缺省时显示 key 文字） |
| `category` | 分类名，用于预制体面板 Toolbar 过滤 |

### 3. 启用编辑器

菜单栏：**Tools → 关卡编辑器 → 启用**（快捷键 `Ctrl+Shift+E`）

启用后 SceneView 中会出现：
- 顶部工具条（保存 / 加载按钮）
- 左下角常驻操作面板
- 格点线叠加层（可在设置中关闭）

---

## 功能说明

### 预制体放置

**打开方式**：  
- 菜单：Tools → 关卡编辑器 → 预制体面板（`Ctrl+Shift+P`）  
- 或通过左下角面板的"放置预制体"区块

**操作**：

| 输入 | 效果 |
|------|------|
| 点击预制体图标 | 进入放置模式，Ghost 跟随鼠标 |
| 移动鼠标 | Ghost XZ 吸附格点，Y 轴 Raycast 贴地 |
| 滚轮 | 绕 Y 轴旋转（步长 = snapAngle，默认 45°） |
| `Shift` + 滚轮 | 调整 Y 轴偏移（步长在 LevelEditorSettings 中配置） |
| 左键 | 确认放置，自动选中新对象 |
| 右键 / `Escape` | 取消放置 |

**Y 轴偏移步长**：在 `LevelEditorSettings.asset` 的 **放置配置 → Y Offset Step** 字段中设置，默认 `0.25`（世界单位）。

---

### 格点配置（LevelEditorSettings）

| 字段 | 默认值 | 说明 |
|------|--------|------|
| Cell Size | 1 | 格点单元格大小（世界单位） |
| Snap Angle | 45° | 滚轮旋转吸附步长 |
| Grid Range | 20 | 格点线显示范围（从中心向外 N 格） |
| Grid Color | 灰色半透明 | 格点线颜色 |
| Grid Origin | (0,0,0) | 格点世界坐标原点 |
| Show Grid | true | 是否显示格点线 |
| Placement Layer Mask | 全部 | 放置 Raycast 检测层（应排除 Ghost 所在的 Ignore Raycast 层） |
| Y Offset Step | 0.25 | Shift+滚轮 Y 轴调整步长 |
| Port Radius | 0.15 | 连线端口圆点半径 |
| Connection Line Width | 2 px | 连线宽度 |

---

### 左下角常驻面板

SceneView 左下角面板分为两个可折叠区块：

**放置预制体**：按分类列出注册表中的所有预制体，点击进入 Ghost 放置模式。底部有"打开预制体面板"按钮可打开图标网格窗口。

**选中对象操作**：当选中携带 `PlacedObject` 组件的对象时激活：

| 按钮 | 功能 |
|------|------|
| 调整参数 | 弹出 PopupWindow，查看 / 修改 [LevelSerialize] 标注的字段 |
| 管理引用 | 进入连线模式，可视化拖拽绑定 [LevelSerializeRef] 字段 |
| 删除对象 | 立即删除并标记场景脏（支持 Undo） |

---

### 引用连线系统

用于在 SceneView 中可视化管理 Component 之间的引用关系。

**触发方式**：选中对象 → 左下角面板 → "管理引用"

**连线模式下**：
- 场景中携带目标类型 Component 的对象会显示白色圆形候选标记
- 从源对象端口拖拽鼠标，悬停到候选目标时高亮为绿色
- 松开鼠标完成引用绑定（支持 Undo）
- 按 `Escape` 退出连线模式

**接入方法**：在任意 MonoBehaviour 中标注 `[LevelSerializeRef]`：

```csharp
using LevelEditor.Runtime.Serialization;

public class MyDoor : MonoBehaviour
{
    [LevelSerializeRef]
    public MyTrigger linkedTrigger;  // 必须是 Component 子类
}
```

> 目标对象必须携带 `PlacedObject` 组件，否则序列化时跳过并输出 Warning。

---

### 序列化系统

#### 值类型字段

标注 `[LevelSerialize]`，支持以下类型：

| 类型 | 序列化格式 |
|------|-----------|
| `bool` | `"True"` / `"False"` |
| `int` | 整数字符串 |
| `float` | Round-trip 精度字符串 |
| `string` | 原始字符串 |
| `Vector3` | `"x,y,z"` |
| `Color` | `"r,g,b,a"` |
| `Enum` | 枚举名称字符串 |

```csharp
using LevelEditor.Runtime.Serialization;

public class SpawnPoint : MonoBehaviour
{
    [LevelSerialize] public int waveIndex;
    [LevelSerialize] public float spawnDelay;
    [LevelSerialize] public SpawnType spawnType;
}
```

#### 引用类型字段

标注 `[LevelSerializeRef]`，序列化为目标对象的 LevelId（GUID），加载时两遍绑定还原：

1. **第一遍**：实例化所有对象，建立 `GUID → PlacedObject` 字典  
2. **第二遍**：遍历所有引用字段，通过字典找到目标对象并绑定 Component

---

### 保存 / 加载

工具条中的按钮（或调用 `LevelSaveLoadEditor` API）：

**保存**：`LevelSaveLoadEditor.SaveLevel(levelName)`  
序列化场景中所有 `PlacedObject`，输出到 `Assets/Levels/<levelName>.json`

**加载**：`LevelSaveLoadEditor.LoadLevel(filePath)`  
读取 JSON，使用 `PrefabUtility.InstantiatePrefab` 实例化（保持 Prefab 连接），支持追加 / 覆盖两种模式。加载操作支持整组 Undo。

**JSON 结构概览**：
```json
{
  "levelName": "Level_01",
  "version": "1.0",
  "savedAt": "2026-07-09 12:00:00",
  "objects": [
    {
      "levelId": "<GUID>",
      "prefabKey": "Door_Wood",
      "position": { "x": 2, "y": 0, "z": 4 },
      "eulerAngles": { "x": 0, "y": 90, "z": 0 },
      "scale": { "x": 1, "y": 1, "z": 1 },
      "components": [
        {
          "typeName": "MyDoor",
          "fields": [
            { "fieldName": "linkedTrigger", "isRef": true, "value": "<target-GUID>" }
          ]
        }
      ]
    }
  ]
}
```

---

### Runtime 加载

运行时（正式游戏）使用 `LevelLoader`（`Object.Instantiate`，无 Prefab 连接）：

```csharp
// 需要注册表和关卡 JSON（通过 Resources 或 Addressables 加载）
LevelLoader.Load(levelData, registry);
```

---

## PlacedObject 组件

`PlacedObject` 是放置对象的必须标记组件：

| 属性 | 说明 |
|------|------|
| `LevelId` | 场景内全局唯一 GUID，Awake 自动生成，序列化时保持不变 |
| `PrefabKey` | 对应注册表中的 `key`，加载时用于查找预制体 |

> 手动放置的对象若需要被关卡系统管理，需手动添加 `PlacedObject` 组件并设置 `PrefabKey`。

---

## 注意事项

- **不要修改已发布关卡中的 `key`**：`prefabKey` 是 JSON 中对象与预制体的唯一绑定，修改后旧存档将无法还原该对象。
- **Ghost 层**：放置 Ghost 对象使用 `Ignore Raycast` 层，Raycast 时自动排除，请勿将场景对象放在此层。
- **`[LevelSerializeRef]` 字段目标必须有 `PlacedObject`**：纯场景对象（非 PlacedObject 管理的）无法被序列化引用。
- **Editor Only**：`Editor/` 下所有脚本不包含在 Build 中；`Runtime/Serialization/` 可按需包含在 Build 中（`LevelLoader` 使用）。
