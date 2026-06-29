# PSD 图片高级效果支持 功能需求文档

> 日期: 2026-06-29 | 状态: 草稿
> 父功能: PSD 转 UGUI 自动化工具链 ([README](./README.md))
> 依赖: [PSD 解析与智能分析管线](./2026-06-24-psd-2-ui-parser-frd.md) (FRD #1)、[Unity Prefab 生成与代码框架](./2026-06-24-psd-2-ui-generator-frd.md) (FRD #3)

## 概述

扩展 PSD 解析器和 Unity 生成器，支持图片对半导出优化（减少大图资源占用）、图片变换效果提取（旋转、缩放、镜像、透明度）、纯色填充效果，实现从 Photoshop 到 Unity uGUI 的图片效果自动化同步，视觉还原度 > 90%。

## 用户故事

| 角色 | 场景 | 目标 |
|------|------|------|
| 程序员 | 拿到美术提供的 PSD，包含大尺寸对称背景图（2048x2048） | 运行工具后，Unity 中自动生成两个 Image 组件显示完整背景，但资源只占用一半大小，优化性能 |
| 程序员 | PSD 中图标旋转了 45°，设置了 50% 透明度 | Unity 中的 Image 自动应用相同旋转和透明度，无需手动调整 RectTransform 参数 |
| 美术 | 需要让一个图标显示为红色（临时调试或主题变体） | 在图层名添加 `.tint`，用纯红色填充图层，工具自动提取颜色并应用到 Unity Image.color |
| 程序员 | PSD 更新后重新生成，某些效果需要保留手动调整 | 挂载 `PSD2UILock` 组件，锁定的对象跳过更新（复用现有迭代维护机制） |
| 程序员 | 查看解析日志，发现某个空图层被标记了 `.hsplit` | 日志清晰记录该图层被跳过，避免生成错误资源 |

## 功能边界

**范围内：**

### 1. 图片对半导出优化（`.hsplit` / `.vsplit`）
- 标签识别：
  - `.hsplit`：水平对半（左右分割）
  - `.vsplit`：垂直对半（上下分割）
- Node.js 解析器行为：
  - `.hsplit`：导出图层的**左半部分**（裁剪宽度为原图 50%）
  - `.vsplit`：导出图层的**上半部分**（裁剪高度为原图 50%）
  - 如果图层已旋转，先导出旋转后的图层，再裁剪一半
  - JSON 中标记 `splitMode: "horizontal"` 或 `"vertical"`
- Unity 生成器行为：
  - 创建两个 Image 组件（子对象，命名：`<LayerName>_Left` 和 `<LayerName>_Right`，或 `_Top` 和 `_Bottom`）
  - 第一个 Image：使用导出的半张图片，位置为原图层左半区域（或上半区域）
  - 第二个 Image：使用同一张图片，通过 `RectTransform.localScale.x = -1`（或 `.y = -1`）实现镜像，位置为原图层右半区域（或下半区域）
  - 两个 Image 组合后视觉上还原完整图层
- 目标：减少大图资源文件大小 50%，优化内存和包体

### 2. 图片变换效果提取（自动识别 PSD 图层变换）
- **旋转**：
  - 提取图层的 `rotation` 参数（角度）
  - Unity 生成器应用到 `RectTransform.localRotation`
- **缩放**：
  - 提取图层的 `scale` 参数（x, y）
  - Unity 生成器应用到 `RectTransform.localScale`
- **镜像**：
  - 提取图层 scale 的负值（如果美术在 PSD 中手动翻转了图层）
  - Unity 生成器应用到 `RectTransform.localScale`（负值实现翻转效果）
- **透明度**：
  - 提取图层的 `opacity` 参数（0-100%）
  - Unity 生成器应用到 `Image.color.a`

### 3. 纯色填充效果（`.tint` 标签）
- 标签识别：`.tint`
- Node.js 解析器行为：
  - 采样图层**中心点像素**的颜色值（RGBA）
  - 假设图层为单色（由美术保证），不做颜色分布分析
  - 将颜色写入 JSON 的 `imageEffects.tint.color`
- Unity 生成器行为：
  - 应用颜色到 `Image.color`（使用 Unity 的 Tint 模式，相当于颜色叠加）

### 4. JSON Schema 扩展
- 在现有 `LayerConfig` 中增加 `imageEffects` 字段：
  ```typescript
  imageEffects?: {
    splitMode?: "horizontal" | "vertical" | null;
    transform: {
      rotation: number;           // 旋转角度（度）
      scale: { x: number; y: number };  // 缩放比例
      flip: { x: boolean; y: boolean }; // 镜像标记（由 scale 负值推导）
    };
    tint?: {
      enabled: boolean;
      color: { r: number; g: number; b: number; a: number }; // 0-1 范围
    };
    opacity: number;              // 透明度 0-1
  }
  ```
- zod 验证规则更新

### 5. 效果组合支持
- 所有效果可自由组合，例如：
  - `img_bg.hsplit.tint`：对半导出 + 纯色填充
  - `img_icon.tint` 图层同时旋转 45°：纯色填充 + 旋转
- 应用顺序：
  1. 对半导出（如果有标签）：先裁剪图层
  2. 变换效果（旋转、缩放、镜像）：应用到 RectTransform
  3. 颜色效果（tint、opacity）：应用到 Image.color

### 6. 边界情况处理
- 空图层或隐藏图层标记 `.hsplit` / `.vsplit` / `.tint`：
  - 跳过导出
  - 生成警告日志：`[WARNING] Layer "<LayerName>" is empty/invisible but marked with .hsplit, skipping export.`
- 非纯色图层标记 `.tint`：
  - 采样中心点颜色（可能不准确）
  - 不报错，不生成警告（假设美术知道自己在做什么）
- 对半标签 + 非对称图片：
  - 信任美术，不做对称性检查
  - 如果图片非对称，镜像后会显示错误（责任由美术承担）

### 7. 命令行接口扩展
- 现有命令：`psd-exporter parse <input.psd> --output <json-path> --assets <assets-dir>`
- 新增行为：自动识别图片效果标签和变换，无需额外参数

**范围外：**
- ❌ 不检查图片对称性（信任美术标记）
- ❌ 不支持复杂混合模式（Multiply、Overlay、Screen 等），tint 仅使用 Normal 模式（Unity Image.color 的默认行为）
- ❌ 不支持图片变形效果（透视 Perspective、扭曲 Warp、弧形 Arc、波浪等）
- ❌ 不支持 9-slicing（Sprite 切片）自动配置
- ❌ 不支持图片动画或序列帧
- ❌ 不支持非纯色图层的智能采样（`.tint` 仅采样中心点，不分析色彩分布或主色调）
- ❌ 不支持 Photoshop 的高级图层效果（Color Overlay、Pattern Overlay、Gradient Overlay 等）
- ❌ 不实现自定义组件支持（项目 2，不在本 FRD 范围）
- ❌ 不实现资源复用（项目 6 的 ref/refp 功能，不在本 FRD 范围）
- ❌ 不实现 bitmap 字体支持（项目 7，不在本 FRD 范围）
- ❌ 不支持 UI Toolkit（仅 uGUI + Image 组件）
- ❌ 不提供运行时动态修改效果的 API（仅编辑器工具）
- ❌ 不提供对半导出后的合并工具（对半导出后，两个 Image 组件独立存在，不可逆）

## 验收条件

### Phase 1：Node.js 解析器扩展

**标签识别：**
- [ ] 正确识别 `.hsplit` 标签，JSON 中 `splitMode = "horizontal"`
- [ ] 正确识别 `.vsplit` 标签，JSON 中 `splitMode = "vertical"`
- [ ] 正确识别 `.tint` 标签，JSON 中 `tint.enabled = true`

**图片对半导出：**
- [ ] `.hsplit` 图层导出**左半部分**图片（宽度 = 原图 50%，从 x=0 开始）
- [ ] `.vsplit` 图层导出**上半部分**图片（高度 = 原图 50%，从 y=0 开始）
- [ ] 对半导出的图片文件命名规则在 design 阶段确定（参考未决问题 #2），确保与完整图片区分且无重名冲突
- [ ] JSON 中 `assetPath` 指向对半导出的图片文件

**图层变换提取：**
- [ ] 正确提取图层旋转角度，写入 `imageEffects.transform.rotation`
- [ ] 正确提取图层缩放比例，写入 `imageEffects.transform.scale.x` 和 `.y`
- [ ] 正确识别镜像（scale 负值），推导 `imageEffects.transform.flip.x` 和 `.y`
- [ ] 正确提取图层透明度（0-100% → 0-1），写入 `imageEffects.opacity`

**纯色填充提取：**
- [ ] 标记 `.tint` 的图层，采样中心点像素颜色
- [ ] 颜色值转换为 RGBA（0-1 范围），写入 `imageEffects.tint.color`
- [ ] 中心点坐标计算：`(width / 2, height / 2)`

**边界情况处理：**
- [ ] 空图层标记 `.hsplit` → 跳过导出，生成警告日志
- [ ] 隐藏图层标记 `.vsplit` → 跳过导出，生成警告日志
- [ ] 空图层标记 `.tint` → 跳过导出，生成警告日志

**JSON Schema 验证：**
- [ ] `imageEffects` 字段符合 zod Schema 定义
- [ ] 包含所有必需字段：`transform`、`opacity`
- [ ] 可选字段 `splitMode`、`tint` 正确序列化

### Phase 2：Unity 生成器扩展

**对半图层处理：**
- [ ] `splitMode = "horizontal"` 的图层生成两个子 Image 组件
  - 第一个命名：`<LayerName>_Left`，位置：原图层左半区域
  - 第二个命名：`<LayerName>_Right`，位置：原图层右半区域，`localScale.x = -1`
- [ ] `splitMode = "vertical"` 的图层生成两个子 Image 组件
  - 第一个命名：`<LayerName>_Top`，位置：原图层上半区域
  - 第二个命名：`<LayerName>_Bottom`，位置：原图层下半区域，`localScale.y = -1`
- [ ] 两个 Image 组件的边界无缝对接（没有缝隙或重叠）
- [ ] 两个 Image 组件的父对象位置和大小与原图层一致

**变换效果应用：**
- [ ] 正确应用旋转到 `RectTransform.localRotation`（Quaternion.Euler(0, 0, rotation)）
- [ ] 正确应用缩放到 `RectTransform.localScale`
- [ ] 正确应用镜像（scale 负值）到 `RectTransform.localScale`
- [ ] 正确应用透明度到 `Image.color.a`

**纯色填充应用：**
- [ ] `tint.enabled = true` 时，正确设置 `Image.color` 为 JSON 中的颜色值
- [ ] `tint.enabled = false` 或未定义时，`Image.color = Color.white`（Unity 默认）

**迭代维护机制：**
- [ ] 检测 `PSD2UILock` 组件，锁定的 Image 对象跳过所有效果更新
- [ ] 生成日志：记录哪些对象被锁定、跳过的效果类型

**边界情况处理：**
- [ ] 空图层或隐藏图层被跳过后，Unity 中不生成对应 GameObject

### Phase 3：端到端测试

**视觉还原测试：**
- [ ] 使用包含所有效果的真实 PSD 测试（对半、旋转、缩放、镜像、透明度、tint）
- [ ] Unity 中的 Image 组件视觉还原度 > 90%（通过截图对比验证）
- [ ] 对半图层的两个 Image 组件无缝拼接，视觉上等同于完整图片

**效果组合测试：**
- [ ] `.hsplit.tint`：对半导出 + 纯色填充，两个 Image 都应用 tint 颜色
- [ ] `.vsplit` + 旋转 45°：对半导出旋转后的图层，Unity 中两个 Image 正确显示
- [ ] `.tint` + 50% 透明度：同时应用 tint 颜色和透明度

**性能测试：**
- [ ] 包含 10+ 对半图层的 PSD 解析时间 < 15 秒
- [ ] Unity 生成器应用效果时间 < 5 秒
- [ ] 对半导出后，资源文件大小减少约 50%

**边界情况测试：**
- [ ] 空图层标记 `.hsplit` → 解析日志包含警告，Unity 中无对应 GameObject
- [ ] 非纯色图层标记 `.tint` → 采样中心点颜色（可能颜色不符预期，但不报错）
- [ ] 对半图层未锁定，PSD 更新后重新生成 → 两个 Image 都更新
- [ ] 对半图层其中一个 Image 锁定 → 该 Image 跳过更新，另一个正常更新

## Decisions

| # | 决策点 | 选择 | 理由 | 影响范围 |
|---|--------|------|------|----------|
| 1 | 对半导出方向 | `.hsplit` 导出左半，`.vsplit` 导出上半 | 符合从左到右、从上到下的视觉习惯，美术和程序员易理解 | 解析器、生成器、文档 |
| 2 | 对称性检查 | 不检查，信任美术标记 | 像素级对比成本高，频繁误用可能性低，通过文档和示例教育美术团队 | 解析器 |
| 3 | 纯色填充采样点 | 图层中心点像素 | 比第一个像素更稳妥（边缘可能透明），假设美术保证图层单色 | 解析器 |
| 4 | 镜像实现方式 | 使用 `RectTransform.localScale` 负值 | Unity 原生支持，无需额外组件或 Shader，性能最优 | 生成器 |
| 5 | 标签语法 | `.hsplit` / `.vsplit` / `.tint`，无参数 | 简洁明确，与现有标签体系一致（点后缀），颜色从 PSD 提取（所见即所得） | 解析器、文档 |
| 6 | JSON Schema 结构 | 在 `LayerConfig` 中增加 `imageEffects` 字段 | 保持数据结构一致性，便于生成器遍历，扩展性好 | 解析器输出、生成器输入 |
| 7 | 对半导出 + 旋转 | 先旋转再对半（导出旋转后的一半） | 符合美术在 PSD 中看到的效果，Unity 中不再额外旋转对半图层 | 解析器 |
| 8 | 对半图层命名 | `<LayerName>_Left` / `_Right` / `_Top` / `_Bottom` | 明确表达图层关系，便于程序员识别和调试 | 生成器 |
| 9 | 迭代维护机制 | 复用 `PSD2UILock` 组件，锁定后跳过所有效果更新 | 与现有文字样式 FRD 保持一致，程序员无需学习新机制 | 生成器 |
| 10 | 空图层处理 | 跳过导出，生成警告日志 | 避免生成空资源文件，日志便于程序员调试美术错误 | 解析器、生成器 |

## 未决问题

| # | 问题 | 建议解决阶段 |
|---|------|-------------|
| 1 | ag-psd 库是否支持图层变换参数（rotation、scale）的提取？如果不支持，是否需要解析原始 PSD 二进制数据 | research - 需要验证 ag-psd API 覆盖度 |
| 2 | 对半导出后的图片文件命名规则：是否需要包含原图层 ID 以避免重名冲突？例如 `<LayerName>_<LayerID>_half.png` | design - 确定文件命名规范 |
| 3 | 对半图层的两个 Image 组件边界对接：是否需要微调位置以避免浮点误差导致的缝隙？ | design - 需要测试 Unity 中的像素对齐行为 |
| 4 | 纯色填充的混合模式：Unity Image.color 使用 Multiply 还是 Additive？默认是 Multiply（Tint），是否足够？ | explore - 测试不同混合模式的视觉效果 |
| 5 | 对半导出是否需要支持自定义比例（如三七分、四六分）？当前只支持 50% 对半 | design - 评估需求频率和实现复杂度 |

## 约束与假设

**技术约束：**
- Node.js 版本 ≥ 18.x（解析器运行环境）
- Unity 版本 ≥ 2021.3 LTS
- 项目使用 uGUI + Image 组件（非 UI Toolkit）
- 依赖 ag-psd 库版本 ≥ 14.x（支持图层变换和像素数据访问）

**开发约束：**
- 解析器代码在 `PSDExporterProject/src/parser/` 和 `src/recognizer/` 下扩展
- Unity 插件代码在 `UnityProject/Assets/Change/Editor/PSD2UI/` 下扩展（路径待 research 确认）
- 需要更新现有的 JSON Schema 定义（`src/generator/json-schema.ts`）

**规范约束：**
- 美术提供的 PSD 必须先通过 PSD2UIForm 脚本优化导出
- 对半导出的图层必须是对称的（由美术保证，工具不检查）
- 纯色填充的图层必须是单色填充（由美术保证，工具仅采样中心点）
- 推荐美术遵循效果标签使用规范（在文档中明确哪些场景适合使用对半导出）

**假设：**
- 美术团队理解对称图片的概念，不会对非对称图片使用 `.hsplit` / `.vsplit`
- 美术团队理解纯色填充的含义，不会对渐变或多色图层使用 `.tint`
- 程序员理解 `PSD2UILock` 机制的使用场景（手动调整需要保留时）
- 美术更新 PSD 时，不会大幅修改图层的命名和层级结构（否则增量更新可能失效）
- 对半导出主要用于大尺寸对称背景图、装饰元素等静态图片，不适用于需要独立控制两半的交互元素
- Unity 项目的 Sprite Import Settings 使用默认设置（无特殊压缩或 Mipmap 配置）

## 技术选型摘要

**解析器扩展（Node.js）：**
- ag-psd - 图层变换参数提取、像素数据访问
- sharp 或 canvas - 图层裁剪和导出（对半切分）
- zod - JSON Schema 扩展和验证

**生成器扩展（Unity C#）：**
- Unity Editor API - GameObject 和 RectTransform 创建、参数设置
- UnityEngine.UI.Image - 组件属性设置（color、sprite）
- Newtonsoft.Json 或 System.Text.Json - JSON 解析

**测试工具：**
- Jest - 解析器单元测试
- Unity Test Framework - 生成器单元测试和集成测试
- 手动截图对比 - 视觉还原度验证

## 输出物规范

### JSON Schema 扩展示例

```json
{
  "layers": [
    {
      "id": "layer_010",
      "name": "img_background.hsplit",
      "type": "image",
      "bounds": { "x": 0, "y": 0, "width": 2048, "height": 1024 },
      "visible": true,
      "opacity": 1.0,
      "assetPath": "assets/img_background_half.png",
      "imageEffects": {
        "splitMode": "horizontal",
        "transform": {
          "rotation": 0,
          "scale": { "x": 1.0, "y": 1.0 },
          "flip": { "x": false, "y": false }
        },
        "opacity": 1.0
      }
    },
    {
      "id": "layer_011",
      "name": "img_icon.tint",
      "type": "image",
      "bounds": { "x": 100, "y": 100, "width": 128, "height": 128 },
      "visible": true,
      "opacity": 0.8,
      "assetPath": "assets/img_icon.png",
      "imageEffects": {
        "splitMode": null,
        "transform": {
          "rotation": 45,
          "scale": { "x": 1.5, "y": 1.5 },
          "flip": { "x": false, "y": false }
        },
        "tint": {
          "enabled": true,
          "color": { "r": 1.0, "g": 0.0, "b": 0.0, "a": 1.0 }
        },
        "opacity": 0.8
      }
    }
  ]
}
```

### Unity 生成器伪代码示例

```csharp
// ImageEffectsApplier.cs (伪代码)
public void ApplyImageEffects(GameObject layerObject, LayerConfig layer)
{
    var effects = layer.imageEffects;
    
    // 对半图层处理
    if (effects.splitMode == "horizontal")
    {
        CreateSplitImages(layerObject, layer, isHorizontal: true);
        return; // 对半图层不应用其他效果到父对象
    }
    else if (effects.splitMode == "vertical")
    {
        CreateSplitImages(layerObject, layer, isHorizontal: false);
        return;
    }
    
    // 普通图层：应用变换效果
    var rectTransform = layerObject.GetComponent<RectTransform>();
    rectTransform.localRotation = Quaternion.Euler(0, 0, effects.transform.rotation);
    rectTransform.localScale = new Vector3(
        effects.transform.scale.x,
        effects.transform.scale.y,
        1f
    );
    
    // 应用颜色效果
    var image = layerObject.GetComponent<Image>();
    Color color = Color.white;
    
    if (effects.tint?.enabled == true)
    {
        color = new Color(
            effects.tint.color.r,
            effects.tint.color.g,
            effects.tint.color.b,
            effects.opacity
        );
    }
    else
    {
        color.a = effects.opacity;
    }
    
    image.color = color;
}

private void CreateSplitImages(GameObject parent, LayerConfig layer, bool isHorizontal)
{
    var sprite = LoadSprite(layer.assetPath); // 加载对半导出的图片
    var bounds = layer.bounds;
    
    // 第一个 Image（左半或上半）
    var firstHalf = new GameObject(isHorizontal ? $"{layer.name}_Left" : $"{layer.name}_Top");
    firstHalf.transform.SetParent(parent.transform, false);
    var firstImage = firstHalf.AddComponent<Image>();
    firstImage.sprite = sprite;
    var firstRT = firstHalf.GetComponent<RectTransform>();
    firstRT.anchorMin = new Vector2(0, isHorizontal ? 0 : 0.5f);
    firstRT.anchorMax = new Vector2(isHorizontal ? 0.5f : 1, 1);
    firstRT.offsetMin = Vector2.zero;
    firstRT.offsetMax = Vector2.zero;
    
    // 第二个 Image（右半或下半，镜像）
    var secondHalf = new GameObject(isHorizontal ? $"{layer.name}_Right" : $"{layer.name}_Bottom");
    secondHalf.transform.SetParent(parent.transform, false);
    var secondImage = secondHalf.AddComponent<Image>();
    secondImage.sprite = sprite;
    var secondRT = secondHalf.GetComponent<RectTransform>();
    secondRT.anchorMin = new Vector2(isHorizontal ? 0.5f : 0, 0);
    secondRT.anchorMax = new Vector2(1, isHorizontal ? 1 : 0.5f);
    secondRT.offsetMin = Vector2.zero;
    secondRT.offsetMax = Vector2.zero;
    secondRT.localScale = isHorizontal 
        ? new Vector3(-1, 1, 1)  // X 轴镜像
        : new Vector3(1, -1, 1); // Y 轴镜像
    
    // 应用其他效果（tint、opacity）到两个 Image
    ApplyColorEffects(firstImage, layer.imageEffects);
    ApplyColorEffects(secondImage, layer.imageEffects);
}
```

详细实现在 design 和 plan 阶段完善。
