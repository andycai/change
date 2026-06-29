import * as path from 'path';
import { TagConfigLoader } from '../../src/recognizer/tag-config-loader';
import { TagParser } from '../../src/recognizer/tag-parser';
import { ComponentRecognizer } from '../../src/recognizer/component-recognizer';
import { Layer } from '../../src/parser/layer-tree';

const configPath = path.resolve(__dirname, '../../config/tag-config.json');

function makeLayer(name: string): Layer {
  return {
    id: `layer-${name}`,
    name,
    type: 'image',
    bounds: { x: 0, y: 0, width: 100, height: 100 },
    visible: true,
    opacity: 1,
    children: [],
  };
}

function makeRecognizer(): ComponentRecognizer {
  const config = TagConfigLoader.load(configPath);
  const tagParser = new TagParser(config);
  return new ComponentRecognizer(null, {
    enableAI: false,
    aiThreshold: 0.8,
    cvConfidenceMin: 0.7,
  }, tagParser);
}

describe('TagParser Integration: 完整链路测试', () => {
  let recognizer: ComponentRecognizer;

  beforeEach(() => {
    recognizer = makeRecognizer();
  });

  test('多标签叠加: close.bt.tmp.bg → Button + tmp + bg', async () => {
    const layer = makeLayer('close.bt.tmp.bg');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('Button');
    expect(info.textBackend).toBe('tmp');
    expect(info.role).toBe('bg');
    expect(info.confidence).toBe(1.0);
    expect(info.source).toBe('tag');
    expect(info.needsReview).toBe(false);
  });

  test('多标签叠加: icon.img.sliced → Image + sliced', async () => {
    const layer = makeLayer('icon.img.sliced');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('Image');
    expect(info.imageType).toBe('sliced');
    expect(info.confidence).toBe(1.0);
    expect(info.source).toBe('tag');
  });

  test('多标签叠加: input.ipt.tmp.placeholder → InputField + tmp + placeholder', async () => {
    const layer = makeLayer('input.ipt.tmp.placeholder');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('InputField');
    expect(info.textBackend).toBe('tmp');
    expect(info.role).toBe('placeholder');
    expect(info.source).toBe('tag');
  });

  test('右→左优先级: panel.bt.dpd → Dropdown（dpd 覆盖 bt）', async () => {
    const layer = makeLayer('panel.bt.dpd');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('Dropdown');
    expect(info.source).toBe('tag');
  });

  test('右→左优先级: icon.sliced.simple → simple（simple 覆盖 sliced）', async () => {
    const layer = makeLayer('icon.sliced.simple');
    const info = await recognizer.recognize(layer);
    expect(info.imageType).toBe('simple');
    expect(info.source).toBe('tag');
  });

  test('ref 前缀: ref icon.img → Image', async () => {
    const layer = makeLayer('ref icon.img');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('Image');
    expect(info.source).toBe('tag');
  });

  test('refp 前缀: refp panel.bt → Button', async () => {
    const layer = makeLayer('refp panel.bt');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('Button');
    expect(info.source).toBe('tag');
  });

  test('未识别标签跳过: name.unknowntag.bt → Button', async () => {
    const layer = makeLayer('name.unknowntag.bt');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('Button');
    expect(info.source).toBe('tag');
  });

  test('无标签图层 AI 禁用: background → Unknown', async () => {
    const layer = makeLayer('background');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('Unknown');
    expect(info.confidence).toBe(0);
    expect(info.source).toBe('ai');
    expect(info.needsReview).toBe(false);
  });

  test('全是未识别标签: name.xyz.abc → Unknown', async () => {
    const layer = makeLayer('name.xyz.abc');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('Unknown');
    expect(info.source).toBe('ai');
  });

  test('新组件类型: dropdown.dpd → Dropdown', async () => {
    const layer = makeLayer('dropdown.dpd');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('Dropdown');
  });

  test('新组件类型: toggle.tg → Toggle', async () => {
    const layer = makeLayer('toggle.tg');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('Toggle');
  });

  test('新组件类型: slider.sld → Slider', async () => {
    const layer = makeLayer('slider.sld');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('Slider');
  });

  test('新组件类型: raw.rimg → RawImage', async () => {
    const layer = makeLayer('raw.rimg');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('RawImage');
  });

  test('新组件类型: mask.msk → Mask', async () => {
    const layer = makeLayer('mask.msk');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('Mask');
  });

  test('新组件类型: fill.col → FillColor', async () => {
    const layer = makeLayer('fill.col');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('FillColor');
  });

  test('布局组件: list.vbox → VerticalLayoutGroup', async () => {
    const layer = makeLayer('list.vbox');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('VerticalLayoutGroup');
  });

  test('布局组件: row.hbox → HorizontalLayoutGroup', async () => {
    const layer = makeLayer('row.hbox');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('HorizontalLayoutGroup');
  });

  test('布局组件: grid.grid → GridLayoutGroup', async () => {
    const layer = makeLayer('grid.grid');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('GridLayoutGroup');
  });

  test('边界: 空字符串 → Unknown', async () => {
    const layer = makeLayer('');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('Unknown');
  });

  test('边界: 只有标签无 baseName: bt.tmp → Button + tmp', async () => {
    const layer = makeLayer('bt.tmp');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('Button');
    expect(info.textBackend).toBe('tmp');
  });

  test('recognizeTree: 父子节点各自识别', async () => {
    const parent: Layer = {
      id: 'parent',
      name: 'panel.bt',
      type: 'group',
      bounds: { x: 0, y: 0, width: 200, height: 200 },
      visible: true,
      opacity: 1,
      children: [
        {
          id: 'child',
          name: 'bg.img.sliced',
          type: 'image',
          bounds: { x: 0, y: 0, width: 200, height: 200 },
          visible: true,
          opacity: 1,
          children: [],
        },
      ],
    };

    const results = await recognizer.recognizeTree(parent);
    expect(results.size).toBe(2);
    expect(results.get('parent')?.type).toBe('Button');
    expect(results.get('child')?.type).toBe('Image');
    expect(results.get('child')?.imageType).toBe('sliced');
  });
});
