import * as fs from 'fs';
import * as path from 'path';
import { Config } from '../config/config-loader';
import { FairyGuiExporter, FairyGuiExportOptions } from '../fairygui/fairygui-exporter';
import { FairyGuiExportResult } from '../fairygui/model';
import { HtmlPreviewGenerator } from '../generator/html-preview-generator';
import { JsonGenerator } from '../generator/json-generator';
import { PsdParser } from '../parser/psd-parser';
import { AiIdentifier } from '../recognizer/ai-identifier';
import { ComponentRecognizer } from '../recognizer/component-recognizer';
import { ComponentInfo } from '../recognizer/component-types';

export type ExportTarget = 'ugui' | 'fairygui' | 'all';

export interface FairyGuiRequestOptions {
  projectPath?: string;
  packageName?: string;
  componentName?: string;
  pageId?: string;
  sourceRoot?: string;
  adoptExisting?: boolean;
}

export interface ExportRequest {
  psdPath: string;
  target?: ExportTarget;
  outputPath?: string;
  assetsDir?: string;
  fairyGui?: FairyGuiRequestOptions;
}

export interface ExportResult {
  target: ExportTarget;
  jsonPath?: string;
  htmlPath?: string;
  fairyGui?: FairyGuiExportResult;
}

interface ParserDependency {
  parseDocument: PsdParser['parseDocument'];
  exportAssets: PsdParser['exportAssets'];
}

interface JsonGeneratorDependency {
  generate: JsonGenerator['generate'];
  save: JsonGenerator['save'];
}

interface HtmlGeneratorDependency {
  generate: HtmlPreviewGenerator['generate'];
  save: HtmlPreviewGenerator['save'];
}

interface FairyGuiExporterDependency {
  export: FairyGuiExporter['export'];
}

interface ExportServiceDependencies {
  parser?: ParserDependency;
  recognizerFactory?: (config: Config) => ComponentRecognizer;
  jsonGenerator?: JsonGeneratorDependency;
  htmlGenerator?: HtmlGeneratorDependency;
  fairyGuiExporter?: FairyGuiExporterDependency;
}

export class ExportService {
  private readonly parser: ParserDependency;
  private readonly recognizerFactory: (config: Config) => ComponentRecognizer;
  private readonly jsonGenerator: JsonGeneratorDependency;
  private readonly htmlGenerator: HtmlGeneratorDependency;
  private readonly fairyGuiExporter: FairyGuiExporterDependency;

  constructor(dependencies: ExportServiceDependencies = {}) {
    this.parser = dependencies.parser ?? new PsdParser();
    this.recognizerFactory = dependencies.recognizerFactory ?? this.createRecognizer;
    this.jsonGenerator = dependencies.jsonGenerator ?? new JsonGenerator();
    this.htmlGenerator = dependencies.htmlGenerator ?? new HtmlPreviewGenerator();
    this.fairyGuiExporter = dependencies.fairyGuiExporter ?? new FairyGuiExporter();
  }

  async export(request: ExportRequest, config: Config): Promise<ExportResult> {
    const target = request.target ?? 'ugui';
    if (!['ugui', 'fairygui', 'all'].includes(target)) {
      throw new Error(`Unsupported export target: ${target}`);
    }

    const document = await this.parser.parseDocument(request.psdPath);
    if (request.assetsDir && (target === 'ugui' || target === 'all')) {
      await this.parser.exportAssets(document, request.assetsDir);
    }

    const recognizer = this.recognizerFactory(config);
    const components: Map<string, ComponentInfo> = await recognizer.recognizeTree(
      document.tree.root,
      request.assetsDir,
    );
    const result: ExportResult = { target };

    if (target === 'ugui' || target === 'all') {
      const outputPath = this.resolveOutputPath(request.psdPath, request.outputPath ?? 'output.json');
      const htmlPath = path.join(
        path.dirname(outputPath),
        `${path.basename(outputPath, path.extname(outputPath))}.html`,
      );
      await this.jsonGenerator.save(this.jsonGenerator.generate(document.tree, components), outputPath);
      await this.htmlGenerator.save(
        this.htmlGenerator.generate(document.tree, components, htmlPath),
        htmlPath,
      );
      result.jsonPath = outputPath;
      result.htmlPath = htmlPath;
    }

    if (target === 'fairygui' || target === 'all') {
      result.fairyGui = await this.fairyGuiExporter.export(
        document,
        components,
        this.resolveFairyGuiOptions(request, config),
      );
    }

    return result;
  }

  private readonly createRecognizer = (config: Config): ComponentRecognizer => {
    const aiIdentifier = config.enableAI && config.claudeApiKey
      ? new AiIdentifier(config.claudeApiKey)
      : null;
    return new ComponentRecognizer(aiIdentifier, {
      enableAI: config.enableAI,
      aiThreshold: config.aiThreshold,
      cvConfidenceMin: config.cvConfidenceMin,
    });
  };

  private resolveFairyGuiOptions(request: ExportRequest, config: Config): FairyGuiExportOptions {
    const sourceRoot = request.fairyGui?.sourceRoot ?? config.fairyGui.sourceRoot;
    const sourceKey = sourceRoot
      ? path.relative(path.resolve(sourceRoot), path.resolve(request.psdPath)).replace(/\\/g, '/')
      : path.resolve(request.psdPath).replace(/\\/g, '/');
    const page = config.fairyGui.pages[sourceKey] ?? {};
    if (page.pageId) {
      const duplicate = Object.entries(config.fairyGui.pages).find(([configuredSource, configuredPage]) =>
        configuredSource !== sourceKey && configuredPage.pageId === page.pageId,
      );
      if (duplicate) {
        throw new Error(`FairyGUI pageId "${page.pageId}" is configured for both "${sourceKey}" and "${duplicate[0]}"`);
      }
    }
    const projectPath = request.fairyGui?.projectPath ?? config.fairyGui.projectPath;
    if (!projectPath) {
      throw new Error('FairyGUI export requires --fairygui-project or fairyGui.projectPath');
    }

    return {
      projectPath,
      packageName: request.fairyGui?.packageName ?? config.fairyGui.packageName,
      componentName: request.fairyGui?.componentName ?? page.componentName,
      pageId: request.fairyGui?.pageId ?? page.pageId,
      sourceRoot,
      adoptExisting: request.fairyGui?.adoptExisting,
      defaultScale9: config.fairyGui.defaultScale9,
      fontMappings: config.fairyGui.fontMappings,
      localReferences: config.fairyGui.references.local,
      externalReferences: config.fairyGui.references.external,
    };
  }

  private resolveOutputPath(psdPath: string, requestedPath: string): string {
    let outputPath = requestedPath;
    const isExistingDir = fs.existsSync(outputPath) && fs.statSync(outputPath).isDirectory();
    const looksLikeDir = !isExistingDir && !path.extname(outputPath);
    if (isExistingDir || looksLikeDir) {
      fs.mkdirSync(outputPath, { recursive: true });
      outputPath = path.join(
        outputPath,
        `${path.basename(psdPath, path.extname(psdPath))}.json`,
      );
    } else {
      fs.mkdirSync(path.dirname(outputPath), { recursive: true });
    }
    return outputPath;
  }
}
