# Tiled地图字段规范

## 坐标转换

```text
Game_X = Tiled_Pixel_X / 32 - 60
Game_Y = 45 - Tiled_Pixel_Y / 32
Game_Rotation_Z = -Tiled_Rotation
```

地图尺寸固定为120×90格，每格32px，对应游戏内1m。游戏坐标原点位于地图中心。

矩形和椭圆对象的`x/y`是左上角，`width/height`是尺寸。Polyline和Polygon的点坐标相对于对象自身`x/y`。

## 顶层属性

| 属性 | 类型 | 当前值 | 用途 |
|---|---|---|---|
| `worldUnitsPerTile` | float | 1 | 一格对应的游戏单位 |
| `scatterMode` | string | RandomWalkableArea | 全图可行走区域随机散落 |
| `scatterClearanceM` | float | 1 | 障碍、边缘安全距离 |
| `scatterAvoidWater` | bool | true | 初始散落点避开水域 |
| `scatterAvoidObstacles` | bool | true | 初始散落点避开障碍 |

叶子数、杂物数、类型比例和时间限制不属于地图数据。

## 图层契约

| 图层 | 必需 | 用途 |
|---|---|---|
| `10_Regions` | 否 | 空间职责与导航元数据 |
| `20_River` | 是 | 河流形状与清理判定 |
| `25_Lakes` | 否 | 湖泊形状与清理判定 |
| `40_Obstacles` | 否 | 阻挡物和随机散落排除物 |
| `50_Decorations` | 否 | 非阻挡装饰 |
| `60_Landmarks` | 否 | 远景地标 |
| `70_GameplayPoints` | 建议 | 镜头、风眼等点位 |
| `90_Notes` | 否 | 策划文字，程序忽略 |

禁止添加`LeafSpawnArea`对象或叶子生成区图层。

## RiverPath

形状：Polyline。

| 属性 | 类型 | 默认 | 说明 |
|---|---|---|---|
| `widthM` | float | 8 | 河流有效宽度 |
| `isCleanupTarget` | bool | true | 是否参与清理判定 |
| `acceptsLeaves` | bool | true | 是否接收叶子 |
| `acceptsDebris` | bool | true | 是否接收杂物 |
| `collectorMarginM` | float | 1 | 岸边向内收缩距离 |

推荐判定方式：物体中心到平滑河流中心线的最短距离，小于`widthM/2 - collectorMarginM`时进入有效水面。

## Lake

形状：Ellipse或Polygon。

| 属性 | 类型 | 默认 | 说明 |
|---|---|---|---|
| `lakeId` | string | Lake | 湖泊唯一策划ID |
| `isCleanupTarget` | bool | true | 是否参与清理判定 |
| `acceptsLeaves` | bool | true | 是否接收叶子 |
| `acceptsDebris` | bool | true | 是否接收杂物 |
| `collectorMarginM` | float | 1 | 湖岸向内收缩距离 |

椭圆湖使用`x/y/width/height`构造判定形状；多边形湖使用`polygon`点集。有效判定边界应向内收缩`collectorMarginM`。

## GameplayRegion

| 属性 | 类型 | 说明 |
|---|---|---|
| `regionId` | string | 稳定区域ID |
| `difficulty` | int | 空间复杂度标记，不控制生成 |
| `areaRole` | string | Tutorial、OpenTraversal等空间职责 |

## Obstacle与Decoration

`prefabKey`记录对应的游戏资源名。`Obstacle.blocksLeaf=true`时，应同时阻挡叶子、杂物和随机散落点。

## 随机散落算法

可行走随机集合定义为：

```text
地图矩形
- 河流形状
- 湖泊形状
- 障碍物形状
- 地图边缘与障碍物周围的scatterClearanceM
```

在该集合中均匀采样。数量和物体类型由游戏模式传入，而不是从地图读取。

## AI或程序校验清单

1. JSON可以解析。
2. `width=120`且`height=90`。
3. `tilewidth=tileheight=32`。
4. 标准图层名称保持不变。
5. 不存在叶子生成区图层或`LeafSpawnArea`类型。
6. 河流至少包含两个点。
7. 湖泊全部位于地图边界内。
8. 河流与湖泊的清理判定字段齐全。
9. 所有`prefabKey`可映射到实际资源。
10. 对象ID不重复，`nextobjectid`大于最大对象ID。
