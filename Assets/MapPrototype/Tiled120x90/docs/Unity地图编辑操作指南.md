# Unity 地图编辑操作指南

## 地图资源关系

- `map_120x90_v1.tmj` 是首版地图的唯一布局数据源，PNG/SVG 只用于对照。
- `Assets/Prefabs/Levels` 下的三个 `Level_*.prefab` 是彼此独立的地图副本；修改其中一个不会同步到另外两个。
- 三种模式当前地图相同，但树叶数量、限时和无尽参数仍各自保存在对应的 `LevelRoot` 中。
- 不要重新添加 `RiverPath2D`、LineRenderer 河流或固定 `LeafSpawnArea`。

## 日常人工调整

1. 在 Project 窗口打开 `Assets/Prefabs/Levels`。
2. 双击需要修改的 `Level_*.prefab` 进入 Prefab Mode。
3. 展开以下分组：
   - `GroundGrid`：黄绿混合地面。
   - `Water/MainRiver_Whole`：单张整河图片、真实水色遮罩与动态水流。
   - `Water/Lake_SouthWetlandLake`：湖泊判定和 `Pond_01` 外观。
   - `Obstacles`：有物理/生成排除判定的物件。
   - `Decorations`、`Landmarks`：默认不阻挡的人工摆放物件。
   - `Boundaries`：不可见地图边界，不建议移动。
4. 修改完成后保存 Prefab；不要使用“Force Rebuild”菜单，除非确定要丢弃这些人工调整。

## 调整河流图片

- `Water/MainRiver_Whole` 是 `MainRiverWhole.prefab` 的唯一整河实例，可移动、旋转和等比缩放。
- 选中整河后，Scene 视图中的青色圆点是入口，橙色圆点是出口；两点应分别与 TMJ 路线首尾重合。
- 点击 `Scan Entry / Exit From Water Pixels` 可从整河源图的蓝色水域重新计算入口、出口和原始水宽。
- 调整时保持 X/Y 缩放一致，不要镜像。动态水纹会读取 Level 根对象的 `MapPrototypeGizmos` 折线路线并平滑转向。
- `RiverArt_01/02/03` 和旧分段 Prefab 仅用于回退，不要实例化到当前三个关卡中。
- 图片上的水色遮罩同时控制树叶收集与随机生成排除区域；不要移除 `RiverWaterMask`、`RiverCollector` 或碰撞体。

## 调整黄绿地面

- 选中 `GroundGrid/GroundTilemap`，在 `GroundTilemapGenerator` 中修改：
  - `Seed`：改变色块分布。
  - `Patch World Size`：数值越大，连续色块越大。
  - `Region Hints`：正数偏绿，负数偏黄。
- 点击 `Rebuild Mixed Ground Tilemap` 将结果写入当前关卡预制体。
- 生成器始终把黄绿 Tile 数量保持为 50/50（最多相差一块），并且不会旋转或翻转图片。
- 当前草地图本身不是无缝纹理，因此放大观察时可能看到 Tile 边界；本版不修改原始图片。

## 调整物件与阻挡

- `Obstacles` 中的物件带 `SpawnExclusion`，树木还带 `TreeTrunkCollider`；移动父物件时这些判定会一起移动。
- `Decorations` 和 `Landmarks` 用于纯视觉摆放。若需要它们阻挡树叶，应把对象移到 `Obstacles` 并补齐相同的碰撞/排除结构。
- 湖泊的真正水域判定在 `Lake_SouthWetlandLake` 根对象的椭圆 Collider 上；`Pond_01_Visual` 只负责显示。
- Level 根对象上的 `MapPrototypeGizmos` 保存 TMJ 河流路线、A/B/C/D 区域以及 CameraStart/WindStart。选中根对象即可显示参考线，运行时不会显示。

## 树叶随机生成

- 树叶从整个 `LevelRoot.MapBounds` 随机生成，不存在固定生成区对象。
- 候选点必须离地图边界、水域和障碍物至少 1 米。
- 若控制台出现 `spawned X/Y` 警告，说明可行走区域过少；优先检查大型 `SpawnExclusion`、水域碰撞体或关卡边界。
- 树叶仍由 `Leaf.prefab` 实例化，外观随机选择四张叶片图片，并随机旋转、尺寸和重量。

## 从 TMJ 重新导入

- `Tools > Fallen Leaves > Import Map Prototype`
  - 校验 TMJ、基础图片和 Catalog。
  - 已存在的三个关卡全部跳过，不覆盖人工布局。
- `Tools > Fallen Leaves > Force Rebuild Three Levels From TMJ`
  - 覆盖三个关卡预制体，恢复为 TMJ 的首版布局。
  - 使用前先创建 Git 提交或备份；该操作会丢失三关的人工摆放调整。
- 批处理前必须关闭所有打开该项目的 Unity 编辑器窗口。
- `Tools > Fallen Leaves > Capture Level Prefab Overview` 会把全图预览输出到项目同级 `logs/level-prefab-overview.png`。

## Git 回退建议

- 地图调整前先执行 `git status`，确保没有不相关文件混入。
- 只暂存目标 `Level_*.prefab` 和相关脚本/资源，不要提交 `.vscode/`。
- 若需要恢复某一关，优先通过 Git 仅恢复对应 prefab；不要对整个仓库执行硬重置。
