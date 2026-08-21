import {
  access,
  open,
  mkdir,
  readFile,
  readdir,
  rename,
  rm,
  unlink,
  writeFile,
} from 'fs/promises';
import { constants } from 'fs';
import { createHash } from 'crypto';
import { basename, dirname, extname, isAbsolute, join, relative, resolve, sep } from 'path';
import sharp from 'sharp';
import { Layer, Rect, ShadowEffect, StrokeEffect, TextStyles } from '../parser/layer-tree';
import { ParsedPsdDocument, RasterSource } from '../parser/psd-document';
import { ComponentInfo } from '../recognizer/component-types';
import { TagConfigLoader } from '../recognizer/tag-config-loader';
import { TagParser } from '../recognizer/tag-parser';
import { createStableId, sanitizeFairyName, StableIdAllocator } from './identity';
import {
  FairyDiagnostic,
  FairyGuiExportResult,
  ManagedPageManifest,
  ManagedResource,
} from './model';
import {
  applyRasterMask,
  detectSolidColor,
  resolveScale9,
  Scale9Margins,
  trimRaster,
} from './raster-processor';
import { createReportFiles, writeReportFiles } from './report-writer';
import { XmlAttributes, writeComponentXml, writeXmlElement } from './component-xml-writer';
import { buildFairyScene } from './scene-builder';
import {
  createPackageXml,
  mergePackageXml,
  PackageResource,
  readPackageInfo,
  validatePackageXml,
  validateXmlDocument,
} from './xml-codec';

export interface FairyGuiExportOptions {
  projectPath: string;
  packageName?: string;
  componentName?: string;
  pageId?: string;
  sourceRoot?: string;
  adoptExisting?: boolean;
  defaultScale9?: Scale9Margins & { unit?: 'pixels' | 'ratio' };
  fontMappings?: Record<string, { default?: string; tmp?: string; ugui?: string }>;
  localReferences?: Record<string, { resourceId: string }>;
  externalReferences?: Record<string, { packageId: string; resourceId: string }>;
}

interface BuildContext {
  pageId: string;
  packageId: string;
  generation: string;
  componentDir: string;
  imageDir: string;
  resources: ManagedResource[];
  files: Map<string, Buffer | string | Promise<Buffer>>;
  componentPaths: Record<string, string>;
  diagnostics: FairyDiagnostic[];
  components: Map<string, ComponentInfo>;
  document: ParsedPsdDocument;
  options: FairyGuiExportOptions;
  localResourceIds: Map<string, string>;
  existingLocalResourceIds: Map<string, string>;
  layerIdentityKeys: ReadonlyMap<string, string>;
  complexSemanticsReported: Set<string>;
  ids: StableIdAllocator;
}

interface RenderedNode {
  xml: string;
  bounds: Rect;
}

interface RenderOptions {
  excludedLayerIds?: ReadonlySet<string>;
  nameOverrides?: ReadonlyMap<string, string>;
}

interface GridMetrics {
  columnGap: number;
  lineGap: number;
  children: Layer[];
}

export class FairyGuiExporter {
  private static readonly generatorVersion = '1.0.0';
  private static readonly generatorRevision = 3;
  private readonly tagParser: TagParser;

  constructor() {
    const configPath = resolve(__dirname, '../../config/tag-config.json');
    this.tagParser = new TagParser(TagConfigLoader.load(configPath));
  }

  async export(
    document: ParsedPsdDocument,
    components: Map<string, ComponentInfo>,
    options: FairyGuiExportOptions,
  ): Promise<FairyGuiExportResult> {
    const projectPath = resolve(options.projectPath);
    const packageName = sanitizeFairyName(options.packageName ?? 'PSDImport');
    const componentName = sanitizeFairyName(
      options.componentName ?? basename(document.tree.metadata.psdPath, extname(document.tree.metadata.psdPath)),
      'Page',
    );
    const pageId = sanitizeFairyName(options.pageId ?? createStableId('page', this.sourceKey(document, options)), 'page');

    await mkdir(projectPath, { recursive: true });
    const releaseLock = await this.acquireLock(projectPath);
    try {
      await this.recoverTransactions(projectPath);
      await this.ensureProject(projectPath, packageName, document);
      return await this.exportLocked(document, components, options, projectPath, packageName, componentName, pageId);
    } finally {
      await releaseLock();
    }
  }

  private async exportLocked(
    document: ParsedPsdDocument,
    components: Map<string, ComponentInfo>,
    options: FairyGuiExportOptions,
    projectPath: string,
    packageName: string,
    componentName: string,
    pageId: string,
  ): Promise<FairyGuiExportResult> {
    const packageDir = join(projectPath, 'assets', packageName);
    const packageXmlPath = join(packageDir, 'package.xml');
    const packageXml = await readFile(packageXmlPath, 'utf8');
    const packageValidationErrors = validatePackageXml(packageXml);
    if (packageValidationErrors.length > 0) {
      throw new Error(`FairyGUI package validation failed: ${packageValidationErrors.join('; ')}`);
    }
    const packageInfo = readPackageInfo(packageXml);
    const oldManifest = await this.readManifest(projectPath, pageId);
    if (oldManifest && !this.isSameSourcePath(
      oldManifest.sourcePath,
      document.tree.metadata.psdPath,
      options.sourceRoot,
      oldManifest.sourceRoot,
    )) {
      throw new Error(`FairyGUI pageId "${pageId}" already belongs to a different PSD: ${oldManifest.sourcePath}`);
    }
    if (oldManifest) {
      await this.validateManagedManifest(projectPath, packageName, packageInfo, oldManifest);
    }
    const oldManagedIds = new Set(oldManifest?.resources.map(resource => resource.id) ?? []);
    const rootName = `${componentName}.xml`;
    const conflict = packageInfo.resources.find(resource =>
      resource.tag === 'component'
      && resource.attributes.name === rootName
      && !oldManagedIds.has(resource.attributes.id),
    );
    if (conflict && !options.adoptExisting) {
      throw new Error(`FairyGUI component "${rootName}" already exists and is not managed`);
    }
    if (conflict) oldManagedIds.add(conflict.attributes.id);

    const packageId = packageInfo.packageId;
    const diagnostics: FairyDiagnostic[] = [];
    const ids = new StableIdAllocator(collision => diagnostics.push({
      level: 'warning',
      code: 'ID_COLLISION_RESOLVED',
      message: `资源身份哈希碰撞已使用确定性 salt ${collision.salt} 重算：${collision.namespace}`,
    }));
    for (const resource of packageInfo.resources) {
      const resourceId = resource.attributes.id;
      if (resourceId && !oldManagedIds.has(resourceId) && resourceId !== conflict?.attributes.id) {
        ids.reserve(resourceId, resource.attributes.name ?? resourceId);
      }
    }
    const layerIdentityKeys = this.createLayerIdentityKeys(document.tree.root, diagnostics);
    const rootComponentId = conflict?.attributes.id
      ?? oldManifest?.rootComponentId
      ?? ids.allocate('component', `${pageId}|root`);
    ids.reserve(rootComponentId, `${pageId}|root`);
    const localResourceIds = this.indexLocalResources(document.tree.root, components, pageId, layerIdentityKeys, ids);
    const existingLocalResourceIds = this.indexExistingLocalResources(packageInfo.resources, oldManagedIds);
    let generation = await this.createGeneration(
      document,
      components,
      pageId,
      packageId,
      componentName,
      rootComponentId,
      options,
      projectPath,
      packageName,
      packageInfo.resources,
      oldManagedIds,
      existingLocalResourceIds,
    );
    let generationBase = join('assets', packageName, 'psd', pageId, generation);
    const context: BuildContext = {
      pageId,
      packageId,
      generation,
      componentDir: join(generationBase, 'components'),
      imageDir: join(generationBase, 'images'),
      resources: [],
      files: new Map(),
      componentPaths: {},
      diagnostics,
      components,
      document,
      options,
      localResourceIds,
      existingLocalResourceIds,
      layerIdentityKeys,
      complexSemanticsReported: new Set(),
      ids,
    };
    if (!options.sourceRoot && !options.pageId) {
      context.diagnostics.push({
        level: 'warning',
        code: 'ABSOLUTE_SOURCE_IDENTITY',
        message: '未配置 sourceRoot 或 pageId，页面身份依赖绝对 PSD 路径',
      });
    }

    let rootRelativePath = join(generationBase, rootName);
    const displayNodes = this.renderChildren(document.tree.root.children ?? [], document.tree.root.bounds, context);
    const rootXml = this.componentXml(document.tree.metadata.canvasSize, displayNodes.map(node => node.xml));
    context.files.set(rootRelativePath, rootXml);
    context.resources.unshift({
      id: rootComponentId,
      type: 'component',
      name: rootName,
      packagePath: this.packagePathFor(generationBase),
      filePath: rootRelativePath,
    });
    for (const [layerId, component] of components) {
      if ((component.source === 'ai' && component.confidence > 0) || component.needsReview) {
        context.diagnostics.push({
          level: 'warning',
          code: 'AI_REVIEW_ONLY',
          message: `图层 ${layerId} 的识别结果仅写入报告，不改变 FairyGUI 结构`,
          layerId,
        });
      }
    }

    const finalizedGeneration = await this.finalizeGeneration(context, generation);
    if (finalizedGeneration !== generation) {
      const finalizedBase = join('assets', packageName, 'psd', pageId, finalizedGeneration);
      this.relocateGeneration(context, generationBase, finalizedBase, finalizedGeneration);
      generation = finalizedGeneration;
      generationBase = finalizedBase;
      rootRelativePath = join(generationBase, rootName);
    }

    const scene = buildFairyScene({
      pageId,
      packageId,
      rootComponentId,
      generation,
      root: document.tree.root,
      components,
      resources: context.resources,
      files: context.files,
      diagnostics: context.diagnostics,
      parseTag: name => this.tagParser.parse(name),
    });

    const packageResources: PackageResource[] = scene.resources.map(resource => ({
      tag: resource.type,
      attributes: {
        id: resource.id,
        name: resource.name,
        path: resource.packagePath,
        ...resource.attributes,
        ...(resource.id === rootComponentId ? { exported: 'true' } : {}),
      },
    }));
    const generatedById = new Map<string, ManagedResource>();
    for (const resource of scene.resources) {
      const previous = generatedById.get(resource.id);
      if (previous) {
        throw new Error(`FairyGUI generated resource id collision: ${resource.id} (${previous.filePath} vs ${resource.filePath})`);
      }
      generatedById.set(resource.id, resource);
    }
    const mergedPackageXml = mergePackageXml(packageXml, oldManagedIds, packageResources);
    const validationErrors = validatePackageXml(mergedPackageXml);
    if (validationErrors.length > 0) {
      throw new Error(`FairyGUI package validation failed: ${validationErrors.join('; ')}`);
    }
    const mergedPackageInfo = readPackageInfo(mergedPackageXml);
    await this.validateGeneratedFiles(projectPath, packageName, context, mergedPackageInfo.resources, packageResources);
    this.decorateManagedResources(context);
    await this.validateGenerationContents(projectPath, context);

    const generatedAt = new Date().toISOString();
    const manifest: ManagedPageManifest = {
      schemaVersion: 1,
      pageId,
      packageName,
      packageId,
      componentName,
      rootComponentId,
      generation,
      sourcePath: resolve(document.tree.metadata.psdPath),
      ...(options.sourceRoot ? { sourceRoot: resolve(options.sourceRoot) } : {}),
      resources: [...scene.resources],
      files: [...scene.files.keys()],
      generatedAt,
      generatorVersion: FairyGuiExporter.generatorVersion,
    };

    const cleanupFailures = await this.commit(projectPath, packageXmlPath, mergedPackageXml, manifest, oldManifest, new Map(scene.files));
    for (const file of cleanupFailures) {
      context.diagnostics.push({
        level: 'warning',
        code: 'OLD_FILE_CLEANUP_FAILED',
        message: `旧受管文件清理失败：${file}`,
      });
    }
    let reportStatus: FairyGuiExportResult['reportStatus'] = 'written';
    let reportError: string | undefined;
    try {
      const reportFiles = createReportFiles(
        manifest,
        context.diagnostics,
        [...scene.layerIntents],
        [...scene.aiSuggestions],
      );
      await writeReportFiles(projectPath, reportFiles);
    } catch (error) {
      reportStatus = 'failed';
      reportError = error instanceof Error ? error.message : String(error);
      context.diagnostics.push({
        level: 'warning',
        code: 'REPORT_WRITE_FAILED',
        message: `FairyGUI 工程已提交，但报告写入失败：${reportError}`,
      });
    }

    return {
      packageId,
      rootComponentId,
      rootComponentPath: rootRelativePath,
      componentPaths: context.componentPaths,
      manifestPath: join('.psd-exporter', 'pages', `${pageId}.json`),
      diagnostics: context.diagnostics,
      reportStatus,
      ...(reportError ? { reportError } : {}),
    };
  }

  private renderChildren(layers: Layer[], parentBounds: Rect, context: BuildContext, options: RenderOptions = {}): RenderedNode[] {
    return [...layers]
      .filter(layer => !options.excludedLayerIds?.has(layer.id))
      .filter(layer => this.shouldRender(layer, context.components.get(layer.id)))
      .map(layer => this.renderLayer(layer, parentBounds, context, options))
      .filter((node): node is RenderedNode => node !== null);
  }

  private renderLayer(layer: Layer, parentBounds: Rect, context: BuildContext, options: RenderOptions = {}): RenderedNode | null {
    if (options.excludedLayerIds?.has(layer.id)) return null;
    this.reportComplexSemantics(layer, context);
    const component = context.components.get(layer.id);
    const parsed = this.tagParser.parse(layer.name);
    const name = options.nameOverrides?.get(layer.id) ?? sanitizeFairyName(parsed?.baseName || layer.name, 'Layer');
    const bounds = this.effectiveBounds(layer);

    if (parsed?.prefix) {
      return this.renderReference(layer, bounds, parentBounds, name, parsed.prefix, context);
    }

    if (layer.children?.length && component?.source === 'tag') {
      if (component.type === 'Button' || component.type === 'Toggle') {
        return this.renderButtonComponent(layer, bounds, parentBounds, name, component.type === 'Toggle', context);
      }
      if (component.type === 'Slider') {
        return this.renderSliderComponent(layer, bounds, parentBounds, name, context);
      }
      if (component.type === 'InputField') {
        return this.renderInputComponent(layer, bounds, parentBounds, name, context);
      }
      if (component.type === 'Dropdown') {
        return this.renderDropdownComponent(layer, bounds, parentBounds, name, context);
      }
      if (component.type === 'ScrollView') {
        return this.renderScrollComponent(layer, bounds, parentBounds, name, context);
      }
      if (component.type === 'Mask') {
        return this.renderGenericExtensionComponent(layer, bounds, parentBounds, name, undefined, undefined, context, ' overflow="hidden"', options);
      }
    }

    if (layer.children?.length && component?.source === 'tag' && ['HorizontalLayoutGroup', 'VerticalLayoutGroup', 'GridLayoutGroup'].includes(component.type)) {
      return this.renderLayoutGroup(layer, bounds, parentBounds, name, component.type, context, options);
    }

    if (layer.children?.length && parsed?.directives.component) {
      return this.renderGenericComponent(layer, bounds, parentBounds, name, context, options);
    }

    if (layer.children?.length) {
      const childNodes = this.renderChildren(layer.children, parentBounds, context, options);
      const groupId = this.nodeId(context, layer, 'group');
      const groupXml = writeXmlElement('group', {
        id: groupId,
        name,
        xy: this.xy(bounds, parentBounds),
        size: this.size(bounds),
      });
      return { xml: `${childNodes.map(node => this.assignGroup(node.xml, groupId)).join('\n')}${childNodes.length ? '\n' : ''}${groupXml}`, bounds };
    }

    if (layer.type === 'text' && layer.text !== undefined) {
      return { xml: this.textNode(layer, bounds, parentBounds, name, context), bounds };
    }

    const raster = context.document.rasterSources.get(layer.id);
    if (!raster) {
      context.diagnostics.push({ level: 'warning', code: 'MISSING_RASTER', message: `图层「${layer.name}」没有可导出的像素`, layerId: layer.id });
      return null;
    }
    return { xml: this.imageNode(layer, raster, bounds, parentBounds, name, context), bounds };
  }

  private renderReference(
    layer: Layer,
    bounds: Rect,
    parentBounds: Rect,
    name: string,
    prefix: 'ref' | 'refp',
    context: BuildContext,
  ): RenderedNode | null {
    if (prefix === 'ref') {
      const localResourceId = context.localResourceIds.get(name);
      if (localResourceId) {
        return {
          xml: writeXmlElement('component', { id: this.nodeId(context, layer, 'page-reference'), name, src: localResourceId, xy: this.xy(bounds, parentBounds), size: this.size(bounds) }),
          bounds,
        };
      }
      const existingResourceId = context.existingLocalResourceIds.get(name);
      if (existingResourceId) {
        return {
          xml: writeXmlElement('component', { id: this.nodeId(context, layer, 'package-reference'), name, src: existingResourceId, xy: this.xy(bounds, parentBounds), size: this.size(bounds) }),
          bounds,
        };
      }
      const reference = context.options.localReferences?.[name];
      if (!reference) {
        return this.renderReferenceFallback(layer, bounds, parentBounds, name, prefix, context);
      }
      return {
        xml: writeXmlElement('component', { id: this.nodeId(context, layer, 'reference'), name, src: reference.resourceId, xy: this.xy(bounds, parentBounds), size: this.size(bounds) }),
        bounds,
      };
    }

    const reference = context.options.externalReferences?.[name];
    if (!reference) {
      return this.renderReferenceFallback(layer, bounds, parentBounds, name, prefix, context);
    }
    return {
      xml: writeXmlElement('component', { id: this.nodeId(context, layer, 'package-reference'), name, src: reference.resourceId, pkg: reference.packageId, xy: this.xy(bounds, parentBounds), size: this.size(bounds) }),
      bounds,
    };
  }

  private renderReferenceFallback(
    layer: Layer,
    bounds: Rect,
    parentBounds: Rect,
    name: string,
    prefix: 'ref' | 'refp',
    context: BuildContext,
  ): RenderedNode | null {
    const kind = prefix === 'ref' ? '本地引用' : '跨包引用';
    const raster = context.document.rasterSources.get(layer.id);
    if (layer.children?.length) {
      context.diagnostics.push({
        level: 'warning',
        code: 'REFERENCE_FALLBACK',
        message: `${kind}「${name}」未配置，已使用图层子树生成本地组件`,
        layerId: layer.id,
      });
      return this.renderGenericComponent(layer, bounds, parentBounds, name, context);
    }
    if (layer.type === 'text' && layer.text !== undefined) {
      context.diagnostics.push({
        level: 'warning',
        code: 'REFERENCE_FALLBACK',
        message: `${kind}「${name}」未配置，已保留本地文本`,
        layerId: layer.id,
      });
      return { xml: this.textNode(layer, bounds, parentBounds, name, context), bounds };
    }
    if (raster) {
      context.diagnostics.push({
        level: 'warning',
        code: 'REFERENCE_FALLBACK',
        message: `${kind}「${name}」未配置，已保留本地图像`,
        layerId: layer.id,
      });
      return { xml: this.imageNode(layer, raster, bounds, parentBounds, name, context), bounds };
    }
    context.diagnostics.push({
      level: 'error',
      code: 'UNRESOLVED_REFERENCE',
      message: `${kind}「${name}」未配置且没有可本地导出的视觉内容`,
      layerId: layer.id,
    });
    return null;
  }

  private renderButtonComponent(
    layer: Layer,
    bounds: Rect,
    parentBounds: Rect,
    name: string,
    toggle: boolean,
    context: BuildContext,
  ): RenderedNode {
    const resourceId = context.ids.allocate('component', `${context.pageId}|${this.layerKey(layer, context)}|${toggle ? 'toggle' : 'button'}`);
    const fileName = this.resourceFileName(name, `${context.pageId}|${this.layerKey(layer, context)}`, 'xml', context);
    const relativePath = join(context.componentDir, fileName);
    const pages = this.hasRole(layer, 'disable', context)
      ? '0,up,1,down,2,over,3,selectedOver,4,disabled,5,selectedDisabled'
      : '0,up,1,down,2,over,3,selectedOver';
    const hasStateRole = (layer.children ?? []).some(child => {
      const role = context.components.get(child.id)?.role;
      return role === 'bg' || role === 'press' || role === 'onover' || role === 'select' || role === 'disable';
    });
    const childXml: string[] = [];
    for (const child of layer.children ?? []) {
      if (!this.shouldRender(child, context.components.get(child.id))) continue;
      const role = context.components.get(child.id)?.role;
      const childName = role === 'bttxt' || role === 'tglb' ? 'title' : sanitizeFairyName(this.tagParser.parse(child.name)?.baseName || child.name);
      const rendered = child.children?.length
        ? this.renderGenericComponent(child, this.effectiveBounds(child), bounds, childName, context)
        : this.renderLayer(child, bounds, context, { nameOverrides: new Map([[child.id, childName]]) });
      if (!rendered) continue;
      const gearPages = this.buttonPages(role, toggle) ?? (!hasStateRole ? '0' : undefined);
      childXml.push(gearPages ? this.addChildXml(rendered.xml, `<gearDisplay controller="button" pages="${gearPages}"/>`) : rendered.xml);
    }
    const xml = writeComponentXml({
      width: bounds.width,
      height: bounds.height,
      attributes: { extention: 'Button' },
      controllers: [writeXmlElement('controller', { name: 'button', pages, selected: 0 })],
      displayList: childXml,
      footer: [writeXmlElement('Button', { mode: toggle ? 'Check' : undefined })],
    });
    context.files.set(relativePath, xml);
    context.resources.push({ id: resourceId, type: 'component', name: fileName, packagePath: this.packagePathFor(context.componentDir), filePath: relativePath });
    context.componentPaths[name] = relativePath;
    const nodeId = this.nodeId(context, layer, 'component');
    return {
      xml: writeXmlElement('component', { id: nodeId, name, src: resourceId, fileName, xy: this.xy(bounds, parentBounds), size: this.size(bounds) }),
      bounds,
    };
  }

  private renderGenericComponent(
    layer: Layer,
    bounds: Rect,
    parentBounds: Rect,
    name: string,
    context: BuildContext,
    options: RenderOptions = {},
  ): RenderedNode {
    this.reportComplexSemantics(layer, context);
    const resourceId = context.ids.allocate('component', `${context.pageId}|${this.layerKey(layer, context)}|component`);
    const fileName = this.resourceFileName(name, `${context.pageId}|${this.layerKey(layer, context)}`, 'xml', context);
    const relativePath = join(context.componentDir, fileName);
    const nodes = this.renderChildren(layer.children ?? [], bounds, context, options);
    context.files.set(relativePath, this.componentXml(bounds, nodes.map(node => node.xml)));
    context.resources.push({ id: resourceId, type: 'component', name: fileName, packagePath: this.packagePathFor(context.componentDir), filePath: relativePath });
    context.componentPaths[name] = relativePath;
    return {
      xml: writeXmlElement('component', { id: this.nodeId(context, layer, 'component'), name, src: resourceId, fileName, xy: this.xy(bounds, parentBounds), size: this.size(bounds) }),
      bounds,
    };
  }

  private renderSliderComponent(layer: Layer, bounds: Rect, parentBounds: Rect, name: string, context: BuildContext): RenderedNode {
    const fill = this.findByRole(layer, 'fill', context);
    const handle = this.findByRole(layer, 'handle', context);
    if (!fill || !handle) {
      context.diagnostics.push({ level: 'warning', code: 'SLIDER_DEGRADED', message: `Slider「${layer.name}」缺少 fill 或 handle，已导出为普通组件`, layerId: layer.id });
      return this.renderGenericComponent(layer, bounds, parentBounds, name, context);
    }
    const vertical = this.inferSliderDirection(layer, bounds, fill, handle, context);
    const resourceId = context.ids.allocate('component', `${context.pageId}|${this.layerKey(layer, context)}|slider`);
    const fileName = this.resourceFileName(name, `${context.pageId}|${this.layerKey(layer, context)}`, 'xml', context);
    const relativePath = join(context.componentDir, fileName);
    const children = (layer.children ?? []).map(child => {
      const role = context.components.get(child.id)?.role;
      const childName = role === 'fill' ? (vertical ? 'bar_v' : 'bar') : role === 'handle' ? 'grip' : sanitizeFairyName(this.tagParser.parse(child.name)?.baseName || child.name);
      return this.renderLayer(child, bounds, context, { nameOverrides: new Map([[child.id, childName]]) })?.xml;
    }).filter((value): value is string => Boolean(value));
    const xml = writeComponentXml({
      width: bounds.width,
      height: bounds.height,
      attributes: { extention: 'Slider' },
      displayList: children,
      footer: [writeXmlElement('Slider')],
    });
    context.files.set(relativePath, xml);
    context.resources.push({ id: resourceId, type: 'component', name: fileName, packagePath: this.packagePathFor(context.componentDir), filePath: relativePath });
    context.componentPaths[name] = relativePath;
    return { xml: writeXmlElement('component', { id: this.nodeId(context, layer, 'slider'), name, src: resourceId, xy: this.xy(bounds, parentBounds), size: this.size(bounds) }), bounds };
  }

  private renderInputComponent(layer: Layer, bounds: Rect, parentBounds: Rect, name: string, context: BuildContext): RenderedNode {
    const resourceId = context.ids.allocate('component', `${context.pageId}|${this.layerKey(layer, context)}|input`);
    const fileName = this.resourceFileName(name, `${context.pageId}|${this.layerKey(layer, context)}`, 'xml', context);
    const relativePath = join(context.componentDir, fileName);
    const placeholder = this.findByRole(layer, 'placeholder', context);
    const input = this.findByRole(layer, 'ipttxt', context);
    const children: string[] = [];
    for (const child of layer.children ?? []) {
      if (child === placeholder || child === input) continue;
      const childName = sanitizeFairyName(this.tagParser.parse(child.name)?.baseName || child.name);
      const rendered = this.renderLayer(child, bounds, context, { nameOverrides: new Map([[child.id, childName]]) });
      if (rendered) children.push(rendered.xml);
    }
    const inputBounds = input ? this.effectiveBounds(input) : bounds;
    const style = input?.textStyles;
    children.push(this.textXml({
      id: this.nodeId(context, input ?? layer, 'input'),
      name: 'input',
      bounds: inputBounds,
      parentBounds: bounds,
      text: input?.text ?? '',
      styles: style,
      attributes: { input: true, prompt: placeholder?.text ?? '' },
      context,
      layer: input ?? layer,
    }));
    context.files.set(relativePath, this.componentXml(bounds, children));
    context.resources.push({ id: resourceId, type: 'component', name: fileName, packagePath: this.packagePathFor(context.componentDir), filePath: relativePath });
    context.componentPaths[name] = relativePath;
    return { xml: writeXmlElement('component', { id: this.nodeId(context, layer, 'input-component'), name, src: resourceId, xy: this.xy(bounds, parentBounds), size: this.size(bounds) }), bounds };
  }

  private renderDropdownComponent(layer: Layer, bounds: Rect, parentBounds: Rect, name: string, context: BuildContext): RenderedNode {
    const template = this.findByRole(layer, 'template', context);
    const item = template
      ? this.findByRole(template, 'item', context)
      : this.findByRole(layer, 'item', context);
    if (!template || !item) {
      context.diagnostics.push({ level: 'warning', code: 'DROPDOWN_DEGRADED', message: `Dropdown「${layer.name}」缺少 template 或 item，已降级为普通组件`, layerId: layer.id });
      return this.renderGenericComponent(layer, bounds, parentBounds, name, context);
    }

    const itemBounds = this.effectiveBounds(item);
    this.renderGenericComponent(item, itemBounds, itemBounds, `${name}Item`, context);
    const itemResource = context.resources.find(resource => resource.filePath === context.componentPaths[`${name}Item`]);
    const popupId = context.ids.allocate('component', `${context.pageId}|${this.layerKey(layer, context)}|dropdown-popup`);
    const popupName = this.resourceFileName(`${name}Popup`, `${context.pageId}|${this.layerKey(layer, context)}|popup`, 'xml', context);
    const popupPath = join(context.componentDir, popupName);
    const structuralIds = new Set([item.id]);
    const templateChildren = this.renderChildren(
      template.children ?? [],
      this.effectiveBounds(template),
      context,
      { excludedLayerIds: structuralIds },
    );
    const popupBounds = this.effectiveBounds(template);
    const list = writeXmlElement('list', {
      id: this.nodeId(context, layer, 'dropdown-list'),
      name: 'list',
      xy: '0,0',
      size: this.size(popupBounds),
      overflow: 'scroll',
      scrollBarFlags: 4,
      margin: '0,0,0,0',
      defaultItem: itemResource ? `ui://${context.packageId}${itemResource.id}` : undefined,
    });
    context.files.set(popupPath, this.componentXml(popupBounds, [...templateChildren.map(node => node.xml), list]));
    context.resources.push({ id: popupId, type: 'component', name: popupName, packagePath: this.packagePathFor(context.componentDir), filePath: popupPath });

    const label = this.findByRole(layer, 'dpdlb', context);
    const arrow = this.findByRole(layer, 'dpdicon', context);
    const nameOverrides = new Map<string, string>();
    if (label) nameOverrides.set(label.id, 'title');
    if (arrow) nameOverrides.set(arrow.id, 'arrow');
    const children = this.renderChildren(layer.children ?? [], bounds, context, {
      excludedLayerIds: new Set([template.id, item.id]),
      nameOverrides,
    }).map(node => node.xml);
    const resourceId = context.ids.allocate('component', `${context.pageId}|${this.layerKey(layer, context)}|dropdown`);
    const fileName = this.resourceFileName(name, `${context.pageId}|${this.layerKey(layer, context)}`, 'xml', context);
    const relativePath = join(context.componentDir, fileName);
    const xml = writeComponentXml({
      width: bounds.width,
      height: bounds.height,
      attributes: { extention: 'ComboBox' },
      displayList: children,
      footer: [writeXmlElement('ComboBox', { dropdown: `ui://${context.packageId}${popupId}` })],
    });
    context.files.set(relativePath, xml);
    context.resources.push({ id: resourceId, type: 'component', name: fileName, packagePath: this.packagePathFor(context.componentDir), filePath: relativePath });
    context.componentPaths[name] = relativePath;
    return { xml: writeXmlElement('component', { id: this.nodeId(context, layer, 'dropdown'), name, src: resourceId, xy: this.xy(bounds, parentBounds), size: this.size(bounds) }), bounds };
  }

  private renderScrollComponent(layer: Layer, bounds: Rect, parentBounds: Rect, name: string, context: BuildContext): RenderedNode {
    const item = this.findByRole(layer, 'item', context);
    const viewport = this.findByRole(layer, 'vpt', context);
    const content = this.findByRole(layer, 'content', context);
    const scrollbarRoles = new Set(['hbarbg', 'hbar', 'vbarbg', 'vbar']);
    const contentCandidates = (layer.children ?? []).filter(child => !scrollbarRoles.has(context.components.get(child.id)?.role ?? ''));
    const fallbackContentBounds = this.childrenBounds(contentCandidates) ?? bounds;
    const viewportBounds = viewport
      ? this.effectiveBounds(viewport)
      : (bounds.width > 0 && bounds.height > 0 ? bounds : fallbackContentBounds);
    const contentBounds = content ? this.effectiveBounds(content) : fallbackContentBounds;
    const scroll = this.scrollDirection(viewportBounds, contentBounds);
    const structuralIds = new Set([
      viewport?.id,
      content?.id,
      item?.id,
      ...this.layersByRoles(layer, ['hbarbg', 'hbar', 'vbarbg', 'vbar'], context).map(candidate => candidate.id),
    ].filter((id): id is string => Boolean(id)));
    const scrollBarRoles = this.layersByRoles(layer, [...scrollbarRoles], context);
    const barConfigs = [
      { axis: 'horizontal' as const, backgroundRole: 'hbarbg', gripRole: 'hbar', attribute: 'hzScrollBar' },
      { axis: 'vertical' as const, backgroundRole: 'vbarbg', gripRole: 'vbar', attribute: 'vtScrollBar' },
    ];
    const nativeScrollBars = new Map<string, string>();
    const incompleteScrollBars: Layer[] = [];
    for (const config of barConfigs) {
      const background = this.findByRole(layer, config.backgroundRole, context);
      const grip = this.findByRole(layer, config.gripRole, context);
      if (background && grip) {
        nativeScrollBars.set(config.attribute, this.renderNativeScrollBar(layer, background, grip, config.axis, name, context));
      } else {
        if (background) incompleteScrollBars.push(background);
        if (grip) incompleteScrollBars.push(grip);
      }
    }
    if (incompleteScrollBars.length > 0) {
      context.diagnostics.push({
        level: 'warning',
        code: 'SCROLLBAR_DEGRADED',
        message: `ScrollView「${layer.name}」的滚动条图层已保留为视觉节点，运行时滚动条设为隐藏`,
        layerId: layer.id,
      });
    }
    if (!item) {
      const viewportContent = viewport?.children ?? [];
      const contentLayer = content ?? viewport ?? layer;
      const contentChildren = content
        ? (content.children?.length ? content.children : [content])
        : viewport
          ? viewportContent
          : layer.children ?? [];
      const visualNodes = this.renderChildren(contentChildren, viewportBounds, context, { excludedLayerIds: structuralIds });
      const decorationExcludedIds = new Set([
        viewport?.id,
        content?.id,
      ].filter((id): id is string => Boolean(id)));
      const scrollBarIds = new Set(scrollBarRoles.map(candidate => candidate.id));
      const decorationCandidates = viewport || content
        ? (layer.children ?? []).filter(child => child !== content && child !== viewport && !scrollBarIds.has(child.id))
        : incompleteScrollBars;
      const decorationNodes = this.renderChildren(
        decorationCandidates,
        bounds,
        context,
        { excludedLayerIds: decorationExcludedIds },
      );
      const paneId = context.ids.allocate('component', `${context.pageId}|${this.layerKey(layer, context)}|scroll-pane`);
      const paneName = this.resourceFileName(`${name}Pane`, `${context.pageId}|${this.layerKey(layer, context)}|pane`, 'xml', context);
      const panePath = join(context.componentDir, paneName);
      const fileName = this.resourceFileName(name, `${context.pageId}|${this.layerKey(layer, context)}`, 'xml', context);
      const relativePath = join(context.componentDir, fileName);
      const contentWrapperId = this.nodeId(context, contentLayer, 'scroll-content');
      const contentExtent = writeXmlElement('graph', { id: contentWrapperId, name: 'content', xy: this.xy(contentBounds, viewportBounds), size: this.size(contentBounds), type: 'rect', lineSize: 0, alpha: 0, touchable: false });
      const paneXml = writeComponentXml({
        width: viewportBounds.width,
        height: viewportBounds.height,
        attributes: {
          overflow: 'scroll',
          ...(scroll === 'vertical' ? {} : { scroll }),
          ...(incompleteScrollBars.length > 0 ? { scrollBar: 'hidden' } : {}),
          ...(nativeScrollBars.get('hzScrollBar') ? { hzScrollBar: `ui://${context.packageId}${nativeScrollBars.get('hzScrollBar')}` } : {}),
          ...(nativeScrollBars.get('vtScrollBar') ? { vtScrollBar: `ui://${context.packageId}${nativeScrollBars.get('vtScrollBar')}` } : {}),
        },
        displayList: [contentExtent, ...visualNodes.map(node => node.xml)],
      });
      context.files.set(panePath, paneXml);
      context.resources.push({ id: paneId, type: 'component', name: paneName, packagePath: this.packagePathFor(context.componentDir), filePath: panePath });
      const paneNode = writeXmlElement('component', { id: this.nodeId(context, layer, 'scroll-pane'), name: 'pane', src: paneId, xy: this.xy(viewportBounds, bounds), size: this.size(viewportBounds) });
      const xml = this.componentXml(bounds, [paneNode, ...decorationNodes.map(node => node.xml)]);
      const resourceId = context.ids.allocate('component', `${context.pageId}|${this.layerKey(layer, context)}|scroll`);
      context.files.set(relativePath, xml);
      context.resources.push({ id: resourceId, type: 'component', name: fileName, packagePath: this.packagePathFor(context.componentDir), filePath: relativePath });
      context.componentPaths[name] = relativePath;
      return { xml: writeXmlElement('component', { id: this.nodeId(context, layer, 'scroll'), name, src: resourceId, xy: this.xy(bounds, parentBounds), size: this.size(bounds) }), bounds };
    }
    const itemBounds = this.effectiveBounds(item);
    const itemComponent = this.renderGenericComponent(item, itemBounds, itemBounds, `${name}Item`, context);
    const itemResource = context.resources.find(resource => resource.filePath === context.componentPaths[`${name}Item`]);
    const resourceId = context.ids.allocate('component', `${context.pageId}|${this.layerKey(layer, context)}|list`);
    const fileName = this.resourceFileName(name, `${context.pageId}|${this.layerKey(layer, context)}`, 'xml', context);
    const relativePath = join(context.componentDir, fileName);
    const scrollBarNodes = incompleteScrollBars
      .map(candidate => this.renderLayer(candidate, viewportBounds, context))
      .filter((node): node is RenderedNode => node !== null);
    const listXml = writeXmlElement('list', {
      id: this.nodeId(context, layer, 'list'),
      name: 'list',
      xy: '0,0',
      size: this.size(viewportBounds),
      overflow: 'scroll',
      ...(scroll === 'vertical' ? {} : { scroll }),
      ...(incompleteScrollBars.length > 0 ? { scrollBar: 'hidden' } : {}),
      ...(nativeScrollBars.get('hzScrollBar') ? { hzScrollBar: `ui://${context.packageId}${nativeScrollBars.get('hzScrollBar')}` } : {}),
      ...(nativeScrollBars.get('vtScrollBar') ? { vtScrollBar: `ui://${context.packageId}${nativeScrollBars.get('vtScrollBar')}` } : {}),
      defaultItem: itemResource ? `ui://${context.packageId}${itemResource.id}` : undefined,
    });
    context.files.set(relativePath, this.componentXml(viewportBounds, [listXml, ...scrollBarNodes.map(node => node.xml)]));
    context.resources.push({ id: resourceId, type: 'component', name: fileName, packagePath: this.packagePathFor(context.componentDir), filePath: relativePath });
    context.componentPaths[name] = relativePath;
    void itemComponent;
    return { xml: writeXmlElement('component', { id: this.nodeId(context, layer, 'list-component'), name, src: resourceId, xy: this.xy(viewportBounds, parentBounds), size: this.size(viewportBounds) }), bounds: viewportBounds };
  }

  private renderNativeScrollBar(
    scrollLayer: Layer,
    background: Layer,
    grip: Layer,
    axis: 'horizontal' | 'vertical',
    name: string,
    context: BuildContext,
  ): string {
    const backgroundBounds = this.effectiveBounds(background);
    const gripBounds = this.effectiveBounds(grip);
    const resourceId = context.ids.allocate('component', `${context.pageId}|${this.layerKey(scrollLayer, context)}|${axis}-scrollbar`);
    const suffix = axis === 'vertical' ? 'Vertical' : 'Horizontal';
    const fileName = this.resourceFileName(`${name}${suffix}ScrollBar`, `${context.pageId}|${this.layerKey(scrollLayer, context)}|${axis}-scrollbar`, 'xml', context);
    const relativePath = join(context.componentDir, fileName);
    const backgroundRaster = context.document.rasterSources.get(background.id);
    const gripRaster = context.document.rasterSources.get(grip.id);
    this.reportScrollbarReferenceFallback(background, context);
    this.reportScrollbarReferenceFallback(grip, context);
    const backgroundNode = backgroundRaster
      ? this.imageNode(background, backgroundRaster, backgroundBounds, backgroundBounds, 'background', context)
      : writeXmlElement('graph', { id: this.nodeId(context, background, 'scrollbar-background'), name: 'background', xy: '0,0', size: this.size(backgroundBounds), type: 'rect', lineSize: 0, fillColor: '#00000000' });
    const barNode = writeXmlElement('graph', { id: this.nodeId(context, background, 'scrollbar-bar'), name: 'bar', xy: '0,0', size: this.size(backgroundBounds), type: 'rect', lineSize: 0, fillColor: '#00000000' });
    const gripNode = gripRaster
      ? this.imageNode(grip, gripRaster, gripBounds, backgroundBounds, 'grip', context)
      : writeXmlElement('graph', { id: this.nodeId(context, grip, 'scrollbar-grip'), name: 'grip', xy: this.xy(gripBounds, backgroundBounds), size: this.size(gripBounds), type: 'rect', lineSize: 0, fillColor: '#00000000' });
    const xml = writeComponentXml({
      width: backgroundBounds.width,
      height: backgroundBounds.height,
      attributes: { extention: 'ScrollBar' },
      displayList: [backgroundNode, barNode, gripNode],
    });
    context.files.set(relativePath, xml);
    context.resources.push({ id: resourceId, type: 'component', name: fileName, packagePath: this.packagePathFor(context.componentDir), filePath: relativePath });
    return resourceId;
  }

  private reportScrollbarReferenceFallback(layer: Layer, context: BuildContext): void {
    const parsed = this.tagParser.parse(layer.name);
    if (!parsed?.prefix) return;
    const name = sanitizeFairyName(parsed.baseName, 'Resource');
    const configured = parsed.prefix === 'ref'
      ? context.localResourceIds.has(name)
        || context.existingLocalResourceIds.has(name)
        || Boolean(context.options.localReferences?.[name])
      : Boolean(context.options.externalReferences?.[name]);
    if (configured) return;
    context.diagnostics.push({
      level: 'warning',
      code: 'REFERENCE_FALLBACK',
      message: `${parsed.prefix === 'ref' ? '本地引用' : '跨包引用'}「${name}」未配置，滚动条已使用本地图像`,
      layerId: layer.id,
    });
  }

  private renderGenericExtensionComponent(
    layer: Layer,
    bounds: Rect,
    parentBounds: Rect,
    name: string,
    extension: string | undefined,
    footer: string | undefined,
    context: BuildContext,
    rootAttributes = '',
    options: RenderOptions = {},
  ): RenderedNode {
    this.reportComplexSemantics(layer, context);
    const resourceId = context.ids.allocate('component', `${context.pageId}|${this.layerKey(layer, context)}|${extension ?? 'scroll'}`);
    const fileName = this.resourceFileName(name, `${context.pageId}|${this.layerKey(layer, context)}`, 'xml', context);
    const relativePath = join(context.componentDir, fileName);
    const children = this.renderChildren(layer.children ?? [], bounds, context, options);
    const parsedAttributes = this.parseAttributeFragment(rootAttributes);
    const xml = writeComponentXml({
      width: bounds.width,
      height: bounds.height,
      attributes: { ...parsedAttributes, extention: extension },
      displayList: children.map(node => node.xml),
      footer: footer ? [footer] : undefined,
    });
    context.files.set(relativePath, xml);
    context.resources.push({ id: resourceId, type: 'component', name: fileName, packagePath: this.packagePathFor(context.componentDir), filePath: relativePath });
    context.componentPaths[name] = relativePath;
    return { xml: writeXmlElement('component', { id: this.nodeId(context, layer, extension ?? 'scroll'), name, src: resourceId, xy: this.xy(bounds, parentBounds), size: this.size(bounds) }), bounds };
  }

  private renderLayoutGroup(
    layer: Layer,
    bounds: Rect,
    parentBounds: Rect,
    name: string,
    type: ComponentInfo['type'],
    context: BuildContext,
    options: RenderOptions = {},
  ): RenderedNode {
    if (type === 'GridLayoutGroup') {
      const grid = this.inferGridMetrics(layer, context);
      if (grid) {
        const item = grid.children[0];
        const itemBounds = this.effectiveBounds(item);
        const itemName = `${name}Item`;
        this.renderGenericComponent(item, itemBounds, itemBounds, itemName, context, options);
        const itemResource = context.resources.find(resource => resource.filePath === context.componentPaths[itemName]);
        if (itemResource) {
          const items = grid.children.map(() => writeXmlElement('item'));
          const listXml = writeXmlElement('list', { id: this.nodeId(context, layer, 'grid-list'), name, xy: this.xy(bounds, parentBounds), size: this.size(bounds), layout: 'flow_hz', lineGap: grid.lineGap, colGap: grid.columnGap, defaultItem: `ui://${context.packageId}${itemResource.id}` }, items);
          return { xml: listXml, bounds };
        }
      }
      context.diagnostics.push({ level: 'warning', code: 'GRID_DEGRADED', message: `Grid「${layer.name}」子项不是稳定同构网格，已保留绝对坐标`, layerId: layer.id });
    }
    const children = this.renderChildren(layer.children ?? [], parentBounds, context, options);
    const groupId = this.nodeId(context, layer, 'layout-group');
    const layout = type === 'HorizontalLayoutGroup' ? 'horizontal' : type === 'VerticalLayoutGroup' ? 'vertical' : undefined;
    const gap = layout ? this.inferLayoutGap(layer, layout) : undefined;
    if (layout && gap === undefined) {
      context.diagnostics.push({ level: 'warning', code: 'LAYOUT_DEGRADED', message: `布局组「${layer.name}」子项间距不一致，保留绝对坐标`, layerId: layer.id });
    }
    const attrs = layout && gap !== undefined ? ` layout="${layout}"${layout === 'horizontal' ? ` columnGap="${gap}"` : ` lineGap="${gap}"`}` : '';
    const groupXml = writeXmlElement('group', { id: groupId, name, xy: this.xy(bounds, parentBounds), size: this.size(bounds), ...this.parseAttributeFragment(attrs) });
    return { xml: `${children.map(node => this.assignGroup(node.xml, groupId)).join('\n')}${children.length ? '\n' : ''}${groupXml}`, bounds };
  }

  private inferLayoutGap(layer: Layer, layout: 'horizontal' | 'vertical'): number | undefined {
    const children = (layer.children ?? []).filter(child => child.visible).sort((left, right) => layout === 'horizontal' ? left.bounds.x - right.bounds.x : left.bounds.y - right.bounds.y);
    if (children.length < 2) return 0;
    const gaps = children.slice(1).map((child, index) => {
      const previous = children[index];
      return layout === 'horizontal'
        ? Math.round(child.bounds.x - (previous.bounds.x + previous.bounds.width))
        : Math.round(child.bounds.y - (previous.bounds.y + previous.bounds.height));
    });
    return gaps.every(gap => gap === gaps[0]) ? gaps[0] : undefined;
  }

  private inferGridMetrics(layer: Layer, context: BuildContext): GridMetrics | undefined {
    const children = (layer.children ?? [])
      .filter(child => this.shouldRender(child, context.components.get(child.id)))
      .sort((left, right) => left.bounds.y - right.bounds.y || left.bounds.x - right.bounds.x);
    if (children.length < 4) return undefined;
    const firstBounds = this.effectiveBounds(children[0]);
    if (children.some(child => {
      const bounds = this.effectiveBounds(child);
      return Math.round(bounds.width) !== Math.round(firstBounds.width)
        || Math.round(bounds.height) !== Math.round(firstBounds.height);
    })) return undefined;

    const rows = new Map<number, Layer[]>();
    for (const child of children) {
      const row = Math.round(this.effectiveBounds(child).y);
      rows.set(row, [...(rows.get(row) ?? []), child]);
    }
    const orderedRows = [...rows.entries()]
      .sort(([left], [right]) => left - right)
      .map(([, row]) => row.sort((left, right) => left.bounds.x - right.bounds.x));
    const columnCount = orderedRows[0]?.length ?? 0;
    if (orderedRows.length < 2 || columnCount < 2 || orderedRows.some(row => row.length !== columnCount)) return undefined;

    const firstColumns = orderedRows[0].map(child => Math.round(this.effectiveBounds(child).x));
    if (orderedRows.some(row => row.some((child, index) => Math.round(this.effectiveBounds(child).x) !== firstColumns[index]))) {
      return undefined;
    }
    const columnGaps = firstColumns.slice(1).map((x, index) => x - firstColumns[index] - Math.round(firstBounds.width));
    const rowStarts = orderedRows.map(row => Math.round(this.effectiveBounds(row[0]).y));
    const lineGaps = rowStarts.slice(1).map((y, index) => y - rowStarts[index] - Math.round(firstBounds.height));
    if (!columnGaps.every(gap => gap === columnGaps[0]) || !lineGaps.every(gap => gap === lineGaps[0])) return undefined;

    const signature = this.gridItemSignature(children[0], context);
    if (children.some(child => this.gridItemSignature(child, context) !== signature)) return undefined;
    return { columnGap: columnGaps[0], lineGap: lineGaps[0], children };
  }

  private inferSliderDirection(
    layer: Layer,
    bounds: Rect,
    fill: Layer,
    handle: Layer,
    context: BuildContext,
  ): boolean {
    const fillBounds = this.effectiveBounds(fill);
    const handleBounds = this.effectiveBounds(handle);
    const fillHorizontal = fillBounds.width > fillBounds.height * 1.2;
    const fillVertical = fillBounds.height > fillBounds.width * 1.2;
    if (fillVertical) return true;
    if (fillHorizontal) return false;
    if (bounds.height > bounds.width * 1.2) return true;
    if (bounds.width > bounds.height * 1.2) return false;
    const handleCenterX = handleBounds.x + handleBounds.width / 2;
    const handleCenterY = handleBounds.y + handleBounds.height / 2;
    const containerCenterX = bounds.x + bounds.width / 2;
    const containerCenterY = bounds.y + bounds.height / 2;
    const horizontalOffset = Math.abs(handleCenterX - containerCenterX) / Math.max(1, bounds.width);
    const verticalOffset = Math.abs(handleCenterY - containerCenterY) / Math.max(1, bounds.height);
    if (verticalOffset > horizontalOffset * 1.5) return true;
    if (horizontalOffset > verticalOffset * 1.5) return false;
    context.diagnostics.push({
      level: 'warning',
      code: 'SLIDER_DIRECTION_DEFAULTED',
      message: `Slider「${layer.name}」方向置信不足，默认使用水平布局`,
      layerId: layer.id,
    });
    return false;
  }

  private reportComplexSemantics(layer: Layer, context: BuildContext): void {
    const blendMode = layer.blendMode?.toLowerCase();
    const complexBlend = Boolean(blendMode && blendMode !== 'normal' && blendMode !== 'pass through');
    if (layer.maskType !== 'vector' && !layer.clipping && !complexBlend) return;
    if (context.complexSemanticsReported.has(layer.id)) return;
    context.complexSemanticsReported.add(layer.id);
    context.diagnostics.push({
      level: 'warning',
      code: 'MASK_DEGRADED',
      message: `图层「${layer.name}」包含 FairyGUI 无法直接表达的 vector mask、混合模式或剪贴链，已保留解析器提供的像素或子树结果并忽略不可编辑的合成语义`,
      layerId: layer.id,
    });
  }

  private gridItemSignature(layer: Layer, context: BuildContext): string {
    const bounds = this.effectiveBounds(layer);
    const raster = context.document.rasterSources.get(layer.id);
    return JSON.stringify({
      type: layer.type,
      name: this.tagParser.parse(layer.name)?.families ?? {},
      size: { width: Math.round(bounds.width), height: Math.round(bounds.height) },
      text: layer.text,
      textStyles: layer.textStyles,
      raster: raster ? this.hashBytes(raster.rgba) : undefined,
      children: (layer.children ?? []).map(child => {
        const childBounds = this.effectiveBounds(child);
        return {
          offset: { x: Math.round(childBounds.x - bounds.x), y: Math.round(childBounds.y - bounds.y) },
          signature: this.gridItemSignature(child, context),
        };
      }),
    });
  }

  private scrollDirection(viewport: Rect, content: Rect): 'horizontal' | 'vertical' | 'both' {
    const overflowsHorizontally = content.width > viewport.width;
    const overflowsVertically = content.height > viewport.height;
    if (overflowsHorizontally && overflowsVertically) return 'both';
    return overflowsHorizontally ? 'horizontal' : 'vertical';
  }

  private childrenBounds(children: Layer[]): Rect | undefined {
    const visible = children.filter(child => child.visible).map(child => this.effectiveBounds(child));
    if (visible.length === 0) return undefined;
    const minX = Math.min(...visible.map(bounds => bounds.x));
    const minY = Math.min(...visible.map(bounds => bounds.y));
    const maxX = Math.max(...visible.map(bounds => bounds.x + bounds.width));
    const maxY = Math.max(...visible.map(bounds => bounds.y + bounds.height));
    return { x: minX, y: minY, width: maxX - minX, height: maxY - minY };
  }

  private imageNode(layer: Layer, source: RasterSource, bounds: Rect, parentBounds: Rect, name: string, context: BuildContext): string {
    this.reportComplexSemantics(layer, context);
    const parsed = this.tagParser.parse(layer.name);
    const maskedSource = applyRasterMask(source, bounds);
    const componentType = context.components.get(layer.id)?.type;
    const solidColor = componentType === 'FillColor' ? detectSolidColor(maskedSource) : undefined;
    if (solidColor) {
      return writeXmlElement('graph', {
        id: this.nodeId(context, layer, 'graph'),
        name,
        xy: this.xy(bounds, parentBounds),
        size: this.size(bounds),
        type: 'rect',
        lineSize: 0,
        fillColor: this.color({ r: solidColor.r / 255, g: solidColor.g / 255, b: solidColor.b / 255, a: solidColor.a / 255 }),
      });
    }
    if (componentType === 'FillColor') {
      context.diagnostics.push({ level: 'warning', code: 'FILLCOLOR_DEGRADED', message: `纯色图层「${layer.name}」包含多个颜色，已降级为图片`, layerId: layer.id });
    }
    const trimmed = trimRaster(maskedSource);
    const explicit = parsed?.directives.scale9;
    const sliced = context.components.get(layer.id)?.imageType === 'sliced' || parsed?.families.imageType === 'sliced';
    const margins = explicit ?? (sliced ? this.defaultScale9(source, context.options.defaultScale9) : undefined);
    const scale9MarginsValid = !margins
      || (margins.left + margins.right < source.width && margins.top + margins.bottom < source.height);
    if (!scale9MarginsValid) {
      context.diagnostics.push({ level: 'warning', code: 'INVALID_SCALE9', message: `九宫格「${layer.name}」边距没有留下有效中心区域，已降级为普通图片`, layerId: layer.id });
    }
    if (sliced && !explicit) {
      context.diagnostics.push({ level: 'warning', code: 'SCALE9_DEFAULTED', message: `九宫格「${layer.name}」使用配置的默认边距`, layerId: layer.id });
    }
    const scale9 = margins && scale9MarginsValid ? resolveScale9(margins, trimmed) : undefined;
    const useTrimmed = !trimmed.empty && (!scale9 || scale9.trimAllowed);
    const image = useTrimmed ? trimmed : {
      rgba: source.rgba,
      offsetX: 0,
      offsetY: 0,
      width: source.width,
      height: source.height,
      originalWidth: source.width,
      originalHeight: source.height,
      empty: false,
    };
    if (trimmed.empty) {
      context.diagnostics.push({ level: 'warning', code: 'EMPTY_RASTER', message: `图片「${layer.name}」完全透明`, layerId: layer.id });
    }
    if (scale9 && !scale9.trimAllowed) {
      context.diagnostics.push({ level: 'warning', code: 'SCALE9_TRIM_DISABLED', message: `九宫格「${layer.name}」因裁剪后中心区非法而保留原纹理`, layerId: layer.id });
    }

    const resourceId = context.ids.allocate('image', `${context.pageId}|${this.layerKey(layer, context)}|image`);
    const fileName = this.resourceFileName(name, `${context.pageId}|${this.layerKey(layer, context)}|image`, 'png', context);
    const filePath = join(context.imageDir, fileName);
    const buffer = Buffer.from(image.rgba.buffer, image.rgba.byteOffset, image.rgba.byteLength);
    context.files.set(filePath, sharp(buffer, { raw: { width: Math.max(1, image.width), height: Math.max(1, image.height), channels: 4 } }).png().toBuffer());
    const packagePath = this.packagePathFor(context.imageDir);
    const imageType = context.components.get(layer.id)?.imageType ?? parsed?.families.imageType;
    const resourceAttributes: Record<string, string> | undefined = scale9
      ? { scale: '9grid', scale9grid: `${scale9.grid.x},${scale9.grid.y},${scale9.grid.width},${scale9.grid.height}` }
      : imageType === 'tiled'
        ? { scale: 'tile' }
        : undefined;
    context.resources.push({ id: resourceId, type: 'image', name: fileName, packagePath, filePath, attributes: resourceAttributes });
    const imageBounds = { ...bounds, x: bounds.x + image.offsetX, y: bounds.y + image.offsetY, width: image.width, height: image.height };
    const scaleAttribute = scale9
      ? ` scale9grid="${scale9.grid.x},${scale9.grid.y},${scale9.grid.width},${scale9.grid.height}"`
      : '';
    if (imageType === 'filled') {
      context.diagnostics.push({ level: 'warning', code: 'FILL_DIRECTION_DEFAULTED', message: `Filled 图层「${layer.name}」未声明方向，默认水平填充`, layerId: layer.id });
      return writeXmlElement('image', { id: this.nodeId(context, layer, 'image'), name, src: resourceId, fileName, xy: this.xy(imageBounds, parentBounds), size: this.size(imageBounds), fillMethod: 'horizontal' });
    }
    if (componentType === 'RawImage' || parsed?.families.main === 'rimg') {
      return writeXmlElement('loader', { id: this.nodeId(context, layer, 'loader'), name, xy: this.xy(imageBounds, parentBounds), size: this.size(imageBounds), url: `ui://${context.packageId}${resourceId}`, fill: 'scaleFree' });
    }
    return writeXmlElement('image', { id: this.nodeId(context, layer, 'image'), name, src: resourceId, fileName, xy: this.xy(imageBounds, parentBounds), size: this.size(imageBounds), ...this.parseAttributeFragment(scaleAttribute) });
  }

  private textNode(layer: Layer, bounds: Rect, parentBounds: Rect, name: string, context: BuildContext): string {
    this.reportComplexSemantics(layer, context);
    const unsupported = (layer.textStyles?.effects ?? []).some(effect =>
      effect.enabled && ['innerShadow', 'gradient', 'outerGlow', 'bevel'].includes(effect.type),
    );
    const raster = context.document.rasterSources.get(layer.id);
    if (unsupported && raster) {
      context.diagnostics.push({ level: 'warning', code: 'TEXT_RASTERIZED', message: `文本「${layer.name}」包含 FairyGUI 无法无损表达的效果，已栅格化`, layerId: layer.id });
      return this.imageNode(layer, raster, bounds, parentBounds, name, context);
    }
    return this.textXml({
      id: this.nodeId(context, layer, 'text'),
      name,
      bounds,
      parentBounds,
      text: layer.text ?? '',
      styles: layer.textStyles,
      context,
      layer,
    });
  }

  private textXml(input: {
    id: string;
    name: string;
    bounds: Rect;
    parentBounds: Rect;
    text: string;
    styles?: TextStyles;
    attributes?: XmlAttributes;
    context: BuildContext;
    layer: Layer;
  }): string {
    this.reportComplexSemantics(input.layer, input.context);
    const styles = input.styles;
    const color = styles ? this.color(styles.color) : '#000000';
    const font = styles ? this.resolveFont(styles.fontName, input.context, input.layer) : undefined;
    const align = styles?.alignment.horizontal === 'justify' ? 'left' : styles?.alignment.horizontal;
    const vAlign = styles?.alignment.vertical === 'middle' ? 'middle' : styles?.alignment.vertical;
    const effects = styles?.effects ?? [];
    const stroke = effects.find((effect): effect is StrokeEffect => effect.enabled && effect.type === 'stroke');
    const shadow = effects.find((effect): effect is ShadowEffect => effect.enabled && effect.type === 'dropShadow');
    return writeXmlElement('text', {
      id: input.id,
      name: input.name,
      xy: this.xy(input.bounds, input.parentBounds),
      size: this.size(input.bounds),
      font,
      fontSize: Math.max(1, Math.round(styles?.fontSize ?? 12)),
      color,
      bold: styles?.fontStyle.bold || undefined,
      italic: styles?.fontStyle.italic || undefined,
      align: align && align !== 'left' ? align : undefined,
      vAlign: vAlign && vAlign !== 'top' ? vAlign : undefined,
      autoSize: 'none',
      strokeColor: stroke ? this.color(stroke.color) : undefined,
      strokeSize: stroke?.width,
      shadowColor: shadow ? this.color(shadow.color) : undefined,
      shadowOffset: shadow ? `${shadow.offsetX},${shadow.offsetY}` : undefined,
      text: input.text,
      ...input.attributes,
    });
  }

  private resolveFont(fontName: string, context: BuildContext, layer: Layer): string {
    const mapping = context.options.fontMappings?.[fontName];
    const backend = context.components.get(layer.id)?.textBackend;
    const resolved = (backend ? mapping?.[backend] : undefined) ?? mapping?.default;
    if (!resolved) {
      context.diagnostics.push({ level: 'warning', code: 'FONT_FALLBACK', message: `字体「${fontName}」未配置映射，保留 PSD 字体名`, layerId: layer.id });
    }
    return resolved ?? fontName;
  }

  private async ensureProject(projectPath: string, packageName: string, document: ParsedPsdDocument): Promise<void> {
    await mkdir(projectPath, { recursive: true });
    const files = await readdir(projectPath);
    const projectFiles = files.filter(file => file.endsWith('.fairy'));
    if (projectFiles.length > 1) {
      throw new Error(`Multiple FairyGUI project files found in ${projectPath}`);
    }
    if (projectFiles.length === 0) {
      const projectId = createStableId('project', projectPath, 32);
      await writeFile(join(projectPath, `${packageName}.fairy`), `<?xml version="1.0" encoding="utf-8"?>\n<projectDescription id="${projectId}" type="Unity" version="3.0"/>\n`, 'utf8');
    }
    const packageDir = join(projectPath, 'assets', packageName);
    await mkdir(packageDir, { recursive: true });
    const packageXmlPath = join(packageDir, 'package.xml');
    if (!await this.exists(packageXmlPath)) {
      await writeFile(packageXmlPath, createPackageXml(createStableId('package', packageName), packageName), 'utf8');
    }
    const settingsDir = join(projectPath, 'settings');
    await mkdir(settingsDir, { recursive: true });
    await this.writeIfMissing(join(settingsDir, 'Common.json'), `${JSON.stringify({ font: 'Arial', fontSize: 12, textColor: '#000000', scrollBars: { defaultDisplay: 'visible', horizontal: '', vertical: '' } }, null, 2)}\n`);
    await this.writeIfMissing(join(settingsDir, 'Adaptation.json'), `${JSON.stringify({ designResolutionX: document.tree.metadata.canvasSize.width, designResolutionY: document.tree.metadata.canvasSize.height, devices: [], scaleMode: 'ScaleWithScreenSize', screenMathMode: 'MatchWidthOrHeight' }, null, 2)}\n`);
    await this.writeIfMissing(join(settingsDir, 'Publish.json'), `${JSON.stringify({ atlasSetting: { allowRotation: true, paging: true, sizeOption: 'pot', trimImage: true }, binaryFormat: true, compressDesc: true, includeHighResolution: 1, packageCount: 1, path: '' }, null, 2)}\n`);
    await this.writeIfMissing(join(projectPath, '.psd-exporter', '.gitignore'), 'staging/\ntransactions/\nexport.lock\n');
  }

  private async commit(
    projectPath: string,
    packageXmlPath: string,
    packageXml: string,
    manifest: ManagedPageManifest,
    oldManifest: ManagedPageManifest | undefined,
    files: Map<string, Buffer | string | Promise<Buffer>>,
  ): Promise<string[]> {
    const transactionId = `${manifest.pageId}-${Date.now()}-${createStableId('transaction', manifest.generation, 6)}`;
    const stagingDir = join(projectPath, '.psd-exporter', 'staging', transactionId);
    const transactionDir = join(projectPath, '.psd-exporter', 'transactions');
    const transactionPath = join(transactionDir, `${transactionId}.json`);
    await mkdir(stagingDir, { recursive: true });
    await mkdir(transactionDir, { recursive: true });
    const staged: Array<{ stagedPath: string; targetPath: string }> = [];
    let completed = false;
    const packageHash = this.hashContent(packageXml);
    try {
      for (const [relativePath, contentValue] of files) {
        const content = await Promise.resolve(contentValue);
        const stagedPath = join(stagingDir, relativePath);
        await mkdir(dirname(stagedPath), { recursive: true });
        await writeFile(stagedPath, content);
        staged.push({ stagedPath, targetPath: join(projectPath, relativePath) });
      }
      await writeFile(transactionPath, `${JSON.stringify({
        status: 'staged',
        pageId: manifest.pageId,
        stagingDir: relative(projectPath, stagingDir),
        files: staged.map(item => relative(projectPath, item.targetPath)),
        packageXmlPath: relative(projectPath, packageXmlPath),
        packageHash,
        manifest,
        oldManifest,
      }, null, 2)}\n`, 'utf8');
      for (const file of staged) {
        await mkdir(dirname(file.targetPath), { recursive: true });
        await rename(file.stagedPath, file.targetPath);
      }
      const packageTemp = `${packageXmlPath}.${transactionId}.tmp`;
      await writeFile(packageTemp, packageXml, 'utf8');
      await rename(packageTemp, packageXmlPath);
      await writeFile(transactionPath, `${JSON.stringify({
        status: 'package-committed',
        pageId: manifest.pageId,
        stagingDir: relative(projectPath, stagingDir),
        files: staged.map(item => relative(projectPath, item.targetPath)),
        packageXmlPath: relative(projectPath, packageXmlPath),
        packageHash,
        manifest,
        oldManifest,
      }, null, 2)}\n`, 'utf8');

      await this.writeManifest(projectPath, manifest, transactionId);
      await writeFile(transactionPath, `${JSON.stringify({ status: 'committed', pageId: manifest.pageId }, null, 2)}\n`, 'utf8');
      const cleanupFailures = await this.cleanupOldFiles(projectPath, oldManifest, manifest);
      completed = true;
      return cleanupFailures;
    } finally {
      await rm(stagingDir, { recursive: true, force: true });
      if (completed) await rm(transactionPath, { force: true });
    }
  }

  private async validateGeneratedFiles(
    projectPath: string,
    packageName: string,
    context: BuildContext,
    existingResources: PackageResource[],
    generatedResources: PackageResource[],
  ): Promise<void> {
    const errors = context.diagnostics
      .filter(diagnostic => diagnostic.level === 'error')
      .map(diagnostic => `${diagnostic.code}: ${diagnostic.message}`);
    const knownLocalIds = new Set(
      [...existingResources, ...generatedResources]
        .map(resource => resource.attributes.id)
        .filter((id): id is string => Boolean(id)),
    );
    const generatedFilePaths = new Set(context.files.keys());
    const referencedLocalIds = new Set<string>();

    for (const resource of context.resources) {
      if (!generatedFilePaths.has(resource.filePath)) {
        errors.push(`MISSING_RESOURCE_FILE: ${resource.filePath}`);
      }
    }

    for (const [relativePath, contentValue] of context.files) {
      const content = await Promise.resolve(contentValue);
      context.files.set(relativePath, content);
      if (typeof content !== 'string' || !relativePath.endsWith('.xml')) continue;

      for (const error of validateXmlDocument(content)) {
        errors.push(`INVALID_COMPONENT_XML: ${relativePath}: ${error}`);
      }

      const ids = new Set<string>();
      for (const match of content.matchAll(/\bid="([^"]+)"/g)) {
        if (ids.has(match[1])) {
          errors.push(`DUPLICATE_NODE_ID: ${relativePath}: ${match[1]}`);
        }
        ids.add(match[1]);
      }

      for (const match of content.matchAll(/\bsrc="([^"]+)"(?:\s+pkg="([^"]+)")?/g)) {
        if (!match[2] && !knownLocalIds.has(match[1])) {
          errors.push(`UNRESOLVED_REFERENCE: ${relativePath}: ${match[1]}`);
        } else if (!match[2]) {
          referencedLocalIds.add(match[1]);
        }
      }

      for (const match of content.matchAll(/\b(?:defaultItem|dropdown|url)="ui:\/\/([^"/]{8})([^"]+)"/g)) {
        if (match[1] === context.packageId && !knownLocalIds.has(match[2])) {
          errors.push(`UNRESOLVED_REFERENCE: ${relativePath}: ${match[2]}`);
        } else if (match[1] === context.packageId) {
          referencedLocalIds.add(match[2]);
        }
      }
    }

    for (const reference of Object.values(context.options.localReferences ?? {})) {
      if (!knownLocalIds.has(reference.resourceId)) {
        errors.push(`UNRESOLVED_REFERENCE: local resource ${reference.resourceId} is not registered in package ${packageName}`);
      }
    }

    for (const resource of existingResources) {
      if (!resource.attributes.id || !referencedLocalIds.has(resource.attributes.id)) continue;
      if (generatedResources.some(candidate => candidate.attributes.id === resource.attributes.id)) continue;
      const resourcePath = resource.attributes.path ?? '/';
      const resourceName = resource.attributes.name;
      if (!resourceName) {
        errors.push(`MISSING_RESOURCE_FILE: package resource ${resource.attributes.id} has no name`);
        continue;
      }
      const absolutePath = this.resolvePackageResourcePath(projectPath, packageName, resourcePath, resourceName);
      if (!await this.exists(absolutePath)) {
        errors.push(`MISSING_RESOURCE_FILE: ${relative(projectPath, absolutePath)}`);
      }
    }

    if (errors.length > 0) {
      throw new Error(errors.join('; '));
    }
  }

  private async acquireLock(projectPath: string): Promise<() => Promise<void>> {
    const lockPath = join(projectPath, '.psd-exporter', 'export.lock');
    await mkdir(dirname(lockPath), { recursive: true });
    let handle;
    for (let attempt = 0; attempt < 2; attempt++) {
      try {
        handle = await open(lockPath, 'wx');
        break;
      } catch (error) {
        if ((error as NodeJS.ErrnoException).code !== 'EEXIST') throw error;
        if (attempt === 0 && await this.removeStaleLock(lockPath)) continue;
        throw new Error(`FairyGUI project is locked by another export: ${lockPath}`);
      }
    }
    if (!handle) throw new Error(`Unable to acquire FairyGUI export lock: ${lockPath}`);
    await handle.writeFile(`${JSON.stringify({ pid: process.pid, startedAt: new Date().toISOString() }, null, 2)}\n`);
    return async () => {
      await handle.close();
      await rm(lockPath, { force: true });
    };
  }

  private async recoverTransactions(projectPath: string): Promise<void> {
    const transactionDir = join(projectPath, '.psd-exporter', 'transactions');
    if (!await this.exists(transactionDir)) return;
    for (const fileName of await readdir(transactionDir)) {
      if (!fileName.endsWith('.json')) continue;
      const transactionPath = join(transactionDir, fileName);
      const transaction = JSON.parse(await readFile(transactionPath, 'utf8')) as {
        status?: string;
        stagingDir?: string;
        files?: string[];
        packageXmlPath?: string;
        packageHash?: string;
        manifest?: ManagedPageManifest;
        oldManifest?: ManagedPageManifest;
      };
      const packageXmlPath = transaction.packageXmlPath
        ? this.resolveTransactionPackagePath(projectPath, transaction)
        : undefined;
      const packageCommitted = Boolean(packageXmlPath
        && transaction.packageHash
        && await this.fileHash(packageXmlPath) === transaction.packageHash);
      if (packageCommitted && transaction.manifest) {
        const packageXml = await readFile(packageXmlPath!, 'utf8');
        const packageErrors = validatePackageXml(packageXml);
        if (packageErrors.length > 0) {
          throw new Error(`FairyGUI package validation failed during recovery: ${packageErrors.join('; ')}`);
        }
        await this.validateManagedManifest(
          projectPath,
          transaction.manifest.packageName,
          readPackageInfo(packageXml),
          transaction.manifest,
        );
        if (transaction.oldManifest) {
          if (transaction.oldManifest.pageId !== transaction.manifest.pageId
            || transaction.oldManifest.packageName !== transaction.manifest.packageName
            || transaction.oldManifest.packageId !== transaction.manifest.packageId) {
            throw new Error('FairyGUI transaction old manifest does not match the committed page');
          }
          const persistedManifest = await this.readManifest(projectPath, transaction.oldManifest.pageId);
          if (!persistedManifest
            || this.hashContent(JSON.stringify(persistedManifest)) !== this.hashContent(JSON.stringify(transaction.oldManifest))) {
            throw new Error('FairyGUI transaction old manifest does not match the persisted manifest');
          }
          await this.validateManagedManifestFiles(projectPath, transaction.oldManifest);
        }
        await this.writeManifest(projectPath, transaction.manifest, `recovery-${Date.now()}`);
        await this.cleanupOldFiles(projectPath, transaction.oldManifest, transaction.manifest);
      } else {
        const oldFiles = new Set(transaction.oldManifest?.files ?? []);
        if (transaction.stagingDir) {
          await rm(this.resolveProjectSubpath(projectPath, '.psd-exporter/staging', transaction.stagingDir), { recursive: true, force: true });
        }
        for (const file of transaction.files ?? []) {
          if (!oldFiles.has(file)) {
            if (!transaction.manifest) {
              throw new Error(`FairyGUI transaction is missing a manifest for file cleanup: ${file}`);
            }
            await rm(this.resolveManagedGenerationFilePath(projectPath, transaction.manifest, file), { force: true });
          }
        }
      }
      await rm(transactionPath, { force: true });
    }
  }

  private async removeStaleLock(lockPath: string): Promise<boolean> {
    try {
      const lock = JSON.parse(await readFile(lockPath, 'utf8')) as { pid?: number };
      if (typeof lock.pid === 'number') {
        try {
          process.kill(lock.pid, 0);
          return false;
        } catch (error) {
          if ((error as NodeJS.ErrnoException).code !== 'ESRCH') return false;
        }
      }
      await rm(lockPath, { force: true });
      return true;
    } catch {
      await rm(lockPath, { force: true });
      return true;
    }
  }

  private async writeManifest(projectPath: string, manifest: ManagedPageManifest, suffix: string): Promise<void> {
    this.assertSafePathSegment(manifest.pageId, 'pageId');
    const manifestDir = join(projectPath, '.psd-exporter', 'pages');
    await mkdir(manifestDir, { recursive: true });
    const manifestPath = this.resolveProjectPath(manifestDir, `${manifest.pageId}.json`);
    const manifestTemp = `${manifestPath}.${suffix}.tmp`;
    await writeFile(manifestTemp, `${JSON.stringify(manifest, null, 2)}\n`, 'utf8');
    await rename(manifestTemp, manifestPath);
  }

  private async cleanupOldFiles(projectPath: string, oldManifest: ManagedPageManifest | undefined, manifest: ManagedPageManifest): Promise<string[]> {
    if (!oldManifest) return [];
    const failures: string[] = [];
    const current = new Set(manifest.files);
    for (const file of oldManifest.files) {
      if (current.has(file)) continue;
      try {
        await unlink(this.resolveManagedGenerationFilePath(projectPath, oldManifest, file));
      } catch (error) {
        if ((error as NodeJS.ErrnoException).code !== 'ENOENT') {
          failures.push(file);
        }
      }
    }
    return failures;
  }

  private async readManifest(projectPath: string, pageId: string): Promise<ManagedPageManifest | undefined> {
    const path = join(projectPath, '.psd-exporter', 'pages', `${pageId}.json`);
    try {
      return JSON.parse(await readFile(path, 'utf8')) as ManagedPageManifest;
    } catch (error) {
      if ((error as NodeJS.ErrnoException).code === 'ENOENT') return undefined;
      throw error;
    }
  }

  private async validateManagedManifest(
    projectPath: string,
    packageName: string,
    packageInfo: ReturnType<typeof readPackageInfo>,
    manifest: ManagedPageManifest,
  ): Promise<void> {
    if (manifest.packageName !== packageName || manifest.packageId !== packageInfo.packageId) {
      throw new Error(`FairyGUI managed manifest does not match package ${packageName}`);
    }
    await this.validateManagedManifestFiles(projectPath, manifest);
    const packageResources = new Map(
      packageInfo.resources
        .filter(resource => Boolean(resource.attributes.id))
        .map(resource => [resource.attributes.id, resource]),
    );
    for (const resource of manifest.resources) {
      const packageResource = packageResources.get(resource.id);
      const expectedPath = resource.packagePath;
      if (!packageResource
        || packageResource.tag !== resource.type
        || packageResource.attributes.name !== resource.name
        || (packageResource.attributes.path ?? '/') !== expectedPath) {
        throw new Error(`FairyGUI managed manifest resource does not match package.xml: ${resource.id}`);
      }
    }
  }

  private async validateManagedManifestFiles(
    projectPath: string,
    manifest: ManagedPageManifest,
  ): Promise<void> {
    this.assertSafePathSegment(manifest.pageId, 'pageId');
    this.assertSafePathSegment(manifest.packageName, 'packageName');
    this.assertSafePathSegment(manifest.generation, 'generation');
    const fileSet = new Set(manifest.files);
    const resourceFiles = new Set(manifest.resources.map(resource => resource.filePath));
    const resourceIds = new Set(manifest.resources.map(resource => resource.id));
    if (resourceIds.size !== manifest.resources.length
      || resourceFiles.size !== manifest.resources.length
      || fileSet.size !== manifest.resources.length
      || [...fileSet].some(file => !resourceFiles.has(file))) {
      throw new Error('FairyGUI managed manifest files do not match managed resources');
    }
    const rootResource = manifest.resources.find(resource => resource.id === manifest.rootComponentId);
    if (!rootResource || rootResource.type !== 'component' || rootResource.name !== `${manifest.componentName}.xml`) {
      throw new Error('FairyGUI managed manifest root component does not match managed resources');
    }
    for (const resource of manifest.resources) {
      if (!resource.id || !resource.contentHash) {
        throw new Error(`FairyGUI managed manifest resource is missing integrity metadata: ${resource.filePath}`);
      }
      if (!fileSet.has(resource.filePath)) {
        throw new Error(`FairyGUI managed manifest resource is missing from files: ${resource.filePath}`);
      }
      const absolutePath = this.resolveManagedGenerationFilePath(projectPath, manifest, resource.filePath);
      const registeredPath = this.resolvePackageResourcePath(
        projectPath,
        manifest.packageName,
        resource.packagePath,
        resource.name,
      );
      if (absolutePath !== registeredPath) {
        throw new Error(`FairyGUI managed manifest file does not match package resource path: ${resource.id}`);
      }
      const contentHash = await this.fileHash(absolutePath);
      if (contentHash !== resource.contentHash) {
        throw new Error(`FairyGUI managed manifest content hash mismatch: ${resource.filePath}`);
      }
    }
    for (const file of manifest.files) this.resolveManagedGenerationFilePath(projectPath, manifest, file);
  }

  private async validateGenerationContents(projectPath: string, context: BuildContext): Promise<void> {
    for (const [relativePath, contentValue] of context.files) {
      const absolutePath = this.resolveProjectPath(projectPath, relativePath);
      const existingHash = await this.fileHash(absolutePath);
      if (!existingHash) continue;
      const content = await Promise.resolve(contentValue);
      if (existingHash !== this.hashContent(content)) {
        throw new Error(`FairyGUI generation content conflict: ${relativePath}`);
      }
    }
  }

  private async finalizeGeneration(context: BuildContext, baseGeneration: string): Promise<string> {
    const resourceSignatures = await Promise.all(
      context.resources
        .map(async resource => {
          const contentValue = context.files.get(resource.filePath);
          const content = contentValue === undefined ? undefined : await Promise.resolve(contentValue);
          if (content !== undefined) context.files.set(resource.filePath, content);
          return {
            id: resource.id,
            type: resource.type,
            name: resource.name,
            attributes: resource.attributes,
            contentHash: content === undefined ? undefined : this.hashContent(content),
          };
        }),
    );
    resourceSignatures.sort((left, right) => left.id.localeCompare(right.id));
    return createStableId('generation-output', JSON.stringify({ baseGeneration, resourceSignatures }), 10);
  }

  private relocateGeneration(
    context: BuildContext,
    previousBase: string,
    nextBase: string,
    generation: string,
  ): void {
    const relocate = (filePath: string): string => join(nextBase, relative(previousBase, filePath));
    context.files = new Map([...context.files].map(([filePath, content]) => [relocate(filePath), content]));
    for (const resource of context.resources) {
      resource.filePath = relocate(resource.filePath);
      resource.packagePath = this.packagePathFor(dirname(resource.filePath));
    }
    for (const [name, filePath] of Object.entries(context.componentPaths)) {
      context.componentPaths[name] = relocate(filePath);
    }
    context.generation = generation;
    context.componentDir = join(nextBase, 'components');
    context.imageDir = join(nextBase, 'images');
  }

  private async createGeneration(
    document: ParsedPsdDocument,
    components: Map<string, ComponentInfo>,
    pageId: string,
    packageId: string,
    componentName: string,
    rootComponentId: string,
    options: FairyGuiExportOptions,
    projectPath: string,
    packageName: string,
    packageResources: PackageResource[],
    oldManagedIds: ReadonlySet<string>,
    existingLocalResourceIds: ReadonlyMap<string, string>,
  ): Promise<string> {
    const referencedNames = this.localReferenceNames(document.tree.root);
    const resolvedExistingReferences = [...existingLocalResourceIds.entries()]
      .filter(([name]) => referencedNames.has(name))
      .sort(([left], [right]) => left.localeCompare(right));
    const referencedIds = new Set(resolvedExistingReferences.map(([, id]) => id));
    for (const [name, reference] of Object.entries(options.localReferences ?? {})) {
      if (referencedNames.has(name)) referencedIds.add(reference.resourceId);
    }
    const manualResourceSignatures = await Promise.all(
      packageResources
        .filter(resource => resource.attributes.id && !oldManagedIds.has(resource.attributes.id) && referencedIds.has(resource.attributes.id))
        .sort((left, right) => (left.attributes.id ?? '').localeCompare(right.attributes.id ?? ''))
        .map(async resource => ({
          tag: resource.tag,
          attributes: resource.attributes,
          contentHash: resource.tag === 'component' && resource.attributes.name
            ? await this.fileHash(this.resolvePackageResourcePath(
              projectPath,
              packageName,
              resource.attributes.path ?? '/',
              resource.attributes.name,
            ))
            : undefined,
        })),
    );
    const signature = JSON.stringify({
      pageId,
      packageId,
      componentName,
      rootComponentId,
      generatorRevision: FairyGuiExporter.generatorRevision,
      canvas: document.tree.metadata.canvasSize,
      layers: this.layerSignature(document.tree.root),
      components: [...components.entries()].sort(([left], [right]) => left.localeCompare(right)),
      rasters: [...document.rasterSources.entries()]
        .sort(([left], [right]) => left.localeCompare(right))
        .map(([layerId, raster]) => ({
          layerId,
          width: raster.width,
          height: raster.height,
          rgba: this.hashBytes(raster.rgba),
          mask: raster.mask ? {
            x: raster.mask.x,
            y: raster.mask.y,
            width: raster.mask.width,
            height: raster.mask.height,
            rgba: this.hashBytes(raster.mask.rgba),
          } : undefined,
        })),
      options: {
        defaultScale9: options.defaultScale9,
        fontMappings: options.fontMappings,
        localReferences: options.localReferences,
        externalReferences: options.externalReferences,
      },
      resolvedExistingReferences,
      manualResources: manualResourceSignatures,
    });
    return createStableId('generation', signature, 10);
  }

  private layerSignature(layer: Layer): unknown {
    return {
      sourceId: layer.sourceId,
      name: layer.name,
      type: layer.type,
      bounds: layer.bounds,
      visible: layer.visible,
      opacity: layer.opacity,
      maskType: layer.maskType,
      blendMode: layer.blendMode,
      clipping: layer.clipping,
      text: layer.text,
      textStyles: layer.textStyles,
      children: layer.children?.map(child => this.layerSignature(child)),
    };
  }

  private localReferenceNames(root: Layer): Set<string> {
    const names = new Set<string>();
    const visit = (layer: Layer): void => {
      const parsed = this.tagParser.parse(layer.name);
      if (parsed?.prefix === 'ref') names.add(sanitizeFairyName(parsed.baseName, 'Resource'));
      for (const child of layer.children ?? []) visit(child);
    };
    visit(root);
    return names;
  }

  private hashBytes(bytes: Uint8ClampedArray): string {
    return createHash('sha256')
      .update(Buffer.from(bytes.buffer, bytes.byteOffset, bytes.byteLength))
      .digest('hex');
  }

  private hashContent(content: string | Buffer): string {
    return createHash('sha256').update(content).digest('hex');
  }

  private async fileHash(path: string): Promise<string | undefined> {
    try {
      return this.hashContent(await readFile(path));
    } catch (error) {
      if ((error as NodeJS.ErrnoException).code === 'ENOENT') return undefined;
      throw error;
    }
  }

  private indexLocalResources(
    root: Layer,
    components: Map<string, ComponentInfo>,
    pageId: string,
    layerIdentityKeys: ReadonlyMap<string, string>,
    ids: StableIdAllocator,
  ): Map<string, string> {
    const candidates = new Map<string, string[]>();
    const visit = (layer: Layer) => {
      const parsed = this.tagParser.parse(layer.name);
      const component = components.get(layer.id);
      if (!parsed?.prefix) {
        const name = sanitizeFairyName(parsed?.baseName || layer.name, 'Layer');
        let resourceId: string | undefined;
        if (parsed?.directives.component) {
          resourceId = ids.allocate('component', `${pageId}|${this.layerKeyFromMap(layer, layerIdentityKeys)}|component`);
        } else if (component?.source === 'tag' && layer.children?.length) {
          if (component.type === 'Button' || component.type === 'Toggle') {
            resourceId = ids.allocate('component', `${pageId}|${this.layerKeyFromMap(layer, layerIdentityKeys)}|${component.type === 'Toggle' ? 'toggle' : 'button'}`);
          } else if (component.type === 'InputField') {
            resourceId = ids.allocate('component', `${pageId}|${this.layerKeyFromMap(layer, layerIdentityKeys)}|input`);
          }
        }
        if (resourceId) candidates.set(name, [...(candidates.get(name) ?? []), resourceId]);
      }
      for (const child of layer.children ?? []) visit(child);
    };
    visit(root);
    return new Map(
      [...candidates.entries()]
        .filter(([, ids]) => new Set(ids).size === 1)
        .map(([name, ids]) => [name, ids[0]]),
    );
  }

  private indexExistingLocalResources(resources: PackageResource[], excludedIds: ReadonlySet<string>): Map<string, string> {
    const candidates = new Map<string, string[]>();
    for (const resource of resources) {
      const id = resource.attributes.id;
      const resourceName = resource.attributes.name;
      if (resource.tag !== 'component' || !id || !resourceName || excludedIds.has(id)) continue;
      const logicalName = sanitizeFairyName(resourceName.replace(/\.xml$/i, ''), 'Resource');
      candidates.set(logicalName, [...(candidates.get(logicalName) ?? []), id]);
    }
    return new Map(
      [...candidates.entries()]
        .filter(([, ids]) => new Set(ids).size === 1)
        .map(([name, ids]) => [name, ids[0]]),
    );
  }

  private decorateManagedResources(context: BuildContext): void {
    for (const resource of context.resources) {
      const content = context.files.get(resource.filePath);
      if (content === undefined || content instanceof Promise) continue;
      const buffer = typeof content === 'string' ? Buffer.from(content, 'utf8') : content;
      resource.contentHash = createHash('sha256').update(buffer).digest('hex');
      if (typeof content === 'string') {
        resource.references = [
          ...content.matchAll(/\bsrc="([^"]+)"/g),
          ...content.matchAll(/\b(?:defaultItem|dropdown|url)="ui:\/\/([^"/]{8})([^"]+)"/g),
        ].map(match => match[2] ?? match[1]);
      }
    }
  }

  private sourceKey(document: ParsedPsdDocument, options: FairyGuiExportOptions): string {
    const psdPath = resolve(document.tree.metadata.psdPath);
    return options.sourceRoot ? relative(resolve(options.sourceRoot), psdPath) : psdPath;
  }

  private isSameSourcePath(manifestPath: string, currentPath: string, sourceRoot?: string, manifestSourceRoot?: string): boolean {
    const normalizedCurrent = resolve(currentPath).replace(/\\/g, '/');
    if (isAbsolute(manifestPath) || /^[A-Za-z]:[\\/]/.test(manifestPath)) {
      return resolve(manifestPath).replace(/\\/g, '/') === normalizedCurrent;
    }
    if (!sourceRoot) return false;
    const normalizedManifest = manifestPath.replace(/\\/g, '/').replace(/^\.\//, '');
    if (manifestSourceRoot
      && resolve(sourceRoot).replace(/\\/g, '/') !== resolve(manifestSourceRoot).replace(/\\/g, '/')) return false;
    return resolve(sourceRoot, normalizedManifest).replace(/\\/g, '/') === normalizedCurrent;
  }


  private shouldRender(layer: Layer, component: ComponentInfo | undefined): boolean {
    return layer.visible || Boolean(component?.role);
  }

  private effectiveBounds(layer: Layer): Rect {
    if (layer.bounds.width > 0 && layer.bounds.height > 0) return layer.bounds;
    const children = layer.children ?? [];
    if (children.length === 0) return layer.bounds;
    const bounds = children.map(child => this.effectiveBounds(child));
    const minX = Math.min(...bounds.map(item => item.x));
    const minY = Math.min(...bounds.map(item => item.y));
    const maxX = Math.max(...bounds.map(item => item.x + item.width));
    const maxY = Math.max(...bounds.map(item => item.y + item.height));
    return { x: minX, y: minY, width: maxX - minX, height: maxY - minY };
  }

  private createLayerIdentityKeys(root: Layer, diagnostics: FairyDiagnostic[]): ReadonlyMap<string, string> {
    const keys = new Map<string, string>();
    const visit = (layer: Layer, identityPath: string): void => {
      const parsedName = this.tagParser.parse(layer.name)?.baseName || layer.name;
      const name = sanitizeFairyName(parsedName, 'Layer');
      const bounds = this.effectiveBounds(layer);
      const geometry = `${Math.round(bounds.x)},${Math.round(bounds.y)},${Math.round(bounds.width)},${Math.round(bounds.height)}`;
      const key = layer.sourceId === undefined
        ? `fallback:${identityPath || name}@${geometry}`
        : String(layer.sourceId);
      keys.set(layer.id, key);
      if (layer.sourceId === undefined && layer !== root) {
        diagnostics.push({
          level: 'warning',
          code: 'SOURCE_ID_FALLBACK',
          message: `图层「${layer.name}」缺少 Photoshop sourceId，使用祖先名称与几何路径保持身份`,
          layerId: layer.id,
        });
      }

      const children = layer.children ?? [];
      const ordinals = new Map<string, number>();
      const sorted = [...children].sort((left, right) => {
        const leftName = sanitizeFairyName(this.tagParser.parse(left.name)?.baseName || left.name, 'Layer');
        const rightName = sanitizeFairyName(this.tagParser.parse(right.name)?.baseName || right.name, 'Layer');
        if (leftName !== rightName) return leftName.localeCompare(rightName);
        const leftBounds = this.effectiveBounds(left);
        const rightBounds = this.effectiveBounds(right);
        return [leftBounds.x, leftBounds.y, leftBounds.width, leftBounds.height]
          .map((value, index) => value - [rightBounds.x, rightBounds.y, rightBounds.width, rightBounds.height][index])
          .find(value => value !== 0) ?? left.id.localeCompare(right.id);
      });
      const childSegments = new Map<string, string>();
      for (const child of sorted) {
        const childName = sanitizeFairyName(this.tagParser.parse(child.name)?.baseName || child.name, 'Layer');
        const ordinal = ordinals.get(childName) ?? 0;
        ordinals.set(childName, ordinal + 1);
        childSegments.set(child.id, `${childName}[${ordinal}]`);
      }
      for (const child of children) {
        const childPath = childSegments.get(child.id) ?? sanitizeFairyName(child.name, 'Layer');
        visit(child, identityPath ? `${identityPath}/${childPath}` : childPath);
      }
    };
    const rootName = sanitizeFairyName(this.tagParser.parse(root.name)?.baseName || root.name, 'Layer');
    visit(root, `${rootName}[0]`);
    return keys;
  }

  private layerKey(layer: Layer, context: BuildContext): string {
    return this.layerKeyFromMap(layer, context.layerIdentityKeys);
  }

  private layerKeyFromMap(layer: Layer, keys: ReadonlyMap<string, string>): string {
    return keys.get(layer.id) ?? String(layer.sourceId ?? layer.id);
  }

  private nodeId(context: BuildContext, layer: Layer, role: string): string {
    return context.ids.allocate('node', `${context.pageId}|${this.layerKey(layer, context)}|${role}`);
  }

  private resourceFileName(
    name: string,
    key: string,
    extension: 'xml' | 'png',
    context: BuildContext,
  ): string {
    return `${sanitizeFairyName(name)}_${context.ids.allocate('name', key, 6)}.${extension}`;
  }

  private packagePathFor(relativeDirectory: string): string {
    const parts = relativeDirectory.split(/[\\/]+/);
    const assetsIndex = parts.indexOf('assets');
    const packageRelative = assetsIndex >= 0 ? parts.slice(assetsIndex + 2).join('/') : relativeDirectory.replace(/\\/g, '/');
    return `/${packageRelative.replace(/^\/+|\/+$/g, '')}/`;
  }

  private componentXml(bounds: Pick<Rect, 'width' | 'height'>, children: string[]): string {
    return writeComponentXml({
      width: bounds.width,
      height: bounds.height,
      displayList: children,
    });
  }

  private parseAttributeFragment(fragment: string): Record<string, string> {
    return Object.fromEntries(
      [...fragment.matchAll(/([A-Za-z_:][\w:.-]*)="([^"]*)"/g)]
        .map(match => [match[1], match[2]]),
    );
  }

  private buttonPages(role: string | undefined, toggle: boolean): string | undefined {
    if (role === 'press') return '1';
    if (role === 'onover') return '2';
    if (toggle && role === 'mark') return '1,3';
    if (role === 'select') return '3';
    if (role === 'disable') return '4,5';
    if (role === 'bg') return toggle ? '0,1,2,3' : '0';
    return undefined;
  }

  private hasRole(layer: Layer, role: string, context: BuildContext): boolean {
    return (layer.children ?? []).some(child => context.components.get(child.id)?.role === role);
  }

  private findByRole(layer: Layer, role: string, context: BuildContext): Layer | undefined {
    for (const child of layer.children ?? []) {
      if (context.components.get(child.id)?.role === role) return child;
      const nested = this.findByRole(child, role, context);
      if (nested) return nested;
    }
    return undefined;
  }

  private layersByRoles(layer: Layer, roles: string[], context: BuildContext): Layer[] {
    const roleSet = new Set(roles);
    const matches: Layer[] = [];
    const visit = (parent: Layer) => {
      for (const child of parent.children ?? []) {
        if (roleSet.has(context.components.get(child.id)?.role ?? '')) matches.push(child);
        visit(child);
      }
    };
    visit(layer);
    return matches;
  }

  private addChildXml(xml: string, child: string): string {
    return xml.replace(/\/>$/, `>\n${this.indent(child, 2)}\n</${/^<([a-z]+)/.exec(xml)?.[1] ?? 'image'}>`);
  }

  private assignGroup(xml: string, groupId: string): string {
    return xml.replace(/^<([a-z]+)(\s)/, `<$1 group="${groupId}"$2`);
  }

  private defaultScale9(
    source: RasterSource,
    configured?: Scale9Margins & { unit?: 'pixels' | 'ratio' },
  ): Scale9Margins {
    if (configured?.unit === 'ratio') {
      return {
        left: Math.floor(source.width * configured.left),
        top: Math.floor(source.height * configured.top),
        right: Math.floor(source.width * configured.right),
        bottom: Math.floor(source.height * configured.bottom),
      };
    }
    if (configured) return configured;
    return {
      left: Math.floor(source.width * 0.3),
      top: Math.floor(source.height * 0.3),
      right: Math.floor(source.width * 0.3),
      bottom: Math.floor(source.height * 0.3),
    };
  }

  private color(value: { r: number; g: number; b: number; a: number }): string {
    const channel = (number: number) => Math.max(0, Math.min(255, Math.round(number * 255))).toString(16).padStart(2, '0');
    const alpha = channel(value.a);
    const rgb = `${channel(value.r)}${channel(value.g)}${channel(value.b)}`;
    return value.a < 1 ? `#${alpha}${rgb}` : `#${rgb}`;
  }

  private xy(bounds: Rect, parent: Rect): string {
    return `${Math.round(bounds.x - parent.x)},${Math.round(bounds.y - parent.y)}`;
  }

  private size(bounds: Pick<Rect, 'width' | 'height'>): string {
    return `${Math.max(0, Math.round(bounds.width))},${Math.max(0, Math.round(bounds.height))}`;
  }

  private indent(value: string, spaces: number): string {
    const prefix = ' '.repeat(spaces);
    return value ? value.split('\n').map(line => `${prefix}${line}`).join('\n') : '';
  }

  private async exists(path: string): Promise<boolean> {
    try {
      await access(path, constants.F_OK);
      return true;
    } catch {
      return false;
    }
  }

  private resolveProjectPath(projectPath: string, projectRelativePath: string): string {
    if (!projectRelativePath || isAbsolute(projectRelativePath)) {
      throw new Error(`FairyGUI sidecar path escapes project root: ${projectRelativePath}`);
    }
    const projectRoot = resolve(projectPath);
    const absolutePath = resolve(projectRoot, projectRelativePath);
    const relativePath = relative(projectRoot, absolutePath);
    if (relativePath === '' || isAbsolute(relativePath) || relativePath === '..' || relativePath.startsWith(`..${sep}`)) {
      throw new Error(`FairyGUI sidecar path escapes project root: ${projectRelativePath}`);
    }
    return absolutePath;
  }

  private resolveProjectSubpath(projectPath: string, allowedRoot: string, projectRelativePath: string): string {
    const absolutePath = this.resolveProjectPath(projectPath, projectRelativePath);
    const absoluteRoot = this.resolveProjectPath(projectPath, allowedRoot);
    const relativePath = relative(absoluteRoot, absolutePath);
    if (relativePath === '' || isAbsolute(relativePath) || relativePath === '..' || relativePath.startsWith(`..${sep}`)) {
      throw new Error(`FairyGUI sidecar path escapes managed root: ${projectRelativePath}`);
    }
    return absolutePath;
  }

  private resolveManagedFilePath(projectPath: string, manifest: ManagedPageManifest, filePath: string): string {
    this.assertSafePathSegment(manifest.packageName, 'packageName');
    this.assertSafePathSegment(manifest.pageId, 'pageId');
    const managedRoot = join('assets', manifest.packageName, 'psd', manifest.pageId);
    return this.resolveProjectSubpath(projectPath, managedRoot, filePath);
  }

  private resolveManagedGenerationFilePath(
    projectPath: string,
    manifest: ManagedPageManifest,
    filePath: string,
  ): string {
    this.assertSafePathSegment(manifest.packageName, 'packageName');
    this.assertSafePathSegment(manifest.pageId, 'pageId');
    this.assertSafePathSegment(manifest.generation, 'generation');
    const generationRoot = join('assets', manifest.packageName, 'psd', manifest.pageId, manifest.generation);
    return this.resolveProjectSubpath(projectPath, generationRoot, filePath);
  }

  private resolvePackageResourcePath(
    projectPath: string,
    packageName: string,
    resourcePath: string,
    resourceName: string,
  ): string {
    this.assertSafePathSegment(packageName, 'packageName');
    const packageRoot = join('assets', packageName);
    const relativePath = join(packageRoot, resourcePath.replace(/^\/+/, ''), resourceName);
    return this.resolveProjectSubpath(projectPath, packageRoot, relativePath);
  }

  private resolveTransactionPackagePath(
    projectPath: string,
    transaction: { packageXmlPath?: string; manifest?: ManagedPageManifest },
  ): string {
    if (transaction.manifest) {
      this.assertSafePathSegment(transaction.manifest.packageName, 'packageName');
    }
    const packageXmlPath = this.resolveProjectPath(projectPath, transaction.packageXmlPath ?? '');
    if (!transaction.manifest) return packageXmlPath;
    const expectedPath = this.resolveProjectPath(
      projectPath,
      join('assets', transaction.manifest.packageName, 'package.xml'),
    );
    if (packageXmlPath !== expectedPath) {
      throw new Error(`FairyGUI transaction package path does not match its manifest: ${transaction.packageXmlPath}`);
    }
    return packageXmlPath;
  }

  private assertSafePathSegment(value: string, label: string): void {
    if (!value || sanitizeFairyName(value, '') !== value || value === '.' || value === '..') {
      throw new Error(`FairyGUI sidecar ${label} is not a safe path segment: ${value}`);
    }
  }

  private async writeIfMissing(path: string, content: string): Promise<void> {
    if (!await this.exists(path)) {
      await writeFile(path, content, 'utf8');
    }
  }
}
