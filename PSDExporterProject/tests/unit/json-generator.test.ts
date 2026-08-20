import { JsonGenerator } from '../../src/generator/json-generator';
import { JsonConfigSchema } from '../../src/generator/json-schema';
import { LayerTree, Layer } from '../../src/parser/layer-tree';
import { ComponentInfo } from '../../src/recognizer/component-types';

jest.mock('fs/promises', () => ({ writeFile: jest.fn(() => Promise.resolve()) }));
import * as fsPromises from 'fs/promises';
const mockedWriteFile = fsPromises.writeFile as jest.Mock;

function makeLayer(overrides: Partial<Layer> = {}): Layer {
  return {
    id: 'layer_0', name: 'unnamed', type: 'image',
    bounds: { x: 0, y: 0, width: 100, height: 100 },
    visible: true, opacity: 1.0, ...overrides,
  };
}

describe('JsonGenerator', () => {
  let generator: JsonGenerator;
  beforeEach(() => { generator = new JsonGenerator(); jest.clearAllMocks(); });

  describe('generate', () => {
    test('should generate config with metadata', () => {
      const tree: LayerTree = {
        root: makeLayer({ id: 'root', name: 'root', type: 'group', children: [] }),
        metadata: { psdPath: '/test.psd', canvasSize: { x: 0, y: 0, width: 1920, height: 1080 }, timestamp: '2026-06-24T10:00:00Z' },
      };
      const config = generator.generate(tree, new Map());
      expect(config.metadata.psdPath).toBe('/test.psd');
      expect(config.metadata.generator).toBe('psd-exporter');
      expect(config.metadata.generatedAt).toBeTruthy();
    });

    test('should flatten layer tree into layers array', () => {
      const tree: LayerTree = {
        root: {
          id: 'root', name: 'canvas', type: 'group',
          bounds: { x: 0, y: 0, width: 1920, height: 1080 }, visible: true, opacity: 1.0,
          children: [
            makeLayer({ id: 'root_0', name: 'btn_close', type: 'group' }),
            makeLayer({ id: 'root_1', name: 'img_bg', type: 'image' }),
          ],
        },
        metadata: { psdPath: '/test.psd', canvasSize: { x: 0, y: 0, width: 1920, height: 1080 }, timestamp: 't' },
      };
      const config = generator.generate(tree, new Map());
      expect(config.layers).toHaveLength(1);
      expect(config.layers[0].children).toHaveLength(2);
      expect(config.layers[0].children![0].id).toBe('root_0');
    });

    test('should attach component info to components array', () => {
      const tree: LayerTree = {
        root: makeLayer({ id: 'root', name: 'canvas', type: 'group', children: [] }),
        metadata: { psdPath: '/t.psd', canvasSize: { x: 0, y: 0, width: 100, height: 100 }, timestamp: 't' },
      };
      const comps = new Map<string, ComponentInfo>();
      comps.set('root', { type: 'Button', confidence: 1.0, source: 'tag', needsReview: false });
      const config = generator.generate(tree, comps);
      expect(config.components).toHaveLength(1);
      expect(config.components[0].component.type).toBe('Button');
    });

    test('should include assetPath when present', () => {
      const tree: LayerTree = {
        root: makeLayer({ id: 'root', name: 'icon', type: 'image', assetPath: '/assets/icon.png' }),
        metadata: { psdPath: '/t.psd', canvasSize: { x: 0, y: 0, width: 100, height: 100 }, timestamp: 't' },
      };
      const config = generator.generate(tree, new Map());
      expect(config.layers[0].assetPath).toBe('/assets/icon.png');
    });

    test('should include text content when present', () => {
      const tree: LayerTree = {
        root: makeLayer({ id: 'root', name: 'title', type: 'text', text: '开始游戏' }),
        metadata: { psdPath: '/t.psd', canvasSize: { x: 0, y: 0, width: 100, height: 100 }, timestamp: 't' },
      };
      const config = generator.generate(tree, new Map());
      expect(config.layers[0].text).toBe('开始游戏');
    });

    test('should not include assetPath when not set', () => {
      const tree: LayerTree = {
        root: makeLayer({ id: 'root', name: 'icon', type: 'image' }),
        metadata: { psdPath: '/t.psd', canvasSize: { x: 0, y: 0, width: 100, height: 100 }, timestamp: 't' },
      };
      const config = generator.generate(tree, new Map());
      expect(config.layers[0].assetPath).toBeUndefined();
    });

    test('should handle deeply nested layers', () => {
      const tree: LayerTree = {
        root: {
          id: 'root', name: 'canvas', type: 'group',
          bounds: { x: 0, y: 0, width: 1920, height: 1080 }, visible: true, opacity: 1.0,
          children: [{
            ...makeLayer({ id: 'root_0', name: 'panel', type: 'group' }),
            children: [{ ...makeLayer({ id: 'root_0_0', name: 'nested', type: 'group' }), children: [makeLayer({ id: 'root_0_0_0', name: 'btn_deep', type: 'image' })] }],
          }],
        },
        metadata: { psdPath: '/t.psd', canvasSize: { x: 0, y: 0, width: 1920, height: 1080 }, timestamp: 't' },
      };
      const config = generator.generate(tree, new Map());
      expect(config.layers[0].children![0].children![0].children![0].name).toBe('btn_deep');
    });

    test('should validate against schema', () => {
      const tree: LayerTree = {
        root: makeLayer({ id: 'root', name: 'canvas', type: 'group', children: [] }),
        metadata: { psdPath: '/t.psd', canvasSize: { x: 0, y: 0, width: 1920, height: 1080 }, timestamp: 't' },
      };
      const comps = new Map<string, ComponentInfo>();
      comps.set('root', { type: 'Button', confidence: 1.0, source: 'tag', needsReview: false });
      expect(JsonConfigSchema.safeParse(generator.generate(tree, comps)).success).toBe(true);
    });
  });

  describe('save', () => {
    test('should write valid JSON to file', async () => {
      const tree: LayerTree = {
        root: makeLayer({ id: 'root', name: 'canvas', type: 'group', children: [] }),
        metadata: { psdPath: '/t.psd', canvasSize: { x: 0, y: 0, width: 1920, height: 1080 }, timestamp: 't' },
      };
      const config = generator.generate(tree, new Map());
      await generator.save(config, '/output/config.json');
      expect(mockedWriteFile).toHaveBeenCalledWith('/output/config.json', expect.any(String), 'utf-8');
      const json = JSON.parse(mockedWriteFile.mock.calls[0][1]);
      expect(json.metadata.psdPath).toBe('/t.psd');
    });

    test('should throw on invalid config', async () => {
      await expect(generator.save({ metadata: { psdPath: 123 }, layers: [], components: [] } as any, '/o/c.json'))
        .rejects.toThrow('JSON config validation failed');
    });

    test('should pretty-print JSON', async () => {
      const tree: LayerTree = {
        root: makeLayer({ id: 'root', name: 'canvas', type: 'group', children: [] }),
        metadata: { psdPath: '/t.psd', canvasSize: { x: 0, y: 0, width: 1920, height: 1080 }, timestamp: 't' },
      };
      await generator.save(generator.generate(tree, new Map()), '/o/c.json');
      expect(mockedWriteFile.mock.calls[0][1]).toContain('\n');
    });

    test('should throw on writeFile failure', async () => {
      mockedWriteFile.mockRejectedValueOnce(new Error('Disk full'));
      const tree: LayerTree = {
        root: makeLayer({ id: 'root', name: 'canvas', type: 'group', children: [] }),
        metadata: { psdPath: '/t.psd', canvasSize: { x: 0, y: 0, width: 1920, height: 1080 }, timestamp: 't' },
      };
      await expect(generator.save(generator.generate(tree, new Map()), '/o/c.json')).rejects.toThrow('Disk full');
    });
  });
});
