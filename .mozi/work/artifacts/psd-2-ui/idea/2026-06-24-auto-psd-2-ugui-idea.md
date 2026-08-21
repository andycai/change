# 基于启发式算法的全自动无感 UI 管线实现 psd to ugui 方案

设计和实现一套从 PSD 自动生成 Unity uGUI Prefab 的工作流，是中大型游戏团队提升 UI 制作效率、减少程序与美术沟通成本的重要技术资产。

为了彻底解放美术人员，让他们无需学习任何 Unity 规范（不打标签、不设置九宫格、不理解布局与锚点），我们可以设计并实现一套**基于「启发式算法 + 计算机视觉（CV）+ 多模态 AI」的全自动无感 UI 管线**。

这套方案已经在部分前沿游戏工作室中得到应用，其核心思想是：**脏活累活全由机器做，美术只需像往常一样在 Photoshop 里画图、切图，然后一键提交。**

---

### 一、 全自动管线架构设计图

```
 [美术生产：普通 PSD (无任何标签)]
                │
                ▼
  【1. 无损结构解析器 (Node.js：ag-psd)】 ─── 导出基础 JSON 树 + 原始图层切片
                │
                ▼
  【2. 智能九宫格分析器 (CV / OpenCV)】 ─── 自动检测图片边缘，写入 9-Slice Border 数据
                │
                ▼
  【3. 多模态 AI 布局分析器 (LLM + Vision)】 ─── 自动推理组件属性（Button, ScrollView, Text）
                │
                ▼
  【4. 布局与锚点启发式推导引擎】 ─── 自动计算自适应锚点、Layout Group
                │
                ▼
  【5. Unity 还原重建插件 (C#)】 ─── 组装 Prefab + 生成 UI 绑定代码
```

---

### 二、 核心技术模块实现方案

#### 模块 1：智能九宫格分析器 (Auto 9-Slicing) — 100% 自动化
九宫格通常由四条线（Left, Right, Top, Bottom）划分，用于保护边角，拉伸中心。
我们无需 AI，用**经典的计算机视觉 (OpenCV 或像素特征分析)** 就可以实现接近 100% 准确率的自动九宫格检测：

```
       ▲  ┌─────────┬─────────┬─────────┐
       │  │  Corner │  Edge   │  Corner │
   Top │  ├─────────┼─────────┼─────────┤
       │  │  Edge   │ Center  │  Edge   │
       ▼  │         │(Stretch)│         │
          ├─────────┼─────────┼─────────┤
          │  Corner │  Edge   │  Corner │
          └─────────┴─────────┴─────────┘
          ◄────────►           ◄────────►
             Left                Right
```

*   **检测算法原理**：
    1.  **水平扫描**：从图片的左边缘向右逐像素列扫描，计算相邻列的像素相似度。一旦进入一个“可以通过拉伸/平铺来复现”的区域（即像素完全相同，或者是完美的线性渐变、平滑纹理），记录该列的位置为 `Left` 边界。
    2.  **对称检查**：同理，从右向左扫描得到 `Right` 边界。
    3.  **垂直扫描**：从上往下、从下往上扫描，计算像素行相似度，确定 `Top` 和 `Bottom` 边界。
    4.  **例外检测**：若整张图的像素熵极高（如手绘复杂插图、无任何重复像素），则判定为非九宫格图片，不进行切片设置。
*   **技术落地**：在 Node.js 解析阶段，使用 `canvas` 或 `jimp` 库运行此像素检测，检测出的 Border 像素值直接写入图片的 `meta` 属性中，Unity 导入时利用 C# `TextureImporter` 自动应用 `spriteBorder`。

---

#### 模块 2：多模态 AI 语义分析器 (AI Semantic Classifier)
在没有任何命名标签（如 `btn_xxx`）的情况下，我们需要 AI 告诉我们哪个组是按钮，哪个组是滑动列表。

*   **数据包打包**：
    将 PSD 解析出的无标签树和全图渲染效果图发送给多模态大模型（如 GPT-5.5 或 Claude 4.8 Opus）。为了保护项目隐私或节省 API 额度，也可以使用本地部署的轻量级目标检测模型（如 YOLOv8 或 Segment Anything 2），这些模型已经针对 UI 控件完成了微调（UI Object Detection）。
*   **Prompt 交互设计示例**：
    ```text
    你是一个资深的 Unity UI 专家。
    输入：一张完整的 UI 视觉稿截图，以及一份 raw_layout.json（包含所有图层的大小、坐标、层级和文字内容）。
    任务：
    1. 识别出视觉稿中的所有“按钮（Button）”，并找到 raw_layout.json 中与之对应的一个图层或图层组。
    2. 识别出列表（List / ScrollView）、输入框（InputField）、页签（Tabs）等容器组件。
    3. 输出一份更新后的 json 树，将匹配到的节点 "type" 修改为对应的 UI 组件类型。
    ```
*   **效果**：原本 PSD 中随机命名的 `Group 14`，会被 AI 正确标记为 `Button`，且其子图层 `Label 1` 被标记为 `ButtonText`。

---

#### 模块 3：锚点与弹性拉伸启发式引擎 (Heuristic Anchoring)
为了实现自适应，我们不能用死板的坐标，需要根据物体的空间几何关系推导锚点。通过**启发式规则（Heuristic Rules）**，可以将这一步完全自动化。

*   **边界锚定规则（Pin to Border）**：
    计算子节点到父节点四条边的相对距离。
    ```csharp
    // 伪代码：在 Unity 导入端自动计算
    float leftRatio = child.left / parent.width;
    float rightRatio = (parent.width - child.right) / parent.width;
    float topRatio = child.top / parent.height;
    
    // 如果该组件极其靠近右上角（例如：关闭按钮）
    if (rightRatio < 0.15f && topRatio < 0.15f) {
        // 自动将锚点设为右上角
        SetAnchor(child, AnchorPresets.TopRight);
    }
    // 如果组件的宽度几乎等于父节点宽度（例如：背景板）
    else if (leftRatio < 0.05f && rightRatio < 0.05f) {
        // 自动设为水平拉伸 Stretch
        SetAnchor(child, AnchorPresets.HorizontalStretch);
    }
    ```
*   **自适应边距（Margins）保留**：对于拉伸的组件，在 Unity 中设置 `offsetMin` 和 `offsetMax` 维持它在 PSD 中表现出的边距，以确保在宽屏手机上完美自适应。

---

#### 模块 4：自动布局（Auto Layout）的自动折叠
美术在画滚动列表或网格背包时，只是在 PSD 里手动复制排列了 10 个相同的框。

*   **几何等距检测算法**：
    1.  遍历一个图层组内的所有直接子节点。
    2.  计算相邻节点的 X 和 Y 坐标差。
    3.  若发现子节点的 Y 轴间距完全相等（如 3 个卡片垂直排列，两两间距均为 15px），则将该父图层判定为 `VerticalLayoutGroup` 候选。
    4.  **折叠去重**：因为这 10 个卡片在游戏运行时通常是动态生成的，Unity C# 还原脚本会自动**仅保留第一个子节点作为 Prefab 模板（Template）**，将其余的克隆体删除，并自动挂载 `Vertical Layout Group`，间距设为自动计算出的 15px。

---

### 三、 落地工具链架构 (Toolchain Implementation)

为了让这套方案落地，您可以构建如下结构的基础设施：

```
┌────────────────────────────────────────────────────────┐
│                      【 生产机 】                      │
│                                                        │
│ 美术在 PS 中通过无头 Node.js 工具（无需打开 PS 界面）     │
│ 运行以下命令：                                           │
│ > ui-compiler export my_panel.psd                      │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼ 【本地/构建服务器自动化处理】
┌────────────────────────────────────────────────────────┐
│                   【 Python AI 服务 】                  │
│                                                        │
│ 1. 像素级 Auto 9-Slice 分析（生成 .border 文件）         │
│ 2. 调用轻量级 UI 检测模型进行语义和对齐分析              │
│ 3. 产出最终高度富集（Enriched）的 ui_config.json        │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼ 【Unity 编辑器自动监听】
┌────────────────────────────────────────────────────────┐
│                  【 Unity Editor 插件 】                │
│                                                        │
│ 1. 自动触发 AssetPostprocessor 导入 PNG 散图             │
│ 2. 读入 .border 文件，通过 TextureImporter 写入九宫格边界 │
│ 3. C# 脚本解析 ui_config.json 并生成 uGUI Prefab         │
│ 4. 自动生成对应的 C# View 逻辑绑定代码                   │
└────────────────────────────────────────────────────────┘
```

---

### 四、 智能化进阶：一键生成 C# UI 代码（UI Binder）

在完全自动构建出 Prefab 之后，AI 还可以帮助我们将**前端界面与程序逻辑彻底桥接**。

1.  **UI 节点导出**：Unity 还原插件生成 Prefab 时，对判定为交互组件（Button, ScrollRect, InputField, Slider）的节点，自动挂载一个轻量级的标记组件（如 `UIElementRef`），并记录其唯一 ID。
2.  **AI 代码自动生成（MVVM/MVC 绑定）**：
    AI 结合 Prefab 的结构 JSON，自动为程序产出绑定代码：
    ```csharp
    // 此代码由 AI 100% 自动生成，程序无需手动连线拖拽
    public partial class ShopWindowView : MonoBehaviour 
    {
        [SerializeField] private Button btnClose;
        [SerializeField] private ScrollRect scrollGoodsList;
        [SerializeField] private TextMeshProUGUI txtTitle;

        public void BindEvents(ShopWindowViewModel viewModel)
        {
            btnClose.onClick.AddListener(viewModel.OnCloseClicked); 
            // 自动侦测到 scrollGoodsList 对应的子模板，生成数据驱动列表逻辑
            viewModel.GoodsData.OnListChanged += UpdateGoodsList;
        }
    }
    ```

### 总结

通过这套**启发式规则 + CV 降维打击 + 局部 AI 赋能**的管线，美术人员的负担降到了**绝对的零**。他们不需要知道什么是 Anchors，不需要知道什么是九宫格，甚至不需要把图层重命名。所有的技术细节都在中转环节被工具链和 AI 推导并解决，这不仅保障了美术的纯粹创作空间，也为团队沉淀了一套高复用性、工业级的自动化工作流。
