// tests/unit/parser.test.ts
import { PsdParser } from '../../src/parser/psd-parser';
import { AssetExporter } from '../../src/parser/asset-exporter';
import { LayerTree } from '../../src/parser/layer-tree';

// Mock ag-psd
jest.mock('ag-psd', () => ({
  readPsd: jest.fn(() => ({
    width: 1920,
    height: 1080,
    name: 'test.psd',
    children: [
      {
        name: 'btn_close',
        left: 100,
        top: 50,
        right: 180,
        bottom: 130,
        opacity: 255,
        hidden: false,
        children: [
          {
            name: 'bg',
            left: 100,
            top: 50,
            right: 180,
            bottom: 130,
            opacity: 255,
            imageData: {},
          },
        ],
      },
    ],
  })),
}));

// Mock fs/promises (used by PsdParser for readFile, AssetExporter for mkdir)
jest.mock('fs/promises', () => ({
  readFile: jest.fn(() => Promise.resolve(Buffer.from('mock-psd-data'))),
  mkdir: jest.fn(() => Promise.resolve()),
}));

// Mock fs (used by AssetExporter for existsSync)
jest.mock('fs', () => ({
  existsSync: jest.fn(() => true),
}));

describe('PsdParser', () => {
  let parser: PsdParser;

  beforeEach(() => {
    parser = new PsdParser();
  });

  test('should parse PSD file and return LayerTree', async () => {
    const tree: LayerTree = await parser.parse('/path/to/test.psd');

    expect(tree.metadata.psdPath).toBe('/path/to/test.psd');
    expect(tree.metadata.canvasSize.width).toBe(1920);
    expect(tree.metadata.canvasSize.height).toBe(1080);
    expect(tree.root.id).toBe('root');
    expect(tree.root.children).toHaveLength(1);
  });

  test('should correctly extract layer properties', async () => {
    const tree = await parser.parse('/path/to/test.psd');
    const firstLayer = tree.root.children![0];

    expect(firstLayer.name).toBe('btn_close');
    expect(firstLayer.type).toBe('group');
    expect(firstLayer.bounds).toEqual({ x: 100, y: 50, width: 80, height: 80 });
    expect(firstLayer.visible).toBe(true);
    expect(firstLayer.opacity).toBeCloseTo(1.0);
  });

  test('should handle nested layers', async () => {
    const tree = await parser.parse('/path/to/test.psd');
    const parentLayer = tree.root.children![0];
    const childLayer = parentLayer.children![0];

    expect(childLayer.name).toBe('bg');
    expect(childLayer.type).toBe('image');
    expect(childLayer.id).toBe('root_0_0');
  });
});

describe('AssetExporter', () => {
  let exporter: AssetExporter;

  beforeEach(() => {
    exporter = new AssetExporter();
  });

  test('should generate correct file path', async () => {
    const layer = {
      id: 'layer_001',
      name: 'test_image',
      type: 'image' as const,
      bounds: { x: 0, y: 0, width: 100, height: 100 },
      visible: true,
      opacity: 1.0,
    };

    // export() currently throws "Not implemented" but includes the generated path
    await expect(exporter.export(layer, '/output')).rejects.toThrow(
      /test_image_layer_001\.png/
    );
  });

  test('should sanitize file names with special characters', async () => {
    const layer = {
      id: 'layer_002',
      name: 'test<image>:file',
      type: 'image' as const,
      bounds: { x: 0, y: 0, width: 100, height: 100 },
      visible: true,
      opacity: 1.0,
    };

    await expect(exporter.export(layer, '/output')).rejects.toThrow(
      /test_image__file_layer_002\.png/
    );
  });

  test('should throw error for non-exportable layer types', async () => {
    const layer = {
      id: 'layer_003',
      name: 'group_layer',
      type: 'group' as const,
      bounds: { x: 0, y: 0, width: 100, height: 100 },
      visible: true,
      opacity: 1.0,
    };

    await expect(exporter.export(layer, '/output')).rejects.toThrow(
      'Cannot export layer type: group'
    );
  });
});
