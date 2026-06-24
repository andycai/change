import { TagParser } from './tag-parser';
import { AiIdentifier } from './ai-identifier';
import { ComponentType, ComponentInfo } from './component-types';
import { Layer } from '../parser/layer-tree';

export interface RecognizerOptions {
  enableAI: boolean;
  aiThreshold: number;
  cvConfidenceMin: number;
}

export class ComponentRecognizer {
  private tagParser: TagParser;
  private aiIdentifier: AiIdentifier | null;
  private options: RecognizerOptions;

  constructor(aiIdentifier: AiIdentifier | null, options: RecognizerOptions) {
    this.tagParser = new TagParser();
    this.aiIdentifier = aiIdentifier;
    this.options = options;
  }

  /**
   * Recognize a single layer's component type.
   * Priority: tag (100% confidence) > AI > Unknown
   */
  async recognize(layer: Layer, imagePath?: string): Promise<ComponentInfo> {
    // 1. Try tag recognition first (100% confidence)
    const tagType = this.tagParser.parse(layer.name);
    if (tagType) {
      return {
        type: tagType,
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
