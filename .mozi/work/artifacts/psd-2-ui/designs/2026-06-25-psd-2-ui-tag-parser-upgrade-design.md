# PSD 标签解析器升级 架构设计

> 日期: 2026-06-25 | 状态: 草稿
> FRD: [PSD 标签解析器升级功能需求文档](../discover/2026-06-25-psd-2-ui-tag-parser-upgrade-frd.md)
> 上游: FRD

## 上游产出引用

### 来自 FRD

| 引用内容 | 如何使用 |
|---------|----------|
| 决策 #2: 点号后缀式标签格式 (`name.bt.tmp`) | 切片 B 实现右→左解析算法 |
| 决策 #3: 右→左解析优先级 | 切片 B 实现同 family 后面覆盖前面的逻辑 |
| 决策 #6: ref/refp 处理策略 | 切片 B 识别前缀并保留到 TagParseResult，不污染 ComponentInfo |
| 决策 #9: ComponentInfo 扩展字段 | 切片 C 新增 textBackend / imageType / role 可选字段 |
| 决策 #10: TagParseResult 独立类型 | 切片 A 定义独立的解析结果类型，与 ComponentInfo 解耦 |
| 验收条件: 39 个标签定义 | 切片 A 创建 tag-config.json 包含完整标签定义 |
| 验收条件: 多标签叠加、优先级、前缀、边界情况 | 切片 B 单元测试覆盖所有场景 |
| 技术选型: zod 校验配置文件 | 切片 A 使用 zod schema 校验 tag-config.json |

## 架构决策

| 决策 | 选择 | 理由 |
|------|------|------|
| 架构模式 | 增量重构 + 类型分离 (Approach 1) | 保持 TagParser 类结构，引入 TagParseResult 中间类型实现解耦；职责清晰：TagParser 负责解析，ComponentRecognizer 负责类型组装；易于测试和扩展 |
| TagParseResult 独立性 | 独立类型，不继承 ComponentInfo | 解析结果与组件信息语义不同，解耦后 Unity 生成器可直接消费 TagParseResult 的 ref/refp 前缀信息 |
| 配置加载时机 | TagParser 构造时一次性加载 | 平衡性能和灵活性：启动时加载避免重复 I/O，失败时快速暴露配置错误 |
| 标签映射数据结构 | 内存 Map（family → Set<tagId>） | 查找复杂度 O(1)，解析性能优于数组遍历 |
| 未识别标签策略 | 跳过并继续解析（宽松模式） | 与 FRD 决策 #7 对齐，部分标签错误不影响整体可用性 |
| main family → ComponentType 映射 | 硬编码映射表 + ComponentType 枚举扩展 | 在 component-types.ts 中扩展 ComponentType 枚举，新增 Dropdown / Toggle / Slider / Mask / FillColor 类型，使 11 个 main family 与 ComponentType 一对一对应 |

## 文件地图

| 文件 | 所属切片 | 职责 |
|------|----------|------|
| `config/tag-config.json` | 切片 A | 标签定义配置文件（39 个规范标签） |
| `src/recognizer/tag-parse-result.ts` | 切片 A | TagParseResult 类型定义 |
| `src/recognizer/tag-config-loader.ts` | 切片 A | 配置加载器，包含 zod schema 校验 |
| `src/recognizer/tag-parser.ts` | 切片 B | 重构后的标签解析器（右→左解析算法） |
| `tests/recognizer/tag-parser.test.ts` | 切片 B | TagParser 单元测试（15-20 用例） |
| `src/recognizer/component-types.ts` | 切片 C | 扩展 ComponentInfo 接口 |
| `src/recognizer/component-recognizer.ts` | 切片 C | 适配 TagParseResult → ComponentInfo |
| `src/generator/json-schema.ts` | 切片 C | 扩展 ComponentInfoSchema，新增 textBackend/imageType/role 字段校验 |
| `tests/integration/tag-parser-integration.test.ts` | 切片 D | 端到端集成测试 |
| `UnityProject/Assets/Change/Editor/PSD2UI/ComponentInfo.cs` | 切片 E | 新增 C# ComponentInfo 数据类（对应 JSON ComponentInfo 结构） |
| `UnityProject/Assets/Change/Editor/PSD2UI/ComponentFactory.cs` | 切片 E | 扩展 ComponentFactory，支持新组件类型和扩展属性 |
| `UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/ComponentFactoryTests.cs` | 切片 E | ComponentFactory 单元测试 |

## 切片分解

### 切片 A: 类型定义与配置基础设施（TypeScript 端）

**依赖：** 无  
**风险等级：** 低  
**涉及文件：** `tag-parse-result.ts`, `tag-config.json`, `tag-config-loader.ts`

**内容：** 建立数据契约和配置加载机制。定义 TagParseResult 接口（prefix + baseName + families），创建包含 39 个标签定义的 JSON 配置文件，实现配置加载器并使用 zod 进行 schema 校验。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `TagParseResult` | `interface TagParseResult { prefix?: 'ref' \| 'refp'; baseName: string; families: {...} }` | 数据容器，存储解析后的原始结果 |
| `TagConfig` | `interface TagConfig { version: string; families: {...} }` | 配置文件结构定义 |
| `TagConfigLoader.load()` | `static load(path: string): TagConfig` | 同步加载并校验配置文件，失败抛出异常 |
| `TagConfigLoader.validate()` | `static validate(data: unknown): TagConfig` | 使用 zod schema 校验配置数据 |

**数据契约：**

```typescript
// tag-parse-result.ts
export interface TagParseResult {
  prefix?: 'ref' | 'refp';
  baseName: string;
  families: {
    main?: string;
    textBackend?: string;
    imageType?: string;
    role?: string;
  };
}

// tag-config-loader.ts 内部类型
interface TagConfig {
  version: string;
  canonicalOrder: string[];
  families: {
    main: Array<{ id: string; label: string }>;
    textBackend: Array<{ id: string; label: string }>;
    imageType: Array<{ id: string; label: string }>;
    role: Array<{ id: string; label: string }>;
  };
}
```

**验收标准：**
- [ ] TagParseResult 类型定义完整，包含 prefix/baseName/families 字段
- [ ] tag-config.json 包含 39 个标签定义（11 main + 2 textBackend + 4 imageType + 22 role）
- [ ] TagConfigLoader.load() 成功加载配置文件
- [ ] 配置文件缺失时抛出明确错误信息：`Error: Config file not found: <path>`
- [ ] 配置文件格式错误时 zod 校验失败并抛出详细错误（包含具体字段路径）
- [ ] TypeScript 类型编译通过

**回归风险评估：**
- **影响范围：** 无，纯新增文件
- **缓解措施：** 无需缓解

---

### 切片 B: 核心解析引擎（TypeScript 端 - 高风险，优先实施）

**依赖：** 切片 A  
**风险等级：** 高（复杂算法、多边界情况）  
**涉及文件：** `tag-parser.ts`, `tag-parser.test.ts`

**内容：** 重构 TagParser 类，实现点号后缀式多标签解析。核心算法：检测 ref/refp 前缀（空格分隔），按 `.` 分割标签，右→左遍历分类到 4 个 family，同 family 后面的覆盖前面的，未识别标签跳过，无标签返回 null。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `TagParser.constructor()` | `constructor(config: TagConfig)` | 初始化解析器，将配置加载到内存 Map（family → Set<tagId>） |
| `TagParser.parse()` | `parse(layerName: string): TagParseResult \| null` | 解析图层名，返回结构化结果或 null（无标签时） |
| `TagParser.hasTag()` | `hasTag(layerName: string): boolean` | 检查图层名是否包含至少一个有效标签 |

**行为契约：**

**解析算法流程：**
1. 检测 ref/refp 前缀（用空格与后续内容分隔）→ 提取 prefix 和剩余部分
2. 按 `.` 分割剩余部分 → 得到 token 数组
3. 从右往左遍历 tokens，查找每个 token 属于哪个 family
4. 同 family 的标签，后面的（右侧）覆盖前面的（左侧）
5. 未识别的标签跳过，不影响其他标签解析
6. 如果没有任何有效标签，返回 null
7. 否则返回 TagParseResult（包含 baseName 和 families）

**输入输出示例：**
- 输入: `"close.bt.tmp.bg"` → 输出: `{baseName: "close", families: {main: "bt", textBackend: "tmp", role: "bg"}}`
- 输入: `"panel.bt.dpd"` → 输出: `{baseName: "panel", families: {main: "dpd"}}` (右侧 dpd 覆盖左侧 bt)
- 输入: `"ref icon.img"` → 输出: `{prefix: "ref", baseName: "icon", families: {main: "img"}}`
- 输入: `"name.unknowntag.bt"` → 输出: `{baseName: "name", families: {main: "bt"}}` (跳过 unknowntag)
- 输入: `"background"` → 输出: `null` (无标签)
- 输入: `"name.xyz.abc"` → 输出: `null` (全是未识别标签)

**验收标准：**
- [ ] 解析 `close.bt.tmp.bg` 返回正确的 baseName 和 3 个 family
- [ ] 解析 `panel.bt.dpd` 返回 `{main: "dpd"}`（验证右→左优先级）
- [ ] 解析 `ref icon.img` 正确提取 prefix 和保留 baseName
- [ ] 解析 `refp panel.bt` 正确处理 refp 前缀
- [ ] 解析 `ref close button.bt.tmp` 正确处理 baseName 包含空格的情况
- [ ] 解析 `name.unknowntag.bt` 跳过未识别标签，返回 `{main: "bt"}`
- [ ] 解析 `background`（无标签）返回 null
- [ ] 解析 `name.xyz.abc`（全是未识别标签）返回 null
- [ ] 单元测试覆盖率 > 80%
- [ ] 所有 FRD 中"核心解析功能"和"前缀处理"验收条件通过

**回归风险评估：**
- **影响范围：** TagParser.parse() 返回类型从 `ComponentType | null` 改为 `TagParseResult | null`，破坏 ComponentRecognizer 现有调用
- **缓解措施：** 切片 C 紧随其后适配 ComponentRecognizer，确保接口兼容性恢复

---

### 切片 C: ComponentInfo 扩展与集成（TypeScript 端）

**依赖：** 切片 B  
**风险等级：** 中（类型系统变更影响下游）  
**涉及文件：** `component-types.ts`, `component-recognizer.ts`, `json-schema.ts`

**内容：** 扩展 ComponentInfo 接口，新增 textBackend / imageType / role 三个可选字段。更新 ComponentRecognizer.recognize() 方法，适配新的 TagParser 接口，将 TagParseResult.families 映射到 ComponentInfo 扩展字段，将 main family 映射到 ComponentType。同步更新 JSON Schema (ComponentInfoSchema)，确保 JSON 导出包含新字段并通过 zod 校验。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `ComponentInfo` (扩展) | `interface ComponentInfo { type: ComponentType; textBackend?: 'tmp' \| 'ugui'; imageType?: 'simple' \| 'sliced' \| 'tiled' \| 'filled'; role?: string; ... }` | 新增三个可选字段表达细分语义 |
| `ComponentRecognizer.recognize()` | `async recognize(layer: Layer, imagePath?: string): Promise<ComponentInfo>` | 适配新 TagParser，从 TagParseResult 填充 ComponentInfo |
| `mainFamilyToComponentType()` | `(mainFamily: string): ComponentType` | 将 main family 标签 ID 映射到 ComponentType 枚举 |

**数据契约：**

```typescript
// component-types.ts 扩展
export type ComponentType =
  | 'Button'
  | 'Image'
  | 'RawImage'      // 新增
  | 'Text'
  | 'ScrollView'
  | 'InputField'
  | 'Dropdown'      // 新增
  | 'Toggle'        // 新增
  | 'Slider'        // 新增
  | 'Mask'          // 新增
  | 'FillColor'     // 新增
  | 'VerticalLayoutGroup'
  | 'HorizontalLayoutGroup'
  | 'GridLayoutGroup'
  | 'Unknown';

export interface ComponentInfo {
  type: ComponentType;
  textBackend?: 'tmp' | 'ugui';      // 新增：文本后端
  imageType?: 'simple' | 'sliced' | 'tiled' | 'filled';  // 新增：图片类型
  role?: string;                      // 新增：角色标签（bg, press, placeholder 等）
  confidence: number;
  source: 'tag' | 'cv' | 'ai';
  needsReview: boolean;
}

// component-recognizer.ts 内部映射表
const MAIN_FAMILY_MAP: Record<string, ComponentType> = {
  'bt': 'Button',
  'img': 'Image',
  'rimg': 'RawImage',
  'txt': 'Text',
  'ipt': 'InputField',
  'dpd': 'Dropdown',
  'tg': 'Toggle',
  'sld': 'Slider',
  'sv': 'ScrollView',
  'msk': 'Mask',
  'col': 'FillColor',
};

// json-schema.ts 扩展
export const ComponentInfoSchema = z.object({
  type: z.enum([
    'Button', 'Image', 'RawImage', 'Text', 'ScrollView', 'InputField',
    'Dropdown', 'Toggle', 'Slider', 'Mask', 'FillColor',
    'VerticalLayoutGroup', 'HorizontalLayoutGroup', 'GridLayoutGroup', 'Unknown',
  ]),
  textBackend: z.enum(['tmp', 'ugui']).optional(),      // 新增
  imageType: z.enum(['simple', 'sliced', 'tiled', 'filled']).optional(),  // 新增
  role: z.string().optional(),                          // 新增
  confidence: z.number().min(0).max(1),
  source: z.enum(['tag', 'cv', 'ai']),
  needsReview: z.boolean(),
});
```

**行为契约：**

**ComponentRecognizer.recognize() 更新逻辑：**
1. 调用 `tagParser.parse(layer.name)` → 得到 `TagParseResult | null`
2. 如果结果为 null → 走现有 AI 识别流程（保持不变）
3. 如果结果非 null：
   - 从 `result.families.main` 查 MAIN_FAMILY_MAP 获取 ComponentType
   - 填充 `textBackend` / `imageType` / `role` 字段（从 families 中提取）
   - 返回 `{ type, textBackend, imageType, role, confidence: 1.0, source: 'tag', needsReview: false }`

**验收标准：**
- [ ] ComponentInfo 接口包含 textBackend / imageType / role 三个可选字段
- [ ] 图层名 `close.bt.tmp.bg` → ComponentInfo 为 `{type: 'Button', textBackend: 'tmp', role: 'bg', confidence: 1.0, source: 'tag'}`
- [ ] 图层名 `icon.img.sliced` → ComponentInfo 为 `{type: 'Image', imageType: 'sliced', confidence: 1.0, source: 'tag'}`
- [ ] 图层名 `input.ipt.tmp.placeholder` → ComponentInfo 为 `{type: 'InputField', textBackend: 'tmp', role: 'placeholder'}`
- [ ] ComponentInfoSchema 扩展三个可选字段：`textBackend`, `imageType`, `role`
- [ ] ComponentInfoSchema 的 type 枚举扩展为 15 个值（新增 Dropdown/Toggle/Slider/RawImage/Mask/FillColor）
- [ ] 导出的 JSON 文件通过 zod schema 校验
- [ ] 无标签图层（如 `background`）仍然走 AI 识别流程，行为不变
- [ ] TypeScript 编译通过，无类型错误
- [ ] 现有 ComponentRecognizer 测试通过或按需更新

**回归风险评估：**
- **影响范围：** 
  - ComponentInfo 接口扩展，JSON Schema 同步更新
  - 导出的 JSON 文件格式变化（新增可选字段）
  - Unity C# 端需要读取新字段（超出当前切片范围，需后续跟进）
- **缓解措施：** 
  - 新字段都是可选的，旧 JSON 文件仍可通过 schema 校验
  - 在 implement 阶段验证新旧 JSON 格式兼容性
  - Unity 端可以先忽略新字段，功能不受影响（向后兼容）

---

### 切片 D: 集成测试与边界验证（TypeScript 端）

**依赖：** 切片 C  
**风险等级：** 低  
**涉及文件：** `tag-parser-integration.test.ts`

**内容：** 编写端到端集成测试，验证完整流程：图层名 → TagParser → ComponentRecognizer → ComponentInfo。覆盖 FRD 所有验收条件，包括多标签叠加、优先级覆盖、ref 前缀、未识别标签跳过、无标签 AI 兜底。确保现有测试套件通过。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| 集成测试套件 | `describe('TagParser Integration', ...)` | 测试完整流程的各种场景 |

**行为契约：**

**测试场景覆盖：**
1. **多标签叠加** — `close.bt.tmp.bg` → Button + TMP + bg 角色
2. **优先级覆盖** — `panel.bt.dpd` → Dropdown（不是 Button）
3. **ref 前缀保留** — `ref icon.img` → Image（prefix 保留在 TagParseResult 但不影响 ComponentInfo.type）
4. **未识别标签跳过** — `name.unknowntag.bt` → Button
5. **无标签 AI 兜底** — `background` → 走 AI 识别流程
6. **现有 AI 流程不变** — 确保无标签图层的 AI 识别仍正常工作
7. **边界情况** — 全是未识别标签、空字符串、特殊字符等

**验收标准：**
- [ ] 所有 FRD"核心解析功能"验收条件的集成测试通过
- [ ] 所有 FRD"前缀处理"验收条件的集成测试通过
- [ ] 所有 FRD"边界情况"验收条件的集成测试通过
- [ ] 所有 FRD"类型系统集成"验收条件的集成测试通过
- [ ] 手动测试：使用包含新标签格式的示例 PSD 文件，验证 JSON 输出正确
- [ ] 回归测试：现有所有测试用例通过（或识别并更新受影响的测试）
- [ ] ESLint + Prettier 代码检查通过
- [ ] TypeScript 编译无错误，无 any 类型

**回归风险评估：**
- **影响范围：** 整体功能集成，确认无遗漏场景
- **缓解措施：** 覆盖 FRD 所有验收条件；特别关注 AI 兜底逻辑是否仍然正常工作；检查现有测试中依赖旧标签格式的用例

---

### 切片 E: Unity C# 端组件扩展

**依赖：** 切片 C（JSON Schema 已更新）  
**风险等级：** 中（新增 C# 数据类，扩展组件工厂）  
**涉及文件：** `ComponentInfo.cs`, `ComponentFactory.cs`, `ComponentFactoryTests.cs`

**内容：** 在 Unity 端新增 ComponentInfo.cs 数据类，映射 TypeScript 端的 ComponentInfo 结构（包含 textBackend / imageType / role 扩展字段）。扩展 ComponentFactory.CreateComponent() 方法，支持新的组件类型（Dropdown, Toggle, Slider, RawImage, Mask, FillColor），并根据扩展属性配置组件（如 Image.type 根据 imageType 设置为 Simple/Sliced/Tiled/Filled，Text 根据 textBackend 选择 TextMeshPro 或 UGUI Text）。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `ComponentInfo` (C# class) | `public class ComponentInfo { string Type; string TextBackend; string ImageType; string Role; ... }` | C# 数据类，对应 JSON 中的 ComponentInfo 结构 |
| `ComponentFactory.CreateComponent()` (扩展) | `public Component CreateComponent(string type, GameObject target, ComponentInfo info = null)` | 新增可选参数 info，根据扩展属性配置组件 |
| `ComponentFactory.ConfigureImageType()` | `private void ConfigureImageType(Image image, string imageType)` | 根据 imageType 设置 Image.type（Simple/Sliced/Tiled/Filled） |
| `ComponentFactory.CreateTextComponent()` | `private Component CreateTextComponent(string textBackend, GameObject target)` | 根据 textBackend 创建 TextMeshProUGUI 或 UGUI Text |

**数据契约：**

```csharp
// ComponentInfo.cs
namespace Change.Editor.PSD2UI
{
    public class ComponentInfo
    {
        public string Type { get; set; }              // "Button", "Image", "Dropdown", etc.
        public string TextBackend { get; set; }       // "tmp" | "ugui" (可选)
        public string ImageType { get; set; }         // "simple" | "sliced" | "tiled" | "filled" (可选)
        public string Role { get; set; }              // "bg", "press", "placeholder", etc. (可选)
        public float Confidence { get; set; }
        public string Source { get; set; }            // "tag" | "cv" | "ai"
        public bool NeedsReview { get; set; }
    }
}

// ComponentFactory.cs 扩展
public Component CreateComponent(string type, GameObject target, ComponentInfo info = null)
{
    // 支持的类型：
    // Button, Image, RawImage, Text, ScrollRect, InputField,
    // Dropdown, Toggle, Slider, Mask (新增 5 个)
    // FillColor 暂不实现（无对应 Unity 组件）
    
    switch (type)
    {
        case "Image":
            var image = target.AddComponent<Image>();
            if (info?.ImageType != null)
                ConfigureImageType(image, info.ImageType);
            return image;
            
        case "RawImage":
            return target.AddComponent<RawImage>();
            
        case "Text":
            return CreateTextComponent(info?.TextBackend ?? "tmp", target);
            
        case "Dropdown":
            return target.AddComponent<Dropdown>();
            
        case "Toggle":
            return target.AddComponent<Toggle>();
            
        case "Slider":
            return target.AddComponent<Slider>();
            
        case "Mask":
            return target.AddComponent<Mask>();
            
        // ... 其他现有类型
    }
}

private void ConfigureImageType(Image image, string imageType)
{
    switch (imageType)
    {
        case "simple":
            image.type = Image.Type.Simple;
            break;
        case "sliced":
            image.type = Image.Type.Sliced;
            break;
        case "tiled":
            image.type = Image.Type.Tiled;
            break;
        case "filled":
            image.type = Image.Type.Filled;
            break;
    }
}

private Component CreateTextComponent(string textBackend, GameObject target)
{
    if (textBackend == "ugui")
        return target.AddComponent<UnityEngine.UI.Text>();
    else
        return target.AddComponent<TextMeshProUGUI>();  // 默认 TMP
}
```

**行为契约：**

**ComponentFactory 扩展逻辑：**
1. 接受可选的 ComponentInfo 参数（包含扩展属性）
2. 根据 type 创建对应的 Unity 组件（新增支持 Dropdown/Toggle/Slider/RawImage/Mask）
3. 如果提供了 imageType，配置 Image.type 属性
4. 如果提供了 textBackend，选择创建 TextMeshProUGUI 或 UGUI Text
5. role 字段暂不消费（保留给后续子组件识别功能，如识别 Button 的 bg/press/select 子节点）

**验收标准：**
- [ ] ComponentInfo.cs 定义完整，包含 Type/TextBackend/ImageType/Role 等字段
- [ ] ComponentFactory 支持创建 Dropdown, Toggle, Slider, RawImage, Mask 组件
- [ ] Image 组件根据 imageType 正确设置 type 属性（Simple/Sliced/Tiled/Filled）
- [ ] Text 组件根据 textBackend 正确选择 TextMeshProUGUI 或 UGUI Text
- [ ] ConfigReader 可以从 JSON 反序列化 ComponentInfo（Newtonsoft.Json 兼容）
- [ ] ComponentFactory 单元测试覆盖新组件类型和扩展属性配置
- [ ] 向后兼容：不提供 ComponentInfo 参数时仍使用默认行为

**回归风险评估：**
- **影响范围：** 
  - ComponentFactory 接口变更（新增可选参数）
  - 新增 ComponentInfo.cs 文件
  - ConfigReader 反序列化逻辑（Newtonsoft.Json 自动处理可选字段）
- **缓解措施：** 
  - ComponentInfo 参数设为可选（默认 null），现有调用方不受影响
  - 单元测试覆盖新旧两种调用方式
  - 新字段都是可选的，旧 JSON 文件反序列化时字段为 null，不影响默认行为

---

## 切片依赖图

```
切片 A: 类型定义与配置基础设施（TypeScript 端 - 低风险，基础）
  ↓
切片 B: 核心解析引擎（TypeScript 端 - 高风险，优先验证）
  ↓
切片 C: ComponentInfo 扩展与集成（TypeScript 端 - 中风险，类型系统集成）
  ├─→ 切片 D: 集成测试与边界验证（TypeScript 端 - 低风险，完整用户旅程）
  └─→ 切片 E: Unity C# 端组件扩展（Unity 端 - 中风险，消费 JSON 新字段）
```

**实施顺序理由：**
1. **依赖优先** — A → B → C 线性依赖，C 完成后 D 和 E 可并行
2. **风险优先** — B 是最高风险（复杂算法），通过完整单元测试在集成前验证正确性
3. **分层验证** — D 验证 TypeScript 端完整流程，E 验证 Unity 端消费能力
4. **可独立交付** — 切片 D 完成后 TypeScript 端可独立发布；切片 E 可延后到 Unity 端有实际需求时再实施

## 关键接口

### 切片 A → 切片 B

```typescript
// tag-parse-result.ts
export interface TagParseResult {
  prefix?: 'ref' | 'refp';
  baseName: string;
  families: {
    main?: string;
    textBackend?: string;
    imageType?: string;
    role?: string;
  };
}

// tag-config-loader.ts
export class TagConfigLoader {
  static load(path: string): TagConfig;
}
```

### 切片 B → 切片 C

```typescript
// tag-parser.ts
export class TagParser {
  constructor(config: TagConfig);
  parse(layerName: string): TagParseResult | null;
  hasTag(layerName: string): boolean;
}
```

### 切片 C 对外暴露（TypeScript → JSON）

```typescript
// component-types.ts
export interface ComponentInfo {
  type: ComponentType;
  textBackend?: 'tmp' | 'ugui';
  imageType?: 'simple' | 'sliced' | 'tiled' | 'filled';
  role?: string;
  confidence: number;
  source: 'tag' | 'cv' | 'ai';
  needsReview: boolean;
}

// component-recognizer.ts
export class ComponentRecognizer {
  async recognize(layer: Layer, imagePath?: string): Promise<ComponentInfo>;
}
```

### 切片 E 对外暴露（C# 消费 JSON）

```csharp
// ComponentInfo.cs
public class ComponentInfo
{
    public string Type { get; set; }
    public string TextBackend { get; set; }
    public string ImageType { get; set; }
    public string Role { get; set; }
    public float Confidence { get; set; }
    public string Source { get; set; }
    public bool NeedsReview { get; set; }
}

// ComponentFactory.cs
public class ComponentFactory
{
    public Component CreateComponent(string type, GameObject target, ComponentInfo info = null);
}
```

```typescript
// TypeScript: json-schema.ts 导出的 JSON 结构
{
  "type": "Button",
  "textBackend": "tmp",
  "imageType": "sliced",
  "role": "bg",
  "confidence": 1.0,
  "source": "tag",
  "needsReview": false
}
```

```csharp
// C#: ComponentInfo.cs 反序列化后的数据结构
public class ComponentInfo
{
    public string Type { get; set; }         // "Button"
    public string TextBackend { get; set; }  // "tmp"
    public string ImageType { get; set; }    // "sliced"
    public string Role { get; set; }         // "bg"
    public float Confidence { get; set; }    // 1.0
    public string Source { get; set; }       // "tag"
    public bool NeedsReview { get; set; }    // false
}
```

```typescript
// component-types.ts
export interface ComponentInfo {
  type: ComponentType;
  textBackend?: 'tmp' | 'ugui';
  imageType?: 'simple' | 'sliced' | 'tiled' | 'filled';
  role?: string;
  confidence: number;
  source: 'tag' | 'cv' | 'ai';
  needsReview: boolean;
}

// component-recognizer.ts
export class ComponentRecognizer {
  async recognize(layer: Layer, imagePath?: string): Promise<ComponentInfo>;
}
```

## 回归风险评估

### 整体影响范围

| 影响范围 | 风险等级 | 缓解措施 |
|---------|---------|----------|
| TagParser 接口变更 | 高 | 切片 C 紧随切片 B 实施，快速恢复接口兼容性 |
| ComponentInfo 类型扩展 | 中 | 新字段可选，现有代码向后兼容；同步更新 JSON Schema |
| JSON 导出格式变化 | 低 | 新字段可选，旧 JSON 仍可校验通过 |
| Unity C# 端数据结构 | 中 | 新增 ComponentInfo.cs，ComponentFactory 新增可选参数，向后兼容 |
| Unity C# 端组件创建 | 低 | 切片 E 单元测试覆盖新旧调用方式 |
| 现有测试用例 | 中 | 切片 D 识别并更新依赖旧标签格式的测试 |
| 旧 PSD 文件 | 低 | FRD 明确范围外，旧格式走 AI 识别兜底 |
| AI 识别流程 | 低 | 切片 C 保持无标签图层走 AI 路径，切片 D 验证 |

### 设计决策记录

| # | 问题 | 决策 | 理由 | 影响切片 |
|---|------|------|------|---------|
| 1 | ComponentType 枚举缺少新组件类型 | 扩展 ComponentType 枚举，新增 Dropdown / Toggle / Slider / RawImage / Mask / FillColor | FRD 定义了 11 个 main family，应在类型系统中完整表达，避免语义丢失 | 切片 C |
| 2 | Unity 生成器是否需要 ref/refp 前缀信息 | 保留在 TagParseResult 中，不传递到 ComponentInfo | FRD 决策 #6 明确要求保留前缀，Unity 生成器可直接消费 TagParseResult 或从原始图层名重新解析 | 切片 A/B |
| 3 | 配置文件热重载需求 | 不实现，构造时一次性加载 | FRD 未提及热重载需求；配置变更频率低；一次性加载简化设计 | 切片 A |
| 4 | Unity C# 端实现时机 | 同步设计和实现（切片 E） | JSON 新字段已定义，Unity 端同步实现可立即消费；避免后续二次对接；提供完整的端到端能力 | 切片 E |
| 5 | role 字段的 Unity 端消费方式 | 切片 E 暂不消费，保留给后续子组件识别功能 | role 字段（bg/press/select 等）用于识别 Button 的子节点角色，当前 Unity 端缺少子节点识别逻辑；切片 E 只实现基础组件创建，role 消费逻辑留给后续迭代 | 切片 E |

## 实施检查清单

**切片 A:**
- [ ] 创建 TagParseResult 接口定义
- [ ] 创建 tag-config.json（39 个标签）
- [ ] 实现 TagConfigLoader（zod 校验）
- [ ] 单元测试：配置加载成功/失败场景

**切片 B:**
- [ ] 重构 TagParser.parse() 实现右→左解析
- [ ] 实现 ref/refp 前缀识别
- [ ] 实现未识别标签跳过逻辑
- [ ] 编写 15-20 个单元测试覆盖所有场景

**切片 C:**
- [ ] 扩展 ComponentInfo 接口
- [ ] 实现 mainFamilyToComponentType 映射
- [ ] 更新 ComponentRecognizer.recognize() 适配新接口
- [ ] 更新或修复受影响的单元测试

**切片 D:**
- [ ] 编写集成测试覆盖 FRD 所有验收条件
- [ ] 运行完整测试套件，修复失败用例
- [ ] 手动测试新标签格式 PSD
- [ ] ESLint + Prettier + TypeScript 检查

**切片 E:**
- [ ] 创建 ComponentInfo.cs 数据类
- [ ] 扩展 ComponentFactory 支持新组件类型（Dropdown/Toggle/Slider/RawImage/Mask）
- [ ] 实现 ConfigureImageType() 配置 Image.type
- [ ] 实现 CreateTextComponent() 根据 textBackend 选择文本组件
- [ ] 编写 ComponentFactory 单元测试（新旧调用方式）
- [ ] 验证 ConfigReader 可正确反序列化新 JSON 格式
