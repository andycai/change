import { TagParser } from './tag-parser';
import { TagConfigLoader } from './tag-config-loader';
import { TagParseResult } from './tag-parse-result';
import { AiIdentifier } from './ai-identifier';
import { ComponentType, ComponentInfo } from './component-types';
import { Layer } from '../parser/layer-tree';

export interface RecognizerOptions {
  enableAI: boolean;
  aiThreshold: number;
  cvConfidenceMin: number;
}

/**
 * Maps a TagParseResult family tag to a ComponentType.
 * Falls back to 'Unknown' if no main tag is present.
 */
function mapMainTagToComponentType(tagId: string | undefined): ComponentType {
  if (!tagId) return 'Unknown';

  const mapping: Record<string, ComponentType> = {
    img: 'Image',
    rimg: 'Image',
    txt: 'Text',
    msk: 'Image',
    col: 'Image',
    bt: 'Button',
    dpd: 'Button',
    ipt: 'InputField',
    tg: 'Button',
    sld: 'Button',
    sv: 'ScrollView',
    vbox: 'VerticalLayoutGroup',
    hbox: 'HorizontalLayoutGroup',
    grid: 'GridLayoutGroup',
  };

  return mapping[tagId] || 'Unknown';
}

export class ComponentRecognizer {
  private tagParser: TagParser;
  private aiIdentifier: AiIdentifier | null;
  private options: RecognizerOptions;

  constructor(aiIdentifier: AiIdentifier | null, options: RecognizerOptions, tagParser?: TagParser) {
    // Use provided TagParser or load from default config
    if (tagParser) {
      this.tagParser = tagParser;
    } else {
      const path = require('path');
      const configPath = path.resolve(__dirname, '../../config/tag-config.json');
      const config = TagConfigLoader.load(configPath);
      this.tagParser = new TagParser(config);
    }
    this.aiIdentifier = aiIdentifier;
    this.options = options;
  }

  /**
   * Recognize a single layer's component type.
   * Priority: tag (100% confidence) > AI > Unknown
   */
  async recognize(layer: Layer, imagePath?: string): Promise<ComponentInfo> {
    // 1. Try tag recognition first (100% confidence)
    const tagResult = this.tagParser.parse(layer.name);
    if (tagResult) {
      const componentType = mapMainTagToComponentType(tagResult.families.main);
      return {
        type: componentType,
        confidence: 1.0,
        source: 'tag',
        needsReview: false,
      };
    }

    // 2. Try AI recognition if enabled
    if (this.options.enableAI && this.aiIdentifier && imagePath) {
      const result = await this.aiIdentifier.identify(layer, imagePath);

      // Mark for review if confidence is below minimum
      if (result.confidence < this.options.cvConfidenceMin) {
        return {
          type: result.type,
          confidence: result.confidence,
          source: 'ai',
          needsReview: true,
        };
      }

      return {
        type: result.type,
        confidence: result.confidence,
        source: 'ai',
        needsReview: false,
      };
    }

    // 3. AI disabled or not available — fallback to Unknown
    return {
      type: 'Unknown',
      confidence: 0,
      source: 'ai',
      needsReview: this.options.enableAI,
    };
  }

  /**
   * Recursively recognize all layers in a tree.
   * Returns a map of layer ID to ComponentInfo.
   */
  async recognizeTree(layer: Layer, imagePath?: string): Promise<Map<string, ComponentInfo>> {
    const results = new Map<string, ComponentInfo>();

    // Recognize this layer
    const info = await this.recognize(layer, imagePath);
    results.set(layer.id, info);

    // Recursively recognize children
    if (layer.children && layer.children.length > 0) {
      for (const child of layer.children) {
        const childResults = await this.recognizeTree(child, imagePath);
        for (const [id, childInfo] of childResults) {
          results.set(id, childInfo);
        }
      }
    }

    return results;
  }

  /**
   * Check if a layer has a tag (without running full recognition).
   */
  hasTag(layer: Layer): boolean {
    return this.tagParser.hasTag(layer.name);
  }
}
