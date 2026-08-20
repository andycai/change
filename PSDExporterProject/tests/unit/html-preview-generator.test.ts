import { HtmlPreviewGenerator } from '../../src/generator/html-preview-generator';
import { Layer, LayerTree } from '../../src/parser/layer-tree';
import { ComponentInfo } from '../../src/recognizer/component-types';

jest.mock('fs/promises', () => ({
  writeFile: jest.fn(() => Promise.resolve()),
}));

import * as fsPromises from 'fs/promises';

const mockedWriteFile = fsPromises.writeFile as jest.Mock;

function layer(
  id: string,
  name: string,
  type: Layer['type'],
  bounds: Layer['bounds'],
  overrides: Partial<Layer> = {},
): Layer {
  return {
    id,
    name,
    type,
    bounds,
    visible: true,
    opacity: 1,
    ...overrides,
  };
}

function makeLayerTree(): LayerTree {
  return {
    root: layer('root', 'canvas', 'group', { x: 0, y: 0, width: 400, height: 300 }, {
      children: [
        layer('image', 'background', 'image', { x: 0, y: 0, width: 400, height: 300 }, { assetPath: '/assets/background.png' }),
        layer('title', 'title.txt', 'text', { x: 20, y: 20, width: 160, height: 32 }, {
          text: 'Hello <PSD>',
          textStyles: {
            fontSize: 20,
            color: { r: 1, g: 1, b: 1, a: 1 },
            fontName: 'Arial',
            fontStyle: { bold: true, italic: false },
            alignment: { horizontal: 'left', vertical: 'top' },
          },
        }),
        layer('button', 'submit.bt', 'group', { x: 20, y: 70, width: 120, height: 40 }, {
          children: [layer('button-bg', 'button-bg', 'shape', { x: 20, y: 70, width: 120, height: 40 })],
        }),
        layer('toggle', 'sound.tg', 'group', { x: 20, y: 120, width: 80, height: 40 }, {
          children: [layer('toggle-mark', 'mark.img.mark', 'image', { x: 30, y: 125, width: 20, height: 20 })],
        }),
        layer('input', 'name.ipt', 'group', { x: 20, y: 170, width: 180, height: 40 }, {
          children: [layer('placeholder', 'Placeholder.tips', 'text', { x: 25, y: 175, width: 100, height: 20 }, { text: '请输入姓名' })],
        }),
        layer('slider', 'volume.sld', 'group', { x: 20, y: 220, width: 180, height: 24 }, {
          children: [layer('slider-bg', 'SliderBg', 'shape', { x: 20, y: 220, width: 180, height: 24 })],
        }),
        layer('dropdown', 'language.dpd', 'group', { x: 220, y: 170, width: 150, height: 120 }, {
          children: [
            layer('dropdown-label', 'DropdownLabel.label', 'text', { x: 225, y: 175, width: 100, height: 24 }, { text: '中文' }),
            layer('dropdown-list', 'DropdownListView.sv', 'group', { x: 220, y: 210, width: 150, height: 80 }, {
              children: [layer('dropdown-bg', 'list.bg', 'shape', { x: 220, y: 210, width: 150, height: 80 })],
            }),
            layer('dropdown-item', 'DropdownItem.tg', 'group', { x: 230, y: 220, width: 120, height: 30 }, {
              children: [layer('item-label', 'DropdownItemLabel.label', 'text', { x: 235, y: 222, width: 90, height: 20 }, { text: 'English' })],
            }),
          ],
        }),
        layer('scroll', 'content.sv', 'group', { x: 220, y: 20, width: 150, height: 120 }, {
          children: [layer('scroll-title', 'scroll title', 'text', { x: 230, y: 30, width: 100, height: 24 }, { text: '可滚动内容' })],
        }),
      ],
    }),
    metadata: {
      psdPath: '/designs/main-menu<&>.psd',
      canvasSize: { x: 0, y: 0, width: 400, height: 300 },
      timestamp: '2026-08-20T00:00:00.000Z',
    },
  };
}

function makeComponents(): Map<string, ComponentInfo> {
  const types: Array<[string, ComponentInfo['type']]> = [
    ['button', 'Button'],
    ['toggle', 'Toggle'],
    ['input', 'InputField'],
    ['slider', 'Slider'],
    ['dropdown', 'Dropdown'],
    ['scroll', 'ScrollView'],
  ];
  return new Map(types.map(([id, type]) => [id, {
    type,
    confidence: 1,
    source: 'tag',
    needsReview: false,
  }]));
}

describe('HtmlPreviewGenerator', () => {
  let generator: HtmlPreviewGenerator;

  beforeEach(() => {
    generator = new HtmlPreviewGenerator();
    jest.clearAllMocks();
  });

  test('should generate layered DOM with real interactive controls', () => {
    const html = generator.generate(makeLayerTree(), makeComponents(), '/output/main-menu.html');

    expect(html).toContain('width: 400px');
    expect(html).toContain('height: 300px');
    expect(html).toContain('data-component="Button"');
    expect(html).toContain('type="checkbox"');
    expect(html).toContain('type="text"');
    expect(html).toContain('placeholder="请输入姓名"');
    expect(html).toContain('type="range"');
    expect(html).toContain('data-component="Dropdown"');
    expect(html).toContain('data-dropdown-trigger');
    expect(html).toContain('data-dropdown-menu');
    expect(html).toContain('data-dropdown-option');
    expect(html).toContain('data-dropdown-option-text');
    expect(html).toContain('data-component="ScrollView"');
    expect(html).toContain('Hello &lt;PSD&gt;');
    expect(html).toContain('src="../assets/background.png"');
    expect(html).not.toContain('data:image/png;base64');
    expect(html).toContain('document.querySelectorAll(\'[data-component="Button"]\')');
  });

  test('should escape document title and layer text', () => {
    const html = generator.generate(makeLayerTree(), makeComponents());

    expect(html).toContain('main-menu&lt;&amp;&gt;.psd');
    expect(html).not.toContain('main-menu<&>.psd');
  });

  test('should save generated HTML as UTF-8', async () => {
    const html = generator.generate(makeLayerTree(), makeComponents());

    await generator.save(html, '/output/main-menu.html');

    expect(mockedWriteFile).toHaveBeenCalledWith(
      '/output/main-menu.html',
      expect.stringContaining('data-component="Dropdown"'),
      'utf-8',
    );
  });
});
