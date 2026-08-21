import { Layer } from '../parser/layer-tree';
import { ComponentInfo } from '../recognizer/component-types';
import { TagParseResult } from '../recognizer/tag-parse-result';
import {
  FairyAiSuggestion,
  FairyDiagnostic,
  FairyLayerMapping,
  ManagedResource,
} from './model';

export interface LayerIntent extends FairyLayerMapping {}

export interface FairyScene {
  pageId: string;
  packageId: string;
  rootComponentId: string;
  generation: string;
  resources: readonly ManagedResource[];
  files: ReadonlyMap<string, Buffer | string | Promise<Buffer>>;
  diagnostics: readonly FairyDiagnostic[];
  layerIntents: readonly LayerIntent[];
  aiSuggestions: readonly FairyAiSuggestion[];
}

export interface FairySceneInput {
  pageId: string;
  packageId: string;
  rootComponentId: string;
  generation: string;
  root: Layer;
  components: Map<string, ComponentInfo>;
  resources: readonly ManagedResource[];
  files: ReadonlyMap<string, Buffer | string | Promise<Buffer>>;
  diagnostics: readonly FairyDiagnostic[];
  parseTag: (name: string) => TagParseResult | null;
}

export function buildFairyScene(input: FairySceneInput): FairyScene {
  return {
    pageId: input.pageId,
    packageId: input.packageId,
    rootComponentId: input.rootComponentId,
    generation: input.generation,
    resources: input.resources,
    files: input.files,
    diagnostics: input.diagnostics,
    layerIntents: buildLayerIntents(input.root, input.components, input.diagnostics, input.parseTag),
    aiSuggestions: buildAiSuggestions(input.components),
  };
}

export function buildLayerIntents(
  root: Layer,
  components: Map<string, ComponentInfo>,
  diagnostics: readonly FairyDiagnostic[],
  parseTag: (name: string) => TagParseResult | null,
): LayerIntent[] {
  const diagnosticsByLayer = new Map<string, string[]>();
  for (const diagnostic of diagnostics) {
    if (!diagnostic.layerId) continue;
    diagnosticsByLayer.set(diagnostic.layerId, [
      ...(diagnosticsByLayer.get(diagnostic.layerId) ?? []),
      diagnostic.code,
    ]);
  }
  const intents: LayerIntent[] = [];
  const visit = (layer: Layer): void => {
    const component = components.get(layer.id);
    const parsed = parseTag(layer.name);
    const diagnosticCodes = diagnosticsByLayer.get(layer.id) ?? [];
    let target: string;
    if (!layer.visible && !component?.role) {
      target = 'skipped:hidden';
    } else if (parsed?.prefix) {
      target = diagnosticCodes.includes('REFERENCE_FALLBACK') ? 'reference:fallback' : `reference:${parsed.prefix}`;
    } else if (component?.source === 'tag' && component.type !== 'Unknown') {
      target = `component:${component.type}`;
    } else if (parsed?.directives.component) {
      target = 'component:custom';
    } else if (layer.type === 'text') {
      target = diagnosticCodes.includes('TEXT_RASTERIZED') ? 'image:rasterized-text' : 'text:native';
    } else if (layer.children?.length) {
      target = 'group:GGroup';
    } else if (component?.type === 'RawImage') {
      target = 'loader';
    } else if (component?.type === 'FillColor') {
      target = 'graph-or-image';
    } else {
      target = 'image';
    }
    intents.push({
      layerId: layer.id,
      sourceId: layer.sourceId,
      name: layer.name,
      target,
      role: component?.role,
      visible: layer.visible,
      diagnosticCodes,
    });
    for (const child of layer.children ?? []) visit(child);
  };
  visit(root);
  return intents;
}

export function buildAiSuggestions(components: Map<string, ComponentInfo>): FairyAiSuggestion[] {
  return [...components.entries()]
    .filter(([, component]) => component.source === 'ai' || component.needsReview)
    .map(([layerId, component]) => ({
      layerId,
      suggestedType: component.type,
      confidence: component.confidence,
      needsReview: component.needsReview,
    }))
    .sort((left, right) => left.layerId.localeCompare(right.layerId));
}
