import { PsdParser } from '../../src/parser/psd-parser';
import { ComponentRecognizer } from '../../src/recognizer/component-recognizer';
import { AiIdentifier } from '../../src/recognizer/ai-identifier';
import { JsonGenerator } from '../../src/generator/json-generator';
import { ConfigLoader, Config } from '../../src/config/config-loader';
import { LayerTree } from '../../src/parser/layer-tree';
import { ComponentInfo } from '../../src/recognizer/component-types';

jest.mock('../../src/parser/psd-parser');
jest.mock('../../src/recognizer/component-recognizer');
jest.mock('../../src/recognizer/ai-identifier');
jest.mock('../../src/generator/json-generator');
jest.mock('../../src/config/config-loader');
jest.mock('fs/promises', () => ({
  readFile: jest.fn(() => Promise.resolve(Buffer.from('mock-psd'))),
  writeFile: jest.fn(() => Promise.resolve()),
}));

const MockedPsdParser = PsdParser as jest.MockedClass<typeof PsdParser>;
const MockedComponentRecognizer = ComponentRecognizer as jest.MockedClass<typeof ComponentRecognizer>;
const MockedAiIdentifier = AiIdentifier as jest.MockedClass<typeof AiIdentifier>;
const MockedJsonGenerator = JsonGenerator as jest.MockedClass<typeof JsonGenerator>;

function makeMockLayerTree(): LayerTree {
  return {
    root: { id: 'root', name: 'canvas', type: 'group', bounds: { x: 0, y: 0, width: 1920, height: 1080 }, visible: true, opacity: 1.0, children: [] },
    metadata: { psdPath: '/test.psd', canvasSize: { x: 0, y: 0, width: 1920, height: 1080 }, timestamp: '2026-06-24T10:00:00Z' },
  };
}

describe('CLI Integration', () => {
  let mockConfig: Config;

  beforeEach(() => {
    jest.clearAllMocks();
    mockConfig = {
      aiThreshold: 0.7,
      cvConfidenceMin: 0.6,
      enableAI: true,
      claudeApiKey: 'sk-ant-test',
      debug: false,
      fairyGui: {
        packageName: 'PSDImport',
        defaultScale9: { unit: 'ratio', left: 0.3, top: 0.3, right: 0.3, bottom: 0.3 },
        fontMappings: {},
        references: { local: {}, external: {} },
        pages: {},
      },
    };
    (ConfigLoader.load as jest.Mock).mockReturnValue(mockConfig);
    MockedPsdParser.mockImplementation(() => ({ parse: jest.fn().mockResolvedValue(makeMockLayerTree()) }) as any);
    MockedJsonGenerator.mockImplementation(() => ({ generate: jest.fn().mockReturnValue({ metadata: {}, layers: [], components: [] }), save: jest.fn().mockResolvedValue(undefined) }) as any);
    MockedComponentRecognizer.mockImplementation(() => ({ recognize: jest.fn().mockResolvedValue({ type: 'Unknown', confidence: 0, source: 'ai', needsReview: false }), recognizeTree: jest.fn().mockResolvedValue(new Map([['root', { type: 'Unknown', confidence: 0, source: 'ai', needsReview: false }]])), hasTag: jest.fn().mockReturnValue(false) }) as any);
  });

  describe('parse command flow', () => {
    test('should load configuration', () => {
      expect(ConfigLoader.load('/test/config.json')).toBeDefined();
      expect(ConfigLoader.load).toHaveBeenCalledWith('/test/config.json');
    });

    test('should parse PSD file', async () => {
      const parser = new PsdParser();
      expect((await parser.parse('/test.psd')).metadata.psdPath).toBe('/test.psd');
    });

    test('should recognize components from layer tree', async () => {
      const r = new ComponentRecognizer(null, { enableAI: false, aiThreshold: 0.7, cvConfidenceMin: 0.6 });
      expect((await r.recognizeTree(makeMockLayerTree().root)).size).toBeGreaterThanOrEqual(0);
    });

    test('should generate JSON', () => {
      const g = new JsonGenerator();
      expect(g.generate(makeMockLayerTree(), new Map())).toBeDefined();
    });

    test('should save JSON', async () => {
      const g = new JsonGenerator();
      await g.save(g.generate(makeMockLayerTree(), new Map()), '/output/result.json');
      expect(g.save).toHaveBeenCalled();
    });
  });

  describe('config integration', () => {
    test('should use config for recognizer options', () => {
      expect(ConfigLoader.load().aiThreshold).toBe(0.7);
    });
    test('should handle missing config', () => {
      (ConfigLoader.load as jest.Mock).mockReturnValueOnce({ ...mockConfig, enableAI: false });
      expect(ConfigLoader.load().enableAI).toBe(false);
    });
  });

  describe('AI identifier integration', () => {
    test('should create with API key', () => {
      expect(new AiIdentifier('sk-ant-test-key')).toBeDefined();
      expect(AiIdentifier).toHaveBeenCalledWith('sk-ant-test-key');
    });
    test('should work without AI', () => {
      expect(new ComponentRecognizer(null, { enableAI: false, aiThreshold: 0.7, cvConfidenceMin: 0.6 })).toBeDefined();
    });
  });
});
