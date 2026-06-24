import { ComponentRecognizer, RecognizerOptions } from '../../src/recognizer/component-recognizer';
import { AiIdentifier } from '../../src/recognizer/ai-identifier';
import { ComponentType, ComponentInfo } from '../../src/recognizer/component-types';
import { Layer } from '../../src/parser/layer-tree';

jest.mock('../../src/recognizer/ai-identifier');

function makeLayer(overrides: Partial<Layer> = {}): Layer {
  return {
    id: 'layer_0', name: 'unnamed', type: 'image',
    bounds: { x: 0, y: 0, width: 100, height: 100 },
    visible: true, opacity: 1.0, ...overrides,
  };
}

function defaultOptions(overrides: Partial<RecognizerOptions> = {}): RecognizerOptions {
  return { enableAI: true, aiThreshold: 0.7, cvConfidenceMin: 0.6, ...overrides };
}

const MockedAiIdentifier = AiIdentifier as jest.MockedClass<typeof AiIdentifier>;

describe('ComponentRecognizer', () => {
  let recognizer: ComponentRecognizer;
  let mockAi: AiIdentifier;

  beforeEach(() => {
    jest.clearAllMocks();
    mockAi = new MockedAiIdentifier('test-key') as jest.Mocked<AiIdentifier>;
    recognizer = new ComponentRecognizer(mockAi, defaultOptions());
  });

  describe('tag recognition (100% confidence)', () => {
    test('should recognize btn tag as Button with 100% confidence', async () => {
      const layer = makeLayer({ name: 'btn_close' });
      const result = await recognizer.recognize(layer);
      expect(result).toEqual({ type: 'Button', confidence: 1.0, source: 'tag', needsReview: false });
    });

    test('should recognize txt tag as Text with 100% confidence', async () => {
      const layer = makeLayer({ name: 'txt_title' });
      const result = await recognizer.recognize(layer);
      expect(result.type).toBe('Text');
      expect(result.confidence).toBe(1.0);
      expect(result.source).toBe('tag');
      expect(result.needsReview).toBe(false);
    });

    test('should recognize img tag as Image with 100% confidence', async () => {
      const result = await recognizer.recognize(makeLayer({ name: 'img_bg' }));
      expect(result.type).toBe('Image');
      expect(result.confidence).toBe(1.0);
      expect(result.source).toBe('tag');
    });

    test('should recognize sv tag as ScrollView', async () => {
      expect((await recognizer.recognize(makeLayer({ name: 'sv_list' }))).type).toBe('ScrollView');
    });

    test('should recognize ipt tag as InputField', async () => {
      expect((await recognizer.recognize(makeLayer({ name: 'ipt_username' }))).type).toBe('InputField');
    });

    test('should recognize vbox tag as VerticalLayoutGroup', async () => {
      expect((await recognizer.recognize(makeLayer({ name: 'vbox_menu' }))).type).toBe('VerticalLayoutGroup');
    });

    test('should recognize hbox tag as HorizontalLayoutGroup', async () => {
      expect((await recognizer.recognize(makeLayer({ name: 'hbox_toolbar' }))).type).toBe('HorizontalLayoutGroup');
    });

    test('should recognize grid tag as GridLayoutGroup', async () => {
      expect((await recognizer.recognize(makeLayer({ name: 'grid_cards' }))).type).toBe('GridLayoutGroup');
    });

    test('should not call AI when tag is found', async () => {
      await recognizer.recognize(makeLayer({ name: 'btn_ok' }));
      expect(mockAi.identify).not.toHaveBeenCalled();
    });
  });

  describe('AI recognition', () => {
    test('should use AI when no tag is found', async () => {
      mockAi.identify = jest.fn().mockResolvedValue({ type: 'Button' as ComponentType, confidence: 0.85 });
      const result = await recognizer.recognize(makeLayer({ name: 'close_button' }), '/path/to/image.png');
      expect(mockAi.identify).toHaveBeenCalledWith(makeLayer({ name: 'close_button' }), '/path/to/image.png');
      expect(result.source).toBe('ai');
      expect(result.type).toBe('Button');
      expect(result.confidence).toBe(0.85);
    });

    test('should return AI result with high confidence', async () => {
      mockAi.identify = jest.fn().mockResolvedValue({ type: 'Image' as ComponentType, confidence: 0.95 });
      const result = await recognizer.recognize(makeLayer({ name: 'portrait' }), '/path/to/image.png');
      expect(result.type).toBe('Image');
      expect(result.confidence).toBe(0.95);
      expect(result.needsReview).toBe(false);
    });
  });

  describe('low confidence marks needsReview', () => {
    test('should mark needsReview when AI confidence is below cvConfidenceMin', async () => {
      mockAi.identify = jest.fn().mockResolvedValue({ type: 'Unknown' as ComponentType, confidence: 0.3 });
      const result = await recognizer.recognize(makeLayer({ name: 'mysterious' }), '/path/to/image.png');
      expect(result.needsReview).toBe(true);
      expect(result.confidence).toBe(0.3);
    });

    test('should mark needsReview below threshold (boundary)', async () => {
      const r = new ComponentRecognizer(mockAi, defaultOptions({ cvConfidenceMin: 0.5 }));
      mockAi.identify = jest.fn().mockResolvedValue({ type: 'Button' as ComponentType, confidence: 0.49 });
      expect((await r.recognize(makeLayer({ name: 'u' }), '/p/img.png')).needsReview).toBe(true);
    });

    test('should not mark needsReview at exact threshold', async () => {
      const r = new ComponentRecognizer(mockAi, defaultOptions({ cvConfidenceMin: 0.5 }));
      mockAi.identify = jest.fn().mockResolvedValue({ type: 'Button' as ComponentType, confidence: 0.5 });
      expect((await r.recognize(makeLayer({ name: 'b' }), '/p/img.png')).needsReview).toBe(false);
    });
  });

  describe('AI disabled fallback', () => {
    test('should return Unknown when AI disabled and no tag', async () => {
      const r = new ComponentRecognizer(mockAi, defaultOptions({ enableAI: false }));
      const result = await r.recognize(makeLayer({ name: 'unnamed' }));
      expect(result.type).toBe('Unknown');
      expect(result.confidence).toBe(0);
      expect(result.needsReview).toBe(false);
    });

    test('should not call AI when disabled', async () => {
      const r = new ComponentRecognizer(mockAi, defaultOptions({ enableAI: false }));
      await r.recognize(makeLayer({ name: 'x' }), '/p/img.png');
      expect(mockAi.identify).not.toHaveBeenCalled();
    });

    test('should return Unknown when AI is null and no tag', async () => {
      const r = new ComponentRecognizer(null, defaultOptions());
      const result = await r.recognize(makeLayer({ name: 'x' }));
      expect(result.type).toBe('Unknown');
      expect(result.confidence).toBe(0);
    });

    test('should return Unknown when no imagePath for AI', async () => {
      const result = await recognizer.recognize(makeLayer({ name: 'x' }));
      expect(result.type).toBe('Unknown');
      expect(mockAi.identify).not.toHaveBeenCalled();
    });
  });

  describe('recursive tree recognition', () => {
    test('should recognize all layers in a flat tree', async () => {
      const root: Layer = {
        id: 'root', name: 'root', type: 'group',
        bounds: { x: 0, y: 0, width: 1920, height: 1080 },
        visible: true, opacity: 1.0,
        children: [
          makeLayer({ id: 'root_0', name: 'btn_close' }),
          makeLayer({ id: 'root_1', name: 'txt_title' }),
        ],
      };
      const results = await recognizer.recognizeTree(root);
      expect(results.size).toBe(3);
      expect(results.get('root_0')!.type).toBe('Button');
      expect(results.get('root_1')!.type).toBe('Text');
    });

    test('should recognize nested layers recursively', async () => {
      const root: Layer = {
        id: 'root', name: 'root', type: 'group',
        bounds: { x: 0, y: 0, width: 1920, height: 1080 },
        visible: true, opacity: 1.0,
        children: [
          {
            ...makeLayer({ id: 'root_0', name: 'sv_list' }),
            children: [
              makeLayer({ id: 'root_0_0', name: 'btn_item1' }),
              makeLayer({ id: 'root_0_1', name: 'btn_item2' }),
            ],
          },
          makeLayer({ id: 'root_1', name: 'txt_footer' }),
        ],
      };
      const results = await recognizer.recognizeTree(root);
      expect(results.size).toBe(5);
      expect(results.get('root_0')!.type).toBe('ScrollView');
      expect(results.get('root_0_0')!.type).toBe('Button');
      expect(results.get('root_1')!.type).toBe('Text');
    });

    test('should handle leaf layers gracefully', async () => {
      const results = await recognizer.recognizeTree(makeLayer({ id: 'leaf', name: 'img_icon' }));
      expect(results.size).toBe(1);
      expect(results.get('leaf')!.type).toBe('Image');
    });
  });

  describe('hasTag', () => {
    test('should return true for tagged layers', () => {
      expect(recognizer.hasTag(makeLayer({ name: 'btn_ok' }))).toBe(true);
      expect(recognizer.hasTag(makeLayer({ name: 'txt_title' }))).toBe(true);
    });

    test('should return false for untagged layers', () => {
      expect(recognizer.hasTag(makeLayer({ name: 'close' }))).toBe(false);
      expect(recognizer.hasTag(makeLayer({ name: 'background' }))).toBe(false);
    });
  });
});
