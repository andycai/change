export type DiagnosticLevel = 'warning' | 'error';

export interface FairyDiagnostic {
  level: DiagnosticLevel;
  code: string;
  message: string;
  layerId?: string;
}

export interface FairyLayerMapping {
  layerId: string;
  sourceId?: number;
  name: string;
  target: string;
  role?: string;
  visible: boolean;
  diagnosticCodes: string[];
}

export interface FairyAiSuggestion {
  layerId: string;
  suggestedType: string;
  confidence: number;
  needsReview: boolean;
}

export interface ManagedResource {
  id: string;
  type: 'component' | 'image';
  name: string;
  packagePath: string;
  filePath: string;
  attributes?: Record<string, string>;
  contentHash?: string;
  references?: string[];
}

export interface ManagedPageManifest {
  schemaVersion: 1;
  pageId: string;
  packageName: string;
  packageId: string;
  componentName: string;
  rootComponentId: string;
  generation: string;
  sourcePath: string;
  sourceRoot?: string;
  resources: ManagedResource[];
  files: string[];
  generatedAt: string;
  generatorVersion: string;
}

export interface FairyGuiExportResult {
  packageId: string;
  rootComponentId: string;
  rootComponentPath: string;
  componentPaths: Record<string, string>;
  manifestPath: string;
  diagnostics: FairyDiagnostic[];
  reportStatus: 'written' | 'failed';
  reportError?: string;
}
