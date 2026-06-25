import { PsdParser } from '../../src/parser/psd-parser';
import { ComponentRecognizer } from '../../src/recognizer/component-recognizer';
import { TagParser } from '../../src/recognizer/tag-parser';
import { TagConfigLoader } from '../../src/recognizer/tag-config-loader';
import { JsonGenerator } from '../../src/generator/json-generator';
import { JsonConfigSchema, JsonConfig, LayerComponentMapping } from '../../src/generator/json-schema';
import { LayerTree, Layer } from '../../src/parser/layer-tree';
import { ComponentInfo } from '../../src/recognizer/component-types';
import * as path from 'path';

jest.mock('ag-psd', () => {
  function createMockPsd() {
    return {
      width: 1920, height: 1080, name: 'main-menu.psd',
      children: [
        { name: 'panel_bg.img', left: 0, top: 0, right: 1920, bottom: 1080, opacity: 255, hidden: false, imageData: {} },
        { name: 'title.txt', left: 400, top: 50, right: 1520, bottom: 150, opacity: 255, text: {} },
        { name: 'button_list.vbox', left: 600, top: 300, right: 1320, bottom: 800, opacity: 255,
          children: [
            { name: 'start.bt', left: 600, top: 300, right: 1320, bottom: 400, opacity: 255, imageData: {} },
            { name: 'settings.bt', left: 600, top: 420, right: 1320, bottom: 520, opacity: 255, imageData: {} },
            { name: 'quit.bt', left: 600, top: 540, right: 1320, bottom: 640, opacity: 255, imageData: {} },
          ],
        },
        { name: 'shop_list.sv', left: 50, top: 850, right: 1870, bottom: 1050, opacity: 255,
          children: [
            { name: 'items_container', left: 50, top: 850, right: 1870, bottom: 1050, opacity: 255,
              children: [
                { name: 'item1.img', left: 50, top: 850, right: 250, bottom: 1050, opacity: 255, imageData: {} },
                { name: 'item2.img', left: 270, top: 850, right: 470, bottom: 1050, opacity: 255, imageData: {} },
                { name: 'item3.img', left: 490, top: 850, right: 690, bottom: 1050, opacity: 255, imageData: {} },
              ],
            },
          ],
        },
        { name: 'search.ipt', left: 200, top: 200, right: 800, bottom: 260, opacity: 255, text: {} },
        { name: 'debug_tools.hbox', left: 0, top: 0, right: 1920, bottom: 100, opacity: 128, hidden: true,
          children: [
            { name: 'debug1.bt', left: 0, top: 0, right: 200, bottom: 100, opacity: 128, hidden: true, imageData: {} },
          ],
        },
      ],
    };
  }
  return { readPsd: jest.fn(() => createMockPsd()) };
});

jest.mock('fs/promises', () => ({
  readFile: jest.fn(() => Promise.resolve(Buffer.from('mock-psd-data'))),
  writeFile: jest.fn(() => Promise.resolve()),
  mkdir: jest.fn(() => Promise.resolve()),
}));

jest.mock('fs', () => ({
  existsSync: jest.fn(() => true),
  readFileSync: jest.fn(() => Buffer.from('mock-image-data')),
}));

describe('End-to-End: parse -> recognize -> generate', () => {
  let parser: PsdParser;
  let generator: JsonGenerator;
  let layerTree: LayerTree;
  let components: Map<string, ComponentInfo>;
  let jsonConfig: JsonConfig;

  beforeAll(async () => {
    parser = new PsdParser();
    layerTree = await parser.parse('/test/main-menu.psd');

    // Load real config to bypass the fs mock
    const realFs = jest.requireActual('fs') as typeof import('fs');
    const configPath = path.resolve(__dirname, '../../config/tag-config.json');
    const configContent = realFs.readFileSync(configPath, 'utf-8');
    const config = JSON.parse(configContent);
    const tagParser = new TagParser(config);

    const recognizer = new ComponentRecognizer(null, {
      enableAI: false, aiThreshold: 0.7, cvConfidenceMin: 0.6,
    }, tagParser);
    components = await recognizer.recognizeTree(layerTree.root);

    generator = new JsonGenerator();
    jsonConfig = generator.generate(layerTree, components);
  });

  describe('Phase 1: PSD Parsing', () => {
    test('should produce valid LayerTree', () => {
      expect(layerTree).toBeDefined();
      expect(layerTree.metadata).toBeDefined();
    });

    test('should extract correct metadata', () => {
      expect(layerTree.metadata.psdPath).toBe('/test/main-menu.psd');
      expect(layerTree.metadata.canvasSize).toEqual({ x: 0, y: 0, width: 1920, height: 1080 });
    });

    test('should parse all top-level layers', () => {
      const names = layerTree.root.children!.map((c: Layer) => c.name);
      expect(names).toContain('panel_bg.img');
      expect(names).toContain('title.txt');
      expect(names).toContain('button_list.vbox');
      expect(names).toContain('shop_list.sv');
      expect(names).toContain('search.ipt');
      expect(names).toContain('debug_tools.hbox');
    });

    test('should mark hidden layers as not visible', () => {
      const dt = layerTree.root.children!.find((c: Layer) => c.name === 'debug_tools.hbox');
      expect(dt!.visible).toBe(false);
    });

    test('should calculate correct opacity', () => {
      const dt = layerTree.root.children!.find((c: Layer) => c.name === 'debug_tools.hbox');
      expect(dt!.opacity).toBeCloseTo(0.5, 2);
    });

    test('should parse nested children recursively', () => {
      const sl = layerTree.root.children!.find((c: Layer) => c.name === 'shop_list.sv');
      expect(sl!.children![0].name).toBe('items_container');
      expect(sl!.children![0].children!.length).toBe(3);
    });
  });

  describe('Phase 2: Component Recognition', () => {
    test('should recognize all layers', () => {
      expect(components.size).toBeGreaterThanOrEqual(10);
    });

    test('should recognize tagged layers correctly', () => {
      expect(components.get('root_0')!.type).toBe('Image');
      expect(components.get('root_1')!.type).toBe('Text');
      expect(components.get('root_2')!.type).toBe('VerticalLayoutGroup');
      expect(components.get('root_3')!.type).toBe('ScrollView');
      expect(components.get('root_4')!.type).toBe('InputField');
      expect(components.get('root_5')!.type).toBe('HorizontalLayoutGroup');
    });

    test('should have tag source with 100% confidence', () => {
      const imgBg = components.get('root_0')!;
      expect(imgBg.source).toBe('tag');
      expect(imgBg.confidence).toBe(1.0);
      expect(imgBg.needsReview).toBe(false);
    });

    test('should return Unknown for untagged layers', () => {
      const gi = layerTree.root.children![3].children![0];
      expect(components.get(gi.id)!.type).toBe('Unknown');
    });
  });

  describe('Phase 3: JSON Generation', () => {
    test('should generate valid JSON config', () => {
      expect(JsonConfigSchema.safeParse(jsonConfig).success).toBe(true);
    });

    test('should have correct metadata', () => {
      expect(jsonConfig.metadata.psdPath).toBe('/test/main-menu.psd');
      expect(jsonConfig.metadata.generator).toBe('psd-exporter');
      expect(jsonConfig.metadata.generatedAt).toBeTruthy();
    });

    test('should have layers and components', () => {
      expect(jsonConfig.layers.length).toBeGreaterThanOrEqual(1);
      expect(jsonConfig.components.length).toBe(components.size);
    });

    test('should have correct component mapping structure', () => {
      const fc = jsonConfig.components[0];
      expect(fc.component).toHaveProperty('type');
      expect(fc.component).toHaveProperty('confidence');
      expect(fc.component).toHaveProperty('source');
      expect(fc.component).toHaveProperty('needsReview');
    });

    test('should preserve nested layer structure', () => {
      expect(jsonConfig.layers[0].children!.length).toBe(6);
    });

    test('should serialize and save valid JSON', async () => {
      const writeFile = require('fs/promises').writeFile as jest.Mock;
      writeFile.mockClear();
      await generator.save(jsonConfig, '/output/e2e-result.json');
      expect(writeFile).toHaveBeenCalledTimes(1);
      const parsed = JSON.parse(writeFile.mock.calls[0][1]);
      expect(parsed.metadata.psdPath).toBe('/test/main-menu.psd');
      expect(writeFile.mock.calls[0][1]).toContain('\n');
    });
  });

  describe('Full pipeline: all-tags scenario', () => {
    test('should handle PSD with many tagged layers', () => {
      expect(JsonConfigSchema.safeParse(jsonConfig).success).toBe(true);
      const tagged = jsonConfig.components.filter(
        (c: LayerComponentMapping) => c.component.source === 'tag',
      );
      expect(tagged.length).toBeGreaterThan(0);
      for (const tc of tagged) {
        expect(tc.component.confidence).toBe(1.0);
        expect(tc.component.needsReview).toBe(false);
      }
    });

    test('should produce complete output with all sections', () => {
      expect(jsonConfig).toHaveProperty('metadata');
      expect(jsonConfig).toHaveProperty('layers');
      expect(jsonConfig).toHaveProperty('components');
      expect(jsonConfig.metadata).toHaveProperty('generator');
      expect(jsonConfig.metadata).toHaveProperty('version');
    });

    test('should correctly map components to layers', () => {
      const ids = new Set(jsonConfig.components.map((c: LayerComponentMapping) => c.layerId));
      expect(ids.size).toBeGreaterThan(0);
      for (const lid of ids) expect(components.get(lid)).toBeDefined();
    });
  });
});
