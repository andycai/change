// tests/unit/parser.test.ts
import { PsdParser } from '../../src/parser/psd-parser';
import { AssetExporter } from '../../src/parser/asset-exporter';
import { LayerTree } from '../../src/parser/layer-tree';

// Mock ag-psd
jest.mock('ag-psd', () => ({
  initializeCanvas: jest.fn(),
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
        opacity: 1,
        hidden: false,
        children: [
          {
            name: 'bg',
            left: 100,
            top: 50,
            right: 180,
            bottom: 130,
            opacity: 1,
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

jest.mock('sharp', () =>
  jest.fn(() => ({
    toFile: jest.fn(() => Promise.resolve()),
  })),
);

// Import mocked modules for per-test overrides and call verification
import * as agPsd from 'ag-psd';
import * as fsPromises from 'fs/promises';
import * as fs from 'fs';

const mockedReadPsd = agPsd.readPsd as jest.Mock;
const mockedReadFile = fsPromises.readFile as jest.Mock;
const mockedMkdir = fsPromises.mkdir as jest.Mock;
const mockedExistsSync = fs.existsSync as jest.Mock;

describe('PsdParser', () => {
  let parser: PsdParser;

  beforeEach(() => {
    parser = new PsdParser();
    jest.clearAllMocks();
  });

  test('should parse PSD file and return LayerTree', async () => {
    const tree: LayerTree = await parser.parse('/path/to/test.psd');

    expect(tree.metadata.psdPath).toBe('/path/to/test.psd');
    expect(tree.metadata.canvasSize.width).toBe(1920);
    expect(tree.metadata.canvasSize.height).toBe(1080);
    expect(tree.root.id).toBe('root');
    expect(tree.root.children).toHaveLength(1);
    // Verify readFile was called with the correct path
    expect(mockedReadFile).toHaveBeenCalledTimes(1);
    expect(mockedReadFile).toHaveBeenCalledWith('/path/to/test.psd');
  });

  test('should correctly extract layer properties', async () => {
    const tree = await parser.parse('/path/to/test.psd');
    const firstLayer = tree.root.children![0];

    expect(firstLayer.name).toBe('btn_close');
    expect(firstLayer.type).toBe('group');
    expect(firstLayer.bounds).toEqual({ x: 100, y: 50, width: 80, height: 80 });
    expect(firstLayer.visible).toBe(true);
    expect(firstLayer.opacity).toBeCloseTo(1.0);
    expect(mockedReadFile).toHaveBeenCalledWith('/path/to/test.psd');
  });

  test('should preserve PSD text content', async () => {
    mockedReadPsd.mockReturnValueOnce({
      width: 200,
      height: 100,
      children: [{
        name: 'title.tmptxt',
        left: 10,
        top: 20,
        right: 190,
        bottom: 60,
        text: { text: '开始游戏' },
      }],
    });

    const tree = await parser.parse('/path/to/text.psd');

    expect(tree.root.children![0].text).toBe('开始游戏');
  });

  test('should handle nested layers', async () => {
    const tree = await parser.parse('/path/to/test.psd');
    const parentLayer = tree.root.children![0];
    const childLayer = parentLayer.children![0];

    expect(childLayer.name).toBe('bg');
    expect(childLayer.type).toBe('image');
    expect(childLayer.id).toBe('root_0_0');
  });

  test('should throw when readPsd raises an error', async () => {
    mockedReadPsd.mockImplementationOnce(() => {
      throw new Error('Corrupt PSD data');
    });

    await expect(parser.parse('/path/to/bad.psd')).rejects.toThrow(
      'Failed to parse PSD file: /path/to/bad.psd'
    );
    expect(mockedReadFile).toHaveBeenCalledWith('/path/to/bad.psd');
  });

  test('should throw when readPsd returns null', async () => {
    mockedReadPsd.mockReturnValueOnce(null);

    await expect(parser.parse('/path/to/null.psd')).rejects.toThrow(
      'Failed to parse PSD file: /path/to/null.psd'
    );
    expect(mockedReadFile).toHaveBeenCalledWith('/path/to/null.psd');
  });
});

describe('AssetExporter', () => {
  let exporter: AssetExporter;

  beforeEach(() => {
    exporter = new AssetExporter();
    jest.clearAllMocks();
  });

  describe('sanitizeFileName', () => {
    test('should generate correct file name from layer name and id', () => {
      const result = exporter.sanitizeFileName('test_image');
      expect(result).toBe('test_image');
    });

    test('should sanitize file names with special characters', () => {
      const result = exporter.sanitizeFileName('test<image>:file');
      expect(result).toBe('test_image__file');
    });

    test('should replace whitespace with underscores', () => {
      const result = exporter.sanitizeFileName('hello world  foo');
      expect(result).toBe('hello_world_foo');
    });

    test('should limit file name length to 50 characters', () => {
      const longName = 'a'.repeat(100);
      const result = exporter.sanitizeFileName(longName);
      expect(result.length).toBe(50);
    });
  });

  describe('export', () => {
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

    test('should call mkdir when output directory does not exist', async () => {
      mockedExistsSync.mockReturnValueOnce(false);

      const layer = {
        id: 'layer_001',
        name: 'test_image',
        type: 'image' as const,
        bounds: { x: 0, y: 0, width: 100, height: 100 },
        visible: true,
        opacity: 1.0,
      };

      await exporter.export(
        { ...layer, _canvas: { width: 100, height: 100, toBuffer: jest.fn(() => Buffer.from('png')) } },
        '/output',
      );
      expect(mockedMkdir).toHaveBeenCalledWith('/output', { recursive: true });
    });

    test('should not call mkdir when output directory already exists', async () => {
      mockedExistsSync.mockReturnValueOnce(true);

      const layer = {
        id: 'layer_002',
        name: 'test_image',
        type: 'image' as const,
        bounds: { x: 0, y: 0, width: 100, height: 100 },
        visible: true,
        opacity: 1.0,
      };

      await exporter.export(
        { ...layer, _canvas: { width: 100, height: 100, toBuffer: jest.fn(() => Buffer.from('png')) } },
        '/output',
      );
      expect(mockedMkdir).not.toHaveBeenCalled();
    });
  });
});
