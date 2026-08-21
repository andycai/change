import { access, mkdtemp, readFile, rm, writeFile, mkdir } from 'fs/promises';
import { createHash } from 'crypto';
import { tmpdir } from 'os';
import { join } from 'path';
import { FairyGuiExporter } from '../../src/fairygui/fairygui-exporter';
import * as reportWriter from '../../src/fairygui/report-writer';
import { ParsedPsdDocument } from '../../src/parser/psd-document';
import { ComponentInfo } from '../../src/recognizer/component-types';
import { Layer } from '../../src/parser/layer-tree';

function imageRgba(width: number, height: number): Uint8ClampedArray {
  const rgba = new Uint8ClampedArray(width * height * 4);
  for (let index = 0; index < rgba.length; index += 4) {
    rgba[index] = 20;
    rgba[index + 1] = 100;
    rgba[index + 2] = 200;
    rgba[index + 3] = 255;
  }
  return rgba;
}

function makeDocument(psdPath: string): ParsedPsdDocument {
  const rgba = imageRgba(100, 40);
  return {
    tree: {
      metadata: {
        psdPath,
        canvasSize: { x: 0, y: 0, width: 320, height: 180 },
        timestamp: '2026-08-20T00:00:00.000Z',
      },
      root: {
        id: 'root', name: 'canvas', type: 'group',
        bounds: { x: 0, y: 0, width: 320, height: 180 }, visible: true, opacity: 1,
        children: [{
          id: 'root_0', sourceId: 10, name: 'Start.bt', type: 'group',
          bounds: { x: 20, y: 30, width: 100, height: 40 }, visible: true, opacity: 1,
          children: [{
            id: 'root_0_0', sourceId: 11, name: 'StartBg.bg', type: 'image',
            bounds: { x: 20, y: 30, width: 100, height: 40 }, visible: true, opacity: 1,
          }, {
            id: 'root_0_1', sourceId: 12, name: 'StartLabel.bttxt', type: 'text',
            bounds: { x: 40, y: 38, width: 60, height: 24 }, visible: true, opacity: 1,
            text: '开始',
            textStyles: {
              fontSize: 20,
              color: { r: 1, g: 1, b: 1, a: 1 },
              fontName: 'PingFang SC',
              fontStyle: { bold: false, italic: false },
              alignment: { horizontal: 'center', vertical: 'middle' },
            },
          }],
        }],
      },
    },
    rasterSources: new Map([
      ['root_0_0', { layerId: 'root_0_0', photoshopLayerId: 11, width: 100, height: 40, rgba }],
    ]),
  };
}

function makeReferenceDocument(psdPath: string): ParsedPsdDocument {
  return {
    tree: {
      metadata: {
        psdPath,
        canvasSize: { x: 0, y: 0, width: 320, height: 180 },
        timestamp: '2026-08-20T00:00:00.000Z',
      },
      root: {
        id: 'root', name: 'canvas', type: 'group',
        bounds: { x: 0, y: 0, width: 320, height: 180 }, visible: true, opacity: 1,
        children: [{
          id: 'root_0', sourceId: 20, name: 'ref SharedButton', type: 'shape',
          bounds: { x: 10, y: 20, width: 100, height: 40 }, visible: true, opacity: 1,
        }, {
          id: 'root_1', sourceId: 21, name: 'refp CommonClose', type: 'shape',
          bounds: { x: 200, y: 20, width: 40, height: 40 }, visible: true, opacity: 1,
        }],
      },
    },
    rasterSources: new Map(),
  };
}

function makeComponents(): Map<string, ComponentInfo> {
  return new Map([
    ['root_0', { type: 'Button', confidence: 1, source: 'tag', needsReview: false }],
    ['root_0_0', { type: 'Unknown', role: 'bg', confidence: 1, source: 'tag', needsReview: false }],
    ['root_0_1', { type: 'Unknown', role: 'bttxt', confidence: 1, source: 'tag', needsReview: false }],
  ]);
}

describe('FairyGuiExporter', () => {
  let projectPath: string;

  beforeEach(async () => {
    projectPath = await mkdtemp(join(tmpdir(), 'psd-fgui-export-'));
  });

  afterEach(async () => {
    await rm(projectPath, { recursive: true, force: true });
  });

  test('creates a FairyGUI 6 project with a native button and reports', async () => {
    const exporter = new FairyGuiExporter();
    const result = await exporter.export(makeDocument('/art/menu.psd'), makeComponents(), {
      projectPath,
      packageName: 'PSDImport',
      componentName: 'Menu',
      pageId: 'menu',
    });

    expect(result.packageId).toHaveLength(8);
    expect(result.rootComponentId).toHaveLength(8);
    expect(await readFile(join(projectPath, 'PSDImport.fairy'), 'utf8')).toContain('version="3.0"');
    const packageXml = await readFile(join(projectPath, 'assets/PSDImport/package.xml'), 'utf8');
    expect(packageXml).toContain(`id="${result.rootComponentId}"`);
    const rootXml = await readFile(join(projectPath, result.rootComponentPath), 'utf8');
    expect(rootXml).toContain('name="Start"');
    const buttonXml = await readFile(join(projectPath, result.componentPaths.Start), 'utf8');
    expect(buttonXml).toContain('extention="Button"');
    expect(buttonXml).toContain('name="button"');
    expect(buttonXml).toContain('name="title"');
    const report = JSON.parse(await readFile(join(projectPath, '.psd-exporter/reports/menu.json'), 'utf8'));
    const markdown = await readFile(join(projectPath, '.psd-exporter/reports/menu.md'), 'utf8');
    expect(report.pageId).toBe('menu');
    expect(report.mappings).toEqual(expect.arrayContaining([
      expect.objectContaining({ layerId: 'root_0', target: 'component:Button' }),
      expect.objectContaining({ layerId: 'root_0_1', role: 'bttxt', target: 'text:native' }),
    ]));
    expect(report.aiSuggestions).toEqual([]);
    expect(markdown).toContain('# FairyGUI 导出报告');
    expect(markdown).toContain('## 图层映射');
    expect(markdown).toContain('## AI 建议');
  });

  test('writes AI suggestions as structured report entries without changing the mapping target', async () => {
    const components = makeComponents();
    components.set('root_0_1', { type: 'Text', confidence: 0.82, source: 'ai', needsReview: true });

    await new FairyGuiExporter().export(makeDocument('/art/ai-report.psd'), components, {
      projectPath, packageName: 'PSDImport', componentName: 'AiReport', pageId: 'ai-report',
    });
    const report = JSON.parse(await readFile(join(projectPath, '.psd-exporter/reports/ai-report.json'), 'utf8'));

    expect(report.aiSuggestions).toEqual([
      { layerId: 'root_0_1', suggestedType: 'Text', confidence: 0.82, needsReview: true },
    ]);
    expect(report.mappings).toEqual(expect.arrayContaining([
      expect.objectContaining({ layerId: 'root_0_1', target: 'text:native' }),
    ]));
  });

  test('preserves PSD top-to-bottom layer order in the FairyGUI display list', async () => {
    const background: Layer = {
      id: 'background', sourceId: 40, name: 'Background.img', type: 'image',
      bounds: { x: 0, y: 0, width: 100, height: 100 }, visible: true, opacity: 1,
    };
    const foreground: Layer = {
      id: 'foreground', sourceId: 41, name: 'Foreground.img', type: 'image',
      bounds: { x: 20, y: 20, width: 40, height: 40 }, visible: true, opacity: 1,
    };
    const document: ParsedPsdDocument = {
      tree: {
        metadata: {
          psdPath: '/art/layer-order.psd',
          canvasSize: { x: 0, y: 0, width: 100, height: 100 },
          timestamp: '2026-08-20T00:00:00.000Z',
        },
        root: {
          id: 'root', name: 'canvas', type: 'group',
          bounds: { x: 0, y: 0, width: 100, height: 100 }, visible: true, opacity: 1,
          children: [background, foreground],
        },
      },
      rasterSources: new Map([
        ['background', { layerId: 'background', photoshopLayerId: 40, width: 100, height: 100, rgba: imageRgba(100, 100) }],
        ['foreground', { layerId: 'foreground', photoshopLayerId: 41, width: 40, height: 40, rgba: imageRgba(40, 40) }],
      ]),
    };

    const result = await new FairyGuiExporter().export(document, new Map(), {
      projectPath, packageName: 'PSDImport', componentName: 'LayerOrder', pageId: 'layer-order',
    });
    const rootXml = await readFile(join(projectPath, result.rootComponentPath), 'utf8');

    expect(rootXml.indexOf('name="Background"')).toBeLessThan(rootXml.indexOf('name="Foreground"'));
  });

  test('re-export is stable and another managed page is preserved', async () => {
    const exporter = new FairyGuiExporter();
    const first = await exporter.export(makeDocument('/art/menu.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'menu',
    });
    const shop = await exporter.export(makeDocument('/art/shop.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Shop', pageId: 'shop',
    });
    const shopManifestPath = join(projectPath, shop.manifestPath);
    const shopManifestBefore = await readFile(shopManifestPath, 'utf8');
    const shopManifest = JSON.parse(shopManifestBefore);
    const shopFilesBefore = await Promise.all(
      shopManifest.files.map((file: string) => readFile(join(projectPath, file))),
    );
    const packageBefore = await readFile(join(projectPath, 'assets/PSDImport/package.xml'), 'utf8');
    const second = await exporter.export(makeDocument('/art/menu.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'menu',
    });

    expect(second.rootComponentId).toBe(first.rootComponentId);
    const packageXml = await readFile(join(projectPath, 'assets/PSDImport/package.xml'), 'utf8');
    const shopFilesAfter = await Promise.all(
      shopManifest.files.map((file: string) => readFile(join(projectPath, file))),
    );
    expect(packageXml).toContain('name="Menu.xml"');
    expect(packageXml).toContain('name="Shop.xml"');
    expect(packageXml).toBe(packageBefore);
    expect(await readFile(shopManifestPath, 'utf8')).toBe(shopManifestBefore);
    expect(shopFilesAfter.map((file, index) => file.equals(shopFilesBefore[index]))).toEqual(
      shopFilesBefore.map(() => true),
    );
  });

  test('refuses to reuse a managed page id for a different PSD without changing the existing page', async () => {
    const exporter = new FairyGuiExporter();
    const first = await exporter.export(makeDocument('/art/menu.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'shared-page',
    });
    const packagePath = join(projectPath, 'assets/PSDImport/package.xml');
    const manifestPath = join(projectPath, first.manifestPath);
    const packageBefore = await readFile(packagePath, 'utf8');
    const manifestBefore = await readFile(manifestPath, 'utf8');

    await expect(exporter.export(makeDocument('/art/shop.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Shop', pageId: 'shared-page',
    })).rejects.toThrow('already belongs to a different PSD');

    expect(await readFile(packagePath, 'utf8')).toBe(packageBefore);
    expect(await readFile(manifestPath, 'utf8')).toBe(manifestBefore);
  });

  test('does not treat an ambiguous legacy relative source path as the same PSD', async () => {
    const exporter = new FairyGuiExporter();
    const first = await exporter.export(makeDocument('/repo/art/menu.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'legacy-page', sourceRoot: '/repo',
    });
    const manifestPath = join(projectPath, first.manifestPath);
    const manifest = JSON.parse(await readFile(manifestPath, 'utf8')) as { sourcePath: string; sourceRoot?: string };
    manifest.sourcePath = 'art/menu.psd';
    manifest.sourceRoot = '/repo';
    await writeFile(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);

    await expect(exporter.export(makeDocument('/other/art/menu.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Other', pageId: 'legacy-page',
    })).rejects.toThrow('already belongs to a different PSD');
  });

  test('migrates a legacy relative source path when sourceRoot resolves it uniquely', async () => {
    const exporter = new FairyGuiExporter();
    const first = await exporter.export(makeDocument('/repo/art/menu.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'legacy-migration', sourceRoot: '/repo',
    });
    const manifestPath = join(projectPath, first.manifestPath);
    const manifest = JSON.parse(await readFile(manifestPath, 'utf8')) as { sourcePath: string; sourceRoot?: string };
    manifest.sourcePath = 'art/menu.psd';
    manifest.sourceRoot = '/repo';
    await writeFile(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);

    await exporter.export(makeDocument('/repo/art/menu.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'legacy-migration', sourceRoot: '/repo',
    });
    const migrated = JSON.parse(await readFile(manifestPath, 'utf8')) as { sourcePath: string };
    expect(migrated.sourcePath).toBe('/repo/art/menu.psd');
  });

  test('keeps resource and node ids stable when parser layer ids change without Photoshop source ids', async () => {
    const makeFallbackDocument = (layerId: string, extraLayer?: Layer): ParsedPsdDocument => ({
      tree: {
        metadata: {
          psdPath: '/art/fallback.psd',
          canvasSize: { x: 0, y: 0, width: 200, height: 100 },
          timestamp: '2026-08-20T00:00:00.000Z',
        },
        root: {
          id: 'root', name: 'canvas', type: 'group',
          bounds: { x: 0, y: 0, width: 200, height: 100 }, visible: true, opacity: 1,
          children: [
            ...(extraLayer ? [extraLayer] : []),
            { id: layerId, name: 'Logo.img', type: 'image', bounds: { x: 40, y: 20, width: 80, height: 40 }, visible: true, opacity: 1 },
          ],
        },
      },
      rasterSources: new Map([
        [layerId, { layerId, width: 80, height: 40, rgba: imageRgba(80, 40) }],
        ...(extraLayer ? [[extraLayer.id, { layerId: extraLayer.id, width: 20, height: 20, rgba: imageRgba(20, 20) }] as const] : []),
      ]),
    });
    const exporter = new FairyGuiExporter();
    const first = await exporter.export(makeFallbackDocument('root_0'), new Map(), {
      projectPath, packageName: 'PSDImport', componentName: 'Fallback', pageId: 'fallback',
    });
    const firstManifest = JSON.parse(await readFile(join(projectPath, first.manifestPath), 'utf8'));
    const firstImage = firstManifest.resources.find((resource: { type: string; name: string }) => resource.type === 'image' && resource.name.startsWith('Logo_'));
    const firstRootXml = await readFile(join(projectPath, first.rootComponentPath), 'utf8');
    const firstNodeId = /<image id="([^"]+)" name="Logo"/.exec(firstRootXml)?.[1];

    const extraLayer: Layer = {
      id: 'root_0', name: 'Badge.img', type: 'image',
      bounds: { x: 5, y: 5, width: 20, height: 20 }, visible: true, opacity: 1,
    };
    const second = await exporter.export(makeFallbackDocument('root_1', extraLayer), new Map(), {
      projectPath, packageName: 'PSDImport', componentName: 'Fallback', pageId: 'fallback',
    });
    const secondManifest = JSON.parse(await readFile(join(projectPath, second.manifestPath), 'utf8'));
    const secondImage = secondManifest.resources.find((resource: { type: string; name: string }) => resource.type === 'image' && resource.name.startsWith('Logo_'));
    const secondRootXml = await readFile(join(projectPath, second.rootComponentPath), 'utf8');
    const secondNodeId = /<image id="([^"]+)" name="Logo"/.exec(secondRootXml)?.[1];

    expect(secondImage.id).toBe(firstImage.id);
    expect(secondImage.name).toBe(firstImage.name);
    expect(secondNodeId).toBe(firstNodeId);
    expect(second.diagnostics).toEqual(expect.arrayContaining([
      expect.objectContaining({ code: 'SOURCE_ID_FALLBACK', layerId: 'root_1' }),
    ]));
  });

  test('keeps fallback identities distinct for identical leaves under different ancestor groups', async () => {
    const document = makeDocument('/art/fallback-ancestors.psd');
    document.tree.root.children = ['Left', 'Right'].map((name, index): Layer => ({
      id: `group-${index}`, name, type: 'group',
      bounds: { x: index * 100, y: 0, width: 80, height: 40 }, visible: true, opacity: 1,
      children: [{
        id: `icon-${index}`, name: 'Icon.img', type: 'image',
        bounds: { x: 0, y: 0, width: 20, height: 20 }, visible: true, opacity: 1,
      }],
    }));
    document.rasterSources = new Map([
      ['icon-0', { layerId: 'icon-0', width: 20, height: 20, rgba: imageRgba(20, 20) }],
      ['icon-1', { layerId: 'icon-1', width: 20, height: 20, rgba: imageRgba(20, 20) }],
    ]);

    const result = await new FairyGuiExporter().export(document, new Map(), {
      projectPath, packageName: 'PSDImport', componentName: 'Ancestors', pageId: 'fallback-ancestors',
    });
    const manifest = JSON.parse(await readFile(join(projectPath, result.manifestPath), 'utf8'));
    const imageIds = manifest.resources
      .filter((resource: { type: string }) => resource.type === 'image')
      .map((resource: { id: string }) => resource.id);

    expect(new Set(imageIds).size).toBe(2);
  });

  test('keeps fallback identities distinct for identical sibling leaves', async () => {
    const document = makeDocument('/art/fallback-siblings.psd');
    document.tree.root.children = [0, 1].map(index => ({
      id: `icon-${index}`, name: 'Icon.img', type: 'image' as const,
      bounds: { x: 10, y: 10, width: 20, height: 20 }, visible: true, opacity: 1,
    }));
    document.rasterSources = new Map([
      ['icon-0', { layerId: 'icon-0', width: 20, height: 20, rgba: imageRgba(20, 20) }],
      ['icon-1', { layerId: 'icon-1', width: 20, height: 20, rgba: imageRgba(20, 20) }],
    ]);

    const result = await new FairyGuiExporter().export(document, new Map(), {
      projectPath, packageName: 'PSDImport', componentName: 'SiblingFallback', pageId: 'fallback-siblings',
    });
    const manifest = JSON.parse(await readFile(join(projectPath, result.manifestPath), 'utf8'));
    const imageIds = manifest.resources
      .filter((resource: { type: string }) => resource.type === 'image')
      .map((resource: { id: string }) => resource.id);

    expect(new Set(imageIds).size).toBe(2);
  });

  test('binds ordinary button children to the up page when no state roles exist', async () => {
    const document = makeDocument('/art/plain-button.psd');
    document.tree.root.children = [{
      id: 'button', sourceId: 50, name: 'Plain.bt', type: 'group',
      bounds: { x: 0, y: 0, width: 100, height: 40 }, visible: true, opacity: 1,
      children: [{
        id: 'visual', sourceId: 51, name: 'Visual.img', type: 'image',
        bounds: { x: 0, y: 0, width: 100, height: 40 }, visible: true, opacity: 1,
      }, {
        id: 'title', sourceId: 52, name: 'Title.bttxt', type: 'text',
        bounds: { x: 10, y: 5, width: 80, height: 20 }, visible: true, opacity: 1, text: 'Start',
        textStyles: { fontSize: 14, color: { r: 1, g: 1, b: 1, a: 1 }, fontName: 'Arial', fontStyle: { bold: false, italic: false }, alignment: { horizontal: 'center', vertical: 'middle' } },
      }],
    }];
    document.rasterSources = new Map([
      ['visual', { layerId: 'visual', photoshopLayerId: 51, width: 100, height: 40, rgba: imageRgba(100, 40) }],
    ]);
    const components = new Map<string, ComponentInfo>([
      ['button', { type: 'Button', confidence: 1, source: 'tag', needsReview: false }],
      ['title', { type: 'Unknown', role: 'bttxt', confidence: 1, source: 'tag', needsReview: false }],
    ]);

    const result = await new FairyGuiExporter().export(document, components, {
      projectPath, packageName: 'PSDImport', componentName: 'PlainButton', pageId: 'plain-button',
    });
    const buttonXml = await readFile(join(projectPath, result.componentPaths.Plain), 'utf8');

    expect(buttonXml.match(/<gearDisplay controller="button" pages="0"\/>/g)).toHaveLength(2);
  });

  test('infers a vertical slider from fill geometry when the container is square', async () => {
    const document = makeDocument('/art/vertical-slider.psd');
    document.tree.root.children = [{
      id: 'slider', sourceId: 60, name: 'Volume.sld', type: 'group',
      bounds: { x: 0, y: 0, width: 100, height: 100 }, visible: true, opacity: 1,
      children: [{
        id: 'fill', sourceId: 61, name: 'Fill.fill', type: 'image',
        bounds: { x: 45, y: 10, width: 10, height: 70 }, visible: true, opacity: 1,
      }, {
        id: 'handle', sourceId: 62, name: 'Handle.handle', type: 'image',
        bounds: { x: 40, y: 70, width: 20, height: 20 }, visible: true, opacity: 1,
      }],
    }];
    document.rasterSources = new Map([
      ['fill', { layerId: 'fill', photoshopLayerId: 61, width: 10, height: 70, rgba: imageRgba(10, 70) }],
      ['handle', { layerId: 'handle', photoshopLayerId: 62, width: 20, height: 20, rgba: imageRgba(20, 20) }],
    ]);
    const components = new Map<string, ComponentInfo>([
      ['slider', { type: 'Slider', confidence: 1, source: 'tag', needsReview: false }],
      ['fill', { type: 'Unknown', role: 'fill', confidence: 1, source: 'tag', needsReview: false }],
      ['handle', { type: 'Unknown', role: 'handle', confidence: 1, source: 'tag', needsReview: false }],
    ]);

    const result = await new FairyGuiExporter().export(document, components, {
      projectPath, packageName: 'PSDImport', componentName: 'VerticalSlider', pageId: 'vertical-slider',
    });
    const sliderXml = await readFile(join(projectPath, result.componentPaths.Volume), 'utf8');

    expect(sliderXml).toContain('name="bar_v"');
    expect(result.diagnostics).not.toEqual(expect.arrayContaining([
      expect.objectContaining({ code: 'SLIDER_DIRECTION_DEFAULTED' }),
    ]));
  });

  test('reports complex masks that cannot be rasterized instead of silently clipping them', async () => {
    const document = makeDocument('/art/complex-mask.psd');
    document.tree.root.children = [{
      id: 'mask', sourceId: 70, name: 'Portrait.msk', type: 'group', maskType: 'vector',
      bounds: { x: 0, y: 0, width: 100, height: 100 }, visible: true, opacity: 1,
      children: [{
        id: 'portrait', sourceId: 71, name: 'Portrait.img', type: 'image', blendMode: 'multiply', clipping: true,
        bounds: { x: 0, y: 0, width: 100, height: 100 }, visible: true, opacity: 1,
      }],
    }];
    document.rasterSources = new Map([
      ['portrait', { layerId: 'portrait', photoshopLayerId: 71, width: 100, height: 100, rgba: imageRgba(100, 100) }],
    ]);
    const components = new Map<string, ComponentInfo>([
      ['mask', { type: 'Mask', confidence: 1, source: 'tag', needsReview: false }],
    ]);

    const result = await new FairyGuiExporter().export(document, components, {
      projectPath, packageName: 'PSDImport', componentName: 'ComplexMask', pageId: 'complex-mask',
    });

    expect(result.diagnostics).toEqual(expect.arrayContaining([
      expect.objectContaining({ code: 'MASK_DEGRADED', layerId: 'mask' }),
    ]));
  });

  test('reports complex semantics on ordinary image and group layers', async () => {
    const document = makeDocument('/art/ordinary-complex.psd');
    document.tree.root.children = [{
      id: 'group', sourceId: 72, name: 'OrdinaryGroup', type: 'group', clipping: true,
      bounds: { x: 0, y: 0, width: 100, height: 100 }, visible: true, opacity: 1,
      children: [{
        id: 'image', sourceId: 73, name: 'OrdinaryImage.img', type: 'image', maskType: 'vector', blendMode: 'multiply',
        bounds: { x: 0, y: 0, width: 100, height: 100 }, visible: true, opacity: 1,
      }],
    }];
    document.rasterSources = new Map([
      ['image', { layerId: 'image', photoshopLayerId: 73, width: 100, height: 100, rgba: imageRgba(100, 100) }],
    ]);

    const result = await new FairyGuiExporter().export(document, new Map(), {
      projectPath, packageName: 'PSDImport', componentName: 'OrdinaryComplex', pageId: 'ordinary-complex',
    });

    expect(result.diagnostics).toEqual(expect.arrayContaining([
      expect.objectContaining({ code: 'MASK_DEGRADED', layerId: 'group' }),
      expect.objectContaining({ code: 'MASK_DEGRADED', layerId: 'image' }),
    ]));
  });

  test('skips ordinary hidden layers while preserving hidden component role layers', async () => {
    const document = makeDocument('/art/hidden-states.psd');
    document.tree.root.children = [{
      id: 'hidden-normal', sourceId: 80, name: 'Hidden.img', type: 'image',
      bounds: { x: 0, y: 0, width: 20, height: 20 }, visible: false, opacity: 1,
    }, {
      id: 'button', sourceId: 81, name: 'Stateful.bt', type: 'group',
      bounds: { x: 20, y: 20, width: 100, height: 40 }, visible: true, opacity: 1,
      children: [{
        id: 'up', sourceId: 82, name: 'Up.bg', type: 'image',
        bounds: { x: 20, y: 20, width: 100, height: 40 }, visible: true, opacity: 1,
      }, {
        id: 'pressed', sourceId: 83, name: 'Pressed.press', type: 'image',
        bounds: { x: 20, y: 20, width: 100, height: 40 }, visible: false, opacity: 1,
      }],
    }];
    document.rasterSources = new Map([
      ['hidden-normal', { layerId: 'hidden-normal', photoshopLayerId: 80, width: 20, height: 20, rgba: imageRgba(20, 20) }],
      ['up', { layerId: 'up', photoshopLayerId: 82, width: 100, height: 40, rgba: imageRgba(100, 40) }],
      ['pressed', { layerId: 'pressed', photoshopLayerId: 83, width: 100, height: 40, rgba: imageRgba(100, 40) }],
    ]);
    const components = new Map<string, ComponentInfo>([
      ['button', { type: 'Button', confidence: 1, source: 'tag', needsReview: false }],
      ['up', { type: 'Unknown', role: 'bg', confidence: 1, source: 'tag', needsReview: false }],
      ['pressed', { type: 'Unknown', role: 'press', confidence: 1, source: 'tag', needsReview: false }],
    ]);

    const result = await new FairyGuiExporter().export(document, components, {
      projectPath, packageName: 'PSDImport', componentName: 'HiddenStates', pageId: 'hidden-states',
    });
    const rootXml = await readFile(join(projectPath, result.rootComponentPath), 'utf8');
    const buttonXml = await readFile(join(projectPath, result.componentPaths.Stateful), 'utf8');

    expect(rootXml).not.toContain('name="Hidden"');
    expect(buttonXml).toContain('name="Pressed"');
    expect(buttonXml).toContain('<gearDisplay controller="button" pages="1"/>');
  });

  test('preserves a button state subtree carried by a group layer', async () => {
    const document = makeDocument('/art/group-state.psd');
    document.tree.root.children = [{
      id: 'button', sourceId: 90, name: 'Grouped.bt', type: 'group',
      bounds: { x: 0, y: 0, width: 100, height: 40 }, visible: true, opacity: 1,
      children: [{
        id: 'pressed', sourceId: 91, name: 'Pressed.press', type: 'group',
        bounds: { x: 0, y: 0, width: 100, height: 40 }, visible: false, opacity: 1,
        children: [{
          id: 'pressed-image', sourceId: 92, name: 'PressedImage.img', type: 'image',
          bounds: { x: 0, y: 0, width: 100, height: 40 }, visible: true, opacity: 1,
        }],
      }],
    }];
    document.rasterSources = new Map([
      ['pressed-image', { layerId: 'pressed-image', photoshopLayerId: 92, width: 100, height: 40, rgba: imageRgba(100, 40) }],
    ]);
    const components = new Map<string, ComponentInfo>([
      ['button', { type: 'Button', confidence: 1, source: 'tag', needsReview: false }],
      ['pressed', { type: 'Unknown', role: 'press', confidence: 1, source: 'tag', needsReview: false }],
    ]);

    const result = await new FairyGuiExporter().export(document, components, {
      projectPath, packageName: 'PSDImport', componentName: 'GroupedState', pageId: 'group-state',
    });
    const buttonXml = await readFile(join(projectPath, result.componentPaths.Grouped), 'utf8');
    const manifest = JSON.parse(await readFile(join(projectPath, result.manifestPath), 'utf8'));
    const stateResource = manifest.resources.find((resource: { type: string; name: string }) => resource.type === 'component' && resource.name.includes('Pressed'));
    const stateXml = await readFile(join(projectPath, stateResource.filePath), 'utf8');

    expect(buttonXml).toContain('<gearDisplay controller="button" pages="1"/>');
    expect(stateXml).toContain('name="PressedImage"');
  });

  test('refuses to adopt an existing manual root component by default', async () => {
    await mkdir(join(projectPath, 'assets/PSDImport'), { recursive: true });
    await writeFile(join(projectPath, 'PSDImport.fairy'), '<?xml version="1.0"?><projectDescription id="p" type="Unity" version="3.0"/>');
    await writeFile(join(projectPath, 'assets/PSDImport/package.xml'), `<?xml version="1.0"?>
<packageDescription id="pkg"><resources><component id="manual" name="Menu.xml" path="/" exported="true"/></resources></packageDescription>`);

    await expect(new FairyGuiExporter().export(makeDocument('/art/menu.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'menu',
    })).rejects.toThrow('already exists and is not managed');
  });

  test('adopts an existing manual root component without changing its resource id', async () => {
    await mkdir(join(projectPath, 'assets/PSDImport'), { recursive: true });
    await writeFile(join(projectPath, 'PSDImport.fairy'), '<?xml version="1.0"?><projectDescription id="p" type="Unity" version="3.0"/>');
    await writeFile(join(projectPath, 'assets/PSDImport/package.xml'), `<?xml version="1.0"?>
<packageDescription id="pkg"><resources><component id="manual01" name="Menu.xml" path="/" exported="true"/></resources></packageDescription>`);
    await writeFile(join(projectPath, 'assets/PSDImport/Menu.xml'), '<?xml version="1.0"?><component size="1,1"><displayList/></component>');

    const result = await new FairyGuiExporter().export(makeDocument('/art/menu.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'menu', adoptExisting: true,
    });

    expect(result.rootComponentId).toBe('manual01');
    const packageXml = await readFile(join(projectPath, 'assets/PSDImport/package.xml'), 'utf8');
    expect(packageXml.match(/id="manual01"/g)).toHaveLength(1);
    expect(packageXml).not.toContain('path="/"');
    await expect(access(join(projectPath, 'assets/PSDImport/Menu.xml'))).resolves.toBeUndefined();
  });

  test('puts scale metadata on the package image resource', async () => {
    const document = makeDocument('/art/menu.psd');
    document.tree.root.children![0].children![0].name = 'StartBg.img.sliced.s9-10-8-10-8';
    const components = makeComponents();
    components.set('root_0_0', {
      type: 'Image', imageType: 'sliced', confidence: 1, source: 'tag', needsReview: false,
    });

    await new FairyGuiExporter().export(document, components, {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'menu',
    });

    const packageXml = await readFile(join(projectPath, 'assets/PSDImport/package.xml'), 'utf8');
    expect(packageXml).toContain('scale="9grid"');
    expect(packageXml).toContain('scale9grid="10,8,80,24"');
  });

  test('changes generation when raster bytes or output options change', async () => {
    const exporter = new FairyGuiExporter();
    const document = makeDocument('/art/menu.psd');
    const first = await exporter.export(document, makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'menu',
    });
    const firstManifest = JSON.parse(await readFile(join(projectPath, first.manifestPath), 'utf8'));

    document.rasterSources.get('root_0_0')!.rgba[0] = 42;
    await exporter.export(document, makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'menu',
      defaultScale9: { left: 5, top: 5, right: 5, bottom: 5 },
    });
    const secondManifest = JSON.parse(await readFile(join(projectPath, first.manifestPath), 'utf8'));

    expect(secondManifest.generation).not.toBe(firstManifest.generation);
  });

  test('resolves configured local and external references', async () => {
    await mkdir(join(projectPath, 'assets/PSDImport'), { recursive: true });
    await mkdir(join(projectPath, 'assets/PSDImport/manual'), { recursive: true });
    await writeFile(join(projectPath, 'PSDImport.fairy'), '<?xml version="1.0"?><projectDescription id="p" type="Unity" version="3.0"/>');
    await writeFile(join(projectPath, 'assets/PSDImport/package.xml'), `<?xml version="1.0"?>
<packageDescription id="pkg00001"><resources><component id="shared01" name="SharedButton.xml" path="/manual/"/></resources></packageDescription>`);
    await writeFile(join(projectPath, 'assets/PSDImport/manual/SharedButton.xml'), '<component size="100,40"><displayList/></component>');

    const result = await new FairyGuiExporter().export(makeReferenceDocument('/art/references.psd'), new Map(), {
      projectPath,
      packageName: 'PSDImport',
      componentName: 'References',
      pageId: 'references',
      localReferences: { SharedButton: { resourceId: 'shared01' } },
      externalReferences: { CommonClose: { packageId: 'common01', resourceId: 'close001' } },
    });

    const rootXml = await readFile(join(projectPath, result.rootComponentPath), 'utf8');
    expect(rootXml).toContain('src="shared01"');
    expect(rootXml).toContain('src="close001" pkg="common01"');
  });

  test('blocks unresolved references without changing the package commit point', async () => {
    const exporter = new FairyGuiExporter();
    await exporter.export(makeDocument('/art/menu.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'menu',
    });
    const packagePath = join(projectPath, 'assets/PSDImport/package.xml');
    const before = await readFile(packagePath, 'utf8');

    await expect(exporter.export(makeReferenceDocument('/art/references.psd'), new Map(), {
      projectPath, packageName: 'PSDImport', componentName: 'References', pageId: 'references',
    })).rejects.toThrow('UNRESOLVED_REFERENCE');

    expect(await readFile(packagePath, 'utf8')).toBe(before);
    await expect(access(join(projectPath, '.psd-exporter/pages/references.json'))).rejects.toThrow();
  });

  test('falls back to local raster content when a reference is not configured', async () => {
    const document = makeReferenceDocument('/art/references.psd');
    document.rasterSources.set('root_0', {
      layerId: 'root_0', width: 100, height: 40, rgba: imageRgba(100, 40),
    });
    document.rasterSources.set('root_1', {
      layerId: 'root_1', width: 40, height: 40, rgba: imageRgba(40, 40),
    });

    const result = await new FairyGuiExporter().export(document, new Map(), {
      projectPath, packageName: 'PSDImport', componentName: 'References', pageId: 'references',
    });

    expect(result.diagnostics).toContainEqual(expect.objectContaining({ code: 'REFERENCE_FALLBACK', layerId: 'root_0' }));
    expect(result.diagnostics).toContainEqual(expect.objectContaining({ code: 'REFERENCE_FALLBACK', layerId: 'root_1' }));
  });

  test('refuses to export while another FairyGUI transaction lock is active', async () => {
    await mkdir(join(projectPath, '.psd-exporter'), { recursive: true });
    await writeFile(join(projectPath, '.psd-exporter/export.lock'), JSON.stringify({ pid: process.pid, startedAt: new Date().toISOString() }));

    await expect(new FairyGuiExporter().export(makeDocument('/art/menu.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'menu',
    })).rejects.toThrow('locked by another export');
  });

  test('cleans stale pre-commit transaction files before exporting', async () => {
    const staleStaging = join(projectPath, '.psd-exporter/staging/stale');
    const transactionDir = join(projectPath, '.psd-exporter/transactions');
    await mkdir(staleStaging, { recursive: true });
    await mkdir(transactionDir, { recursive: true });
    await writeFile(join(staleStaging, 'partial.txt'), 'partial');
    await writeFile(join(transactionDir, 'stale.json'), JSON.stringify({
      status: 'staged',
      stagingDir: '.psd-exporter/staging/stale',
    }));

    await new FairyGuiExporter().export(makeDocument('/art/menu.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'menu',
    });

    await expect(access(staleStaging)).rejects.toThrow();
    await expect(access(join(transactionDir, 'stale.json'))).rejects.toThrow();
  });

  test('recovers a manifest after the package commit point', async () => {
    const componentContent = '<component size="100,100"><displayList/></component>';
    const componentHash = createHash('sha256').update(componentContent).digest('hex');
    const packageXml = '<?xml version="1.0" encoding="utf-8"?><packageDescription id="package1"><resources><component id="root0001" name="Recovered.xml" path="/psd/recovered/generation1/" exported="true"/></resources></packageDescription>';
    await mkdir(join(projectPath, 'assets/PSDImport/psd/recovered/generation1'), { recursive: true });
    await writeFile(join(projectPath, 'assets/PSDImport/package.xml'), packageXml);
    await writeFile(join(projectPath, 'assets/PSDImport/psd/recovered/generation1/Recovered.xml'), componentContent);
    const transactionDir = join(projectPath, '.psd-exporter/transactions');
    await mkdir(transactionDir, { recursive: true });
    await writeFile(join(transactionDir, 'committed.json'), JSON.stringify({
      status: 'package-committed',
      packageXmlPath: 'assets/PSDImport/package.xml',
      packageHash: createHash('sha256').update(packageXml).digest('hex'),
      manifest: {
        schemaVersion: 1,
        pageId: 'recovered',
        packageName: 'PSDImport',
        packageId: 'package1',
        componentName: 'Recovered',
        rootComponentId: 'root0001',
        generation: 'generation1',
        sourcePath: '/art/recovered.psd',
        resources: [{
          id: 'root0001',
          type: 'component',
          name: 'Recovered.xml',
          packagePath: '/psd/recovered/generation1/',
          filePath: 'assets/PSDImport/psd/recovered/generation1/Recovered.xml',
          contentHash: componentHash,
        }],
        files: ['assets/PSDImport/psd/recovered/generation1/Recovered.xml'],
        generatedAt: '2026-08-20T00:00:00.000Z',
        generatorVersion: '1.0.0',
      },
    }));

    await new FairyGuiExporter().export(makeDocument('/art/menu.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'menu',
    });

    expect(await readFile(join(projectPath, '.psd-exporter/pages/recovered.json'), 'utf8')).toContain('"pageId": "recovered"');
    await expect(access(join(transactionDir, 'committed.json'))).rejects.toThrow();
  });

  test('resolves a unique managed component in the current page before config references', async () => {
    const document = makeDocument('/art/menu.psd');
    document.tree.root.children!.push({
      id: 'root_1', sourceId: 30, name: 'ref Start', type: 'shape',
      bounds: { x: 180, y: 30, width: 100, height: 40 }, visible: true, opacity: 1,
    });

    const result = await new FairyGuiExporter().export(document, makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'menu',
    });

    const rootXml = await readFile(join(projectPath, result.rootComponentPath), 'utf8');
    const manifest = JSON.parse(await readFile(join(projectPath, result.manifestPath), 'utf8'));
    const startComponent = manifest.resources.find((resource: { name: string }) => resource.name.startsWith('Start_'));
    expect(rootXml).toContain(`src="${startComponent.id}"`);
  });

  test('rejects an automatically discovered component reference when its XML is missing', async () => {
    await mkdir(join(projectPath, 'assets/PSDImport'), { recursive: true });
    await writeFile(join(projectPath, 'assets/PSDImport/package.xml'), `<?xml version="1.0"?><packageDescription id="pkg00001"><resources><component id="shared01" name="SharedButton.xml" path="/manual/"/></resources></packageDescription>`);
    const document = makeReferenceDocument('/art/missing-reference.psd');
    document.tree.root.children = document.tree.root.children?.slice(0, 1);
    await expect(new FairyGuiExporter().export(document, new Map(), {
      projectPath, packageName: 'PSDImport', componentName: 'MissingReference', pageId: 'missing-reference',
    })).rejects.toThrow('MISSING_RESOURCE_FILE');
  });

  test('changes generation when a referenced manual component changes', async () => {
    await mkdir(join(projectPath, 'assets/PSDImport/manual'), { recursive: true });
    await writeFile(join(projectPath, 'assets/PSDImport/package.xml'), `<?xml version="1.0"?><packageDescription id="pkg00001"><resources><component id="shared01" name="SharedButton.xml" path="/manual/"/></resources></packageDescription>`);
    await writeFile(join(projectPath, 'assets/PSDImport/manual/SharedButton.xml'), '<component size="100,40"><displayList/></component>');
    const exporter = new FairyGuiExporter();
    const document = makeReferenceDocument('/art/reference-generation.psd');
    document.tree.root.children = document.tree.root.children?.slice(0, 1);
    const first = await exporter.export(document, new Map(), {
      projectPath, packageName: 'PSDImport', componentName: 'References', pageId: 'reference-generation',
    });
    const firstManifest = JSON.parse(await readFile(join(projectPath, first.manifestPath), 'utf8'));
    await writeFile(join(projectPath, 'assets/PSDImport/manual/SharedButton.xml'), '<component size="120,40"><displayList/></component>');
    const second = await exporter.export(document, new Map(), {
      projectPath, packageName: 'PSDImport', componentName: 'References', pageId: 'reference-generation',
    });
    const secondManifest = JSON.parse(await readFile(join(projectPath, second.manifestPath), 'utf8'));
    expect(secondManifest.generation).not.toBe(firstManifest.generation);

    const packagePath = join(projectPath, 'assets/PSDImport/package.xml');
    const packageXml = await readFile(packagePath, 'utf8');
    await writeFile(packagePath, packageXml.replace('id="shared01"', 'id="shared02"'));
    const third = await exporter.export(document, new Map(), {
      projectPath, packageName: 'PSDImport', componentName: 'References', pageId: 'reference-generation',
    });
    const thirdManifest = JSON.parse(await readFile(join(projectPath, third.manifestPath), 'utf8'));
    const rootXml = await readFile(join(projectPath, third.rootComponentPath), 'utf8');
    expect(thirdManifest.generation).not.toBe(secondManifest.generation);
    expect(rootXml).toContain('src="shared02"');
  });

  test('rejects a tampered manifest before it can remove a manual package resource', async () => {
    const exporter = new FairyGuiExporter();
    const first = await exporter.export(makeDocument('/art/tampered.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'tampered',
    });
    const packagePath = join(projectPath, 'assets/PSDImport/package.xml');
    const packageBefore = await readFile(packagePath, 'utf8');
    const manifestPath = join(projectPath, first.manifestPath);
    const manifest = JSON.parse(await readFile(manifestPath, 'utf8')) as { rootComponentId: string; resources: Array<{ id: string }> };
    await writeFile(join(projectPath, 'assets/PSDImport/manual.xml'), '<component/>');
    await writeFile(packagePath, packageBefore.replace('</resources>', '<component id="manual01" name="manual.xml" path="/"/></resources>'));
    const nonRootResource = manifest.resources.find(resource => resource.id !== manifest.rootComponentId);
    if (!nonRootResource) throw new Error('Expected a non-root managed resource');
    nonRootResource.id = 'manual01';
    await writeFile(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);

    await expect(exporter.export(makeDocument('/art/tampered.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'tampered',
    })).rejects.toThrow('does not match package.xml');
    expect(await readFile(packagePath, 'utf8')).toContain('id="manual01"');
  });

  test('rejects a tampered manifest root id before reserving a manual resource id', async () => {
    const exporter = new FairyGuiExporter();
    const first = await exporter.export(makeDocument('/art/tampered-root.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'tampered-root',
    });
    const packagePath = join(projectPath, 'assets/PSDImport/package.xml');
    const packageBefore = await readFile(packagePath, 'utf8');
    const manifestPath = join(projectPath, first.manifestPath);
    const manifest = JSON.parse(await readFile(manifestPath, 'utf8')) as { rootComponentId: string };
    await writeFile(join(projectPath, 'assets/PSDImport/manual.xml'), '<component/>');
    await writeFile(packagePath, packageBefore.replace('</resources>', '<component id="manual01" name="manual.xml" path="/"/></resources>'));
    manifest.rootComponentId = 'manual01';
    await writeFile(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);

    await expect(exporter.export(makeDocument('/art/tampered-root.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'tampered-root',
    })).rejects.toThrow('root component does not match managed resources');
    expect(await readFile(packagePath, 'utf8')).toContain('id="manual01"');
  });

  test('returns a committed result when report writing fails after the package commit point', async () => {
    const reportFailure = jest.spyOn(reportWriter, 'writeReportFiles').mockRejectedValueOnce(new Error('report disk failure'));
    try {
      const result = await new FairyGuiExporter().export(makeDocument('/art/report-failure.psd'), makeComponents(), {
        projectPath, packageName: 'PSDImport', componentName: 'ReportFailure', pageId: 'report-failure',
      });
      expect(result.reportStatus).toBe('failed');
      expect(result.reportError).toContain('report disk failure');
      expect(result.diagnostics).toEqual(expect.arrayContaining([
        expect.objectContaining({ code: 'REPORT_WRITE_FAILED' }),
      ]));
      await expect(access(join(projectPath, result.manifestPath))).resolves.toBeUndefined();
      expect(await readFile(join(projectPath, 'assets/PSDImport/package.xml'), 'utf8')).toContain(`id="${result.rootComponentId}"`);
    } finally {
      reportFailure.mockRestore();
    }
  });

  test('resolves a unique existing package component by logical name', async () => {
    await mkdir(join(projectPath, 'assets/PSDImport/shared'), { recursive: true });
    await writeFile(join(projectPath, 'PSDImport.fairy'), '<?xml version="1.0"?><projectDescription id="p" type="Unity" version="3.0"/>');
    await writeFile(join(projectPath, 'assets/PSDImport/package.xml'), `<?xml version="1.0"?>
<packageDescription id="pkg00001"><resources><component id="shared01" name="SharedButton.xml" path="/shared/"/></resources></packageDescription>`);
    await writeFile(join(projectPath, 'assets/PSDImport/shared/SharedButton.xml'), '<?xml version="1.0"?><component size="100,40"><displayList/></component>');
    const document = makeDocument('/art/menu.psd');
    document.tree.root.children!.push({
      id: 'root_1', sourceId: 30, name: 'ref SharedButton', type: 'shape',
      bounds: { x: 180, y: 30, width: 100, height: 40 }, visible: true, opacity: 1,
    });

    const result = await new FairyGuiExporter().export(document, makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'menu',
    });

    const rootXml = await readFile(join(projectPath, result.rootComponentPath), 'utf8');
    expect(rootXml).toContain('src="shared01"');
    expect(result.diagnostics).not.toEqual(expect.arrayContaining([expect.objectContaining({ code: 'REFERENCE_FALLBACK' })]));
  });

  test('writes content hashes and reference edges to the managed manifest', async () => {
    const result = await new FairyGuiExporter().export(makeDocument('/art/menu.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'menu',
    });

    const manifest = JSON.parse(await readFile(join(projectPath, result.manifestPath), 'utf8'));
    expect(manifest.generatorVersion).toBe('1.0.0');
    expect(manifest.resources.every((resource: { contentHash?: string }) => resource.contentHash?.length === 64)).toBe(true);
    expect(manifest.resources.find((resource: { id: string }) => resource.id === result.rootComponentId).references.length).toBeGreaterThan(0);
  });

  test('changes generation when mask semantics change', async () => {
    const exporter = new FairyGuiExporter();
    const document = makeDocument('/art/menu.psd');
    const first = await exporter.export(document, makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'menu',
    });
    const firstManifest = JSON.parse(await readFile(join(projectPath, first.manifestPath), 'utf8'));

    document.tree.root.children![0].maskType = 'vector';
    const second = await exporter.export(document, makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'menu',
    });
    const secondManifest = JSON.parse(await readFile(join(projectPath, second.manifestPath), 'utf8'));

    expect(secondManifest.generation).not.toBe(firstManifest.generation);
  });

  test('rejects sidecar paths that escape the project root during recovery', async () => {
    const transactionDir = join(projectPath, '.psd-exporter/transactions');
    await mkdir(transactionDir, { recursive: true });
    await writeFile(join(transactionDir, 'unsafe.json'), JSON.stringify({
      status: 'staged',
      stagingDir: '../../outside-staging',
      files: ['../../outside-file'],
      packageXmlPath: 'assets/PSDImport/package.xml',
    }));

    await expect(new FairyGuiExporter().export(makeDocument('/art/menu.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'menu',
    })).rejects.toThrow('escapes project root');
  });

  test('rejects transaction cleanup outside the managed page directory', async () => {
    const transactionDir = join(projectPath, '.psd-exporter/transactions');
    await mkdir(transactionDir, { recursive: true });
    await writeFile(join(transactionDir, 'unsafe-managed-file.json'), JSON.stringify({
      status: 'staged',
      files: ['settings/Common.json'],
      manifest: {
        schemaVersion: 1,
        pageId: 'menu',
        packageName: 'PSDImport',
        packageId: 'package1',
        componentName: 'Menu',
        rootComponentId: 'root0001',
        generation: 'generation1',
        sourcePath: '/art/menu.psd',
        resources: [],
        files: ['settings/Common.json'],
        generatedAt: '2026-08-20T00:00:00.000Z',
        generatorVersion: '1.0.0',
      },
    }));

    await expect(new FairyGuiExporter().export(makeDocument('/art/menu.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'menu',
    })).rejects.toThrow('escapes managed root');
  });

  test('rejects transaction manifests that redirect the managed root', async () => {
    const transactionDir = join(projectPath, '.psd-exporter/transactions');
    await mkdir(transactionDir, { recursive: true });
    await writeFile(join(transactionDir, 'unsafe-manifest.json'), JSON.stringify({
      status: 'staged',
      files: ['settings/Common.json'],
      manifest: {
        schemaVersion: 1,
        pageId: '..',
        packageName: '../settings',
        packageId: 'package1',
        componentName: 'Menu',
        rootComponentId: 'root0001',
        generation: 'generation1',
        sourcePath: '/art/menu.psd',
        resources: [],
        files: ['settings/Common.json'],
        generatedAt: '2026-08-20T00:00:00.000Z',
        generatorVersion: '1.0.0',
      },
    }));

    await expect(new FairyGuiExporter().export(makeDocument('/art/menu.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'menu',
    })).rejects.toThrow('is not a safe path segment');
  });

  test('pre-commit recovery never deletes files from the previous manifest', async () => {
    const exporter = new FairyGuiExporter();
    const result = await exporter.export(makeDocument('/art/menu.psd'), makeComponents(), {
      projectPath, packageName: 'PSDImport', componentName: 'Menu', pageId: 'menu',
    });
    const manifest = JSON.parse(await readFile(join(projectPath, result.manifestPath), 'utf8'));
    const transactionDir = join(projectPath, '.psd-exporter/transactions');
    await mkdir(transactionDir, { recursive: true });
    await writeFile(join(transactionDir, 'same-generation.json'), JSON.stringify({
      status: 'staged',
      files: manifest.files,
      oldManifest: manifest,
    }));

    const unresolved = makeReferenceDocument('/art/unresolved.psd');
    await expect(exporter.export(unresolved, new Map(), {
      projectPath, packageName: 'PSDImport', componentName: 'Unresolved', pageId: 'unresolved',
    })).rejects.toThrow('UNRESOLVED_REFERENCE');

    await expect(access(join(projectPath, manifest.files[0]))).resolves.toBeUndefined();
  });

  test('maps nested dropdown template and item to an editable popup list', async () => {
    const document = makeDocument('/art/dropdown.psd');
    document.tree.root.children = [{
      id: 'dropdown', sourceId: 100, name: 'Language.dpd', type: 'group',
      bounds: { x: 10, y: 10, width: 120, height: 40 }, visible: true, opacity: 1,
      children: [{
        id: 'label', sourceId: 101, name: 'Label.dpdlb', type: 'text',
        bounds: { x: 15, y: 15, width: 80, height: 20 }, visible: true, opacity: 1, text: '中文',
        textStyles: { fontSize: 14, color: { r: 1, g: 1, b: 1, a: 1 }, fontName: 'Arial', fontStyle: { bold: false, italic: false }, alignment: { horizontal: 'left', vertical: 'top' } },
      }, {
        id: 'template', sourceId: 102, name: 'Popup.template', type: 'group',
        bounds: { x: 10, y: 50, width: 120, height: 100 }, visible: true, opacity: 1,
        children: [{
          id: 'item', sourceId: 103, name: 'Option.item', type: 'group',
          bounds: { x: 10, y: 50, width: 120, height: 24 }, visible: true, opacity: 1,
          children: [{
            id: 'itemText', sourceId: 104, name: 'OptionText.txt', type: 'text',
            bounds: { x: 15, y: 52, width: 80, height: 18 }, visible: true, opacity: 1, text: '选项',
            textStyles: { fontSize: 14, color: { r: 1, g: 1, b: 1, a: 1 }, fontName: 'Arial', fontStyle: { bold: false, italic: false }, alignment: { horizontal: 'left', vertical: 'top' } },
          }],
        }],
      }],
    }];
    const components = new Map<string, ComponentInfo>([
      ['dropdown', { type: 'Dropdown', confidence: 1, source: 'tag', needsReview: false }],
      ['label', { type: 'Unknown', role: 'dpdlb', confidence: 1, source: 'tag', needsReview: false }],
      ['template', { type: 'Unknown', role: 'template', confidence: 1, source: 'tag', needsReview: false }],
      ['item', { type: 'Unknown', role: 'item', confidence: 1, source: 'tag', needsReview: false }],
      ['itemText', { type: 'Text', confidence: 1, source: 'tag', needsReview: false }],
    ]);

    const result = await new FairyGuiExporter().export(document, components, {
      projectPath, packageName: 'PSDImport', componentName: 'DropdownPage', pageId: 'dropdown',
    });
    const manifest = JSON.parse(await readFile(join(projectPath, result.manifestPath), 'utf8'));
    const popup = manifest.resources.find((resource: { name: string }) => resource.name.includes('Popup'));
    const dropdown = await readFile(join(projectPath, result.componentPaths.Language), 'utf8');
    const popupXml = await readFile(join(projectPath, popup.filePath), 'utf8');

    expect(dropdown).toContain('extention="ComboBox"');
    expect(dropdown).toContain(`dropdown="ui://${result.packageId}${popup.id}"`);
    expect(popupXml).toContain('defaultItem="ui://');
    expect(popupXml).toContain('name="list"');
    expect(result.diagnostics).not.toEqual(expect.arrayContaining([expect.objectContaining({ code: 'DROPDOWN_DEGRADED' })]));
  });

  test('degrades an incomplete dropdown to a plain component without ComboBox runtime semantics', async () => {
    const document = makeDocument('/art/incomplete-dropdown.psd');
    document.tree.root.children = [{
      id: 'dropdown', sourceId: 150, name: 'Filter.dpd', type: 'group',
      bounds: { x: 0, y: 0, width: 180, height: 40 }, visible: true, opacity: 1,
      children: [{
        id: 'background', sourceId: 151, name: 'Background.img', type: 'image',
        bounds: { x: 0, y: 0, width: 180, height: 40 }, visible: true, opacity: 1,
      }],
    }];
    document.rasterSources = new Map([
      ['background', { layerId: 'background', photoshopLayerId: 151, width: 180, height: 40, rgba: imageRgba(180, 40) }],
    ]);
    const components = new Map<string, ComponentInfo>([
      ['dropdown', { type: 'Dropdown', confidence: 1, source: 'tag', needsReview: false }],
    ]);

    const result = await new FairyGuiExporter().export(document, components, {
      projectPath, packageName: 'PSDImport', componentName: 'IncompleteDropdown', pageId: 'incomplete-dropdown',
    });
    const dropdownXml = await readFile(join(projectPath, result.componentPaths.Filter), 'utf8');

    expect(dropdownXml).not.toContain('extention="ComboBox"');
    expect(dropdownXml).not.toContain('<ComboBox');
    expect(result.diagnostics).toEqual(expect.arrayContaining([
      expect.objectContaining({ code: 'DROPDOWN_DEGRADED', layerId: 'dropdown' }),
    ]));
  });

  test('maps scroll viewport and content to a vertical editable ScrollPane', async () => {
    const document = makeDocument('/art/scroll.psd');
    document.tree.root.children = [{
      id: 'scroll', sourceId: 200, name: 'Content.sv', type: 'group',
      bounds: { x: 10, y: 10, width: 100, height: 80 }, visible: true, opacity: 1,
      children: [{
        id: 'viewport', sourceId: 201, name: 'Viewport.vpt', type: 'group',
        bounds: { x: 10, y: 10, width: 100, height: 80 }, visible: true, opacity: 1,
        children: [{
          id: 'content', sourceId: 202, name: 'Content.content', type: 'group',
          bounds: { x: 10, y: 10, width: 100, height: 180 }, visible: true, opacity: 1,
          children: [{
            id: 'contentText', sourceId: 203, name: 'Text.txt', type: 'text',
            bounds: { x: 20, y: 140, width: 80, height: 20 }, visible: true, opacity: 1, text: '可滚动',
            textStyles: { fontSize: 14, color: { r: 1, g: 1, b: 1, a: 1 }, fontName: 'Arial', fontStyle: { bold: false, italic: false }, alignment: { horizontal: 'left', vertical: 'top' } },
          }],
        }],
      }, {
        id: 'vbarbg', sourceId: 204, name: 'VbarBG.vbarbg', type: 'image',
        bounds: { x: 90, y: 10, width: 10, height: 80 }, visible: true, opacity: 1,
      }, {
        id: 'vbar', sourceId: 205, name: 'Vbar.vbar', type: 'image',
        bounds: { x: 90, y: 10, width: 10, height: 20 }, visible: true, opacity: 1,
      }],
    }];
    const components = new Map<string, ComponentInfo>([
      ['scroll', { type: 'ScrollView', confidence: 1, source: 'tag', needsReview: false }],
      ['viewport', { type: 'Unknown', role: 'vpt', confidence: 1, source: 'tag', needsReview: false }],
      ['content', { type: 'Unknown', role: 'content', confidence: 1, source: 'tag', needsReview: false }],
      ['contentText', { type: 'Text', confidence: 1, source: 'tag', needsReview: false }],
      ['vbarbg', { type: 'Unknown', role: 'vbarbg', confidence: 1, source: 'tag', needsReview: false }],
      ['vbar', { type: 'Unknown', role: 'vbar', confidence: 1, source: 'tag', needsReview: false }],
    ]);

    const result = await new FairyGuiExporter().export(document, components, {
      projectPath, packageName: 'PSDImport', componentName: 'ScrollPage', pageId: 'scroll',
    });
    const manifest = JSON.parse(await readFile(join(projectPath, result.manifestPath), 'utf8'));
    const pane = manifest.resources.find((resource: { name: string }) => resource.name.includes('Pane'));
    const scrollbar = manifest.resources.find((resource: { name: string }) => resource.name.includes('VerticalScrollBar'));
    const rootXml = await readFile(join(projectPath, result.rootComponentPath), 'utf8');
    const paneXml = await readFile(join(projectPath, pane.filePath), 'utf8');
    const scrollbarXml = await readFile(join(projectPath, scrollbar.filePath), 'utf8');

    expect(rootXml).toContain('name="Content"');
    expect(paneXml).toContain('overflow="scroll"');
    expect(paneXml).not.toContain('scroll="horizontal"');
    expect(paneXml).toContain(`vtScrollBar="ui://${result.packageId}${scrollbar.id}"`);
    expect(scrollbarXml).toContain('extention="ScrollBar"');
    expect(scrollbarXml).toContain('name="bar"');
    expect(scrollbarXml).toContain('name="grip"');
    expect(paneXml).toContain('name="content"');
    expect(result.diagnostics).not.toEqual(expect.arrayContaining([expect.objectContaining({ code: 'SCROLLBAR_DEGRADED' })]));
  });

  test('maps a stable homogeneous grid to a FairyGUI flow_hz list', async () => {
    const document = makeDocument('/art/grid.psd');
    const makeCell = (id: string, sourceId: number, x: number, y: number): Layer => ({
      id, sourceId, name: `${id}.comp`, type: 'group', bounds: { x, y, width: 40, height: 40 }, visible: true, opacity: 1,
      children: [{
        id: `${id}-text`, sourceId: sourceId + 100, name: 'Label.txt', type: 'text', bounds: { x: x + 4, y: y + 4, width: 30, height: 20 }, visible: true, opacity: 1, text: 'Cell',
        textStyles: { fontSize: 12, color: { r: 1, g: 1, b: 1, a: 1 }, fontName: 'Arial', fontStyle: { bold: false, italic: false }, alignment: { horizontal: 'left', vertical: 'top' } },
      }],
    });
    document.tree.root.children = [{
      id: 'grid', sourceId: 300, name: 'Inventory.grid', type: 'group', bounds: { x: 0, y: 0, width: 90, height: 90 }, visible: true, opacity: 1,
      children: [makeCell('cell-a', 301, 0, 0), makeCell('cell-b', 302, 45, 0), makeCell('cell-c', 303, 0, 45), makeCell('cell-d', 304, 45, 45)],
    }];
    const components = new Map<string, ComponentInfo>([
      ['grid', { type: 'GridLayoutGroup', confidence: 1, source: 'tag', needsReview: false }],
      ...['cell-a', 'cell-b', 'cell-c', 'cell-d'].map(id => [id, { type: 'Unknown', confidence: 1, source: 'tag', needsReview: false }] as const),
      ...['cell-a-text', 'cell-b-text', 'cell-c-text', 'cell-d-text'].map(id => [id, { type: 'Text', confidence: 1, source: 'tag', needsReview: false }] as const),
    ]);

    const result = await new FairyGuiExporter().export(document, components, {
      projectPath, packageName: 'PSDImport', componentName: 'GridPage', pageId: 'grid',
    });
    const rootXml = await readFile(join(projectPath, result.rootComponentPath), 'utf8');

    expect(rootXml).toContain('layout="flow_hz"');
    expect(rootXml).toContain('lineGap="5"');
    expect(rootXml).toContain('colGap="5"');
    expect(result.diagnostics).not.toEqual(expect.arrayContaining([expect.objectContaining({ code: 'GRID_DEGRADED' })]));
  });
});
