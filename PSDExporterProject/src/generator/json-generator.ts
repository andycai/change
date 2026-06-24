import { writeFile } from 'fs/promises';
import { Layer, LayerTree } from '../parser/layer-tree';
import { ComponentInfo } from '../recognizer/component-types';
import {
  JsonConfig,
  JsonConfigSchema,
  LayerConfig,
  LayerComponentMapping,
} from './json-schema';

export class JsonGenerator {
  generate(
    layerTree: LayerTree,
    components: Map<string, ComponentInfo>,
  ): JsonConfig {
    const flatLayers = this.flattenLayers(layerTree.root);

    const componentMappings: LayerComponentMapping[] = [];
    for (const [layerId, component] of components) {
      componentMappings.push({ layerId, component });
    }

    return {
      metadata: {
        psdPath: layerTree.metadata.psdPath,
        canvasSize: layerTree.metadata.canvasSize,
        timestamp: layerTree.metadata.timestamp,
        generatedAt: new Date().toISOString(),
        generator: 'psd-exporter',
        version: '1.0.0',
      },
      layers: flatLayers,
      components: componentMappings,
    };
  }

  async save(config: JsonConfig, path: string): Promise<void> {
    const result = JsonConfigSchema.safeParse(config);
    if (!result.success) {
      throw new Error(
        `JSON config validation failed: ${result.error.message}`,
      );
    }
    const json = JSON.stringify(result.data, null, 2);
    await writeFile(path, json, 'utf-8');
  }

  private flattenLayers(layer: Layer): LayerConfig[] {
    const configs: LayerConfig[] = [];
    const config: LayerConfig = {
      id: layer.id,
      name: layer.name,
      type: layer.type,
      bounds: layer.bounds,
      visible: layer.visible,
      opacity: layer.opacity,
    };
    if (layer.assetPath) config.assetPath = layer.assetPath;
    if (layer.children && layer.children.length > 0) {
      config.children = [];
      for (const child of layer.children) {
        config.children.push(...this.flattenLayers(child));
      }
    }
    configs.push(config);
    return configs;
  }
}
