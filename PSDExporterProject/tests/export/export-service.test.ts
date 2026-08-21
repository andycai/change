import { Config } from '../../src/config/config-loader';
import { ExportService } from '../../src/export/export-service';
import { ParsedPsdDocument } from '../../src/parser/psd-document';

function makeConfig(): Config {
  return {
    aiThreshold: 0.7,
    cvConfidenceMin: 0.6,
    enableAI: false,
    debug: false,
    fairyGui: {
      projectPath: '/fairy-project',
      packageName: 'PSDImport',
      sourceRoot: '/art',
      defaultScale9: { unit: 'ratio', left: 0.3, top: 0.3, right: 0.3, bottom: 0.3 },
      fontMappings: {},
      references: { local: {}, external: {} },
      pages: { 'menu/main.psd': { pageId: 'main-menu', componentName: 'MainMenu' } },
    },
  };
}

function makeDocument(): ParsedPsdDocument {
  return {
    tree: {
      metadata: {
        psdPath: '/art/menu/main.psd',
        canvasSize: { x: 0, y: 0, width: 100, height: 100 },
        timestamp: '2026-08-20T00:00:00.000Z',
      },
      root: {
        id: 'root', name: 'canvas', type: 'group',
        bounds: { x: 0, y: 0, width: 100, height: 100 }, visible: true, opacity: 1, children: [],
      },
    },
    rasterSources: new Map(),
  };
}

describe('ExportService', () => {
  test('all target parses once and dispatches both backends', async () => {
    const parser = {
      parseDocument: jest.fn().mockResolvedValue(makeDocument()),
      exportAssets: jest.fn().mockResolvedValue(undefined),
    };
    const recognizer = { recognizeTree: jest.fn().mockResolvedValue(new Map()) };
    const jsonGenerator = {
      generate: jest.fn().mockReturnValue({ metadata: {}, layers: [], components: [] }),
      save: jest.fn().mockResolvedValue(undefined),
    };
    const htmlGenerator = {
      generate: jest.fn().mockReturnValue('<html/>'),
      save: jest.fn().mockResolvedValue(undefined),
    };
    const fairyGuiExporter = {
      export: jest.fn().mockResolvedValue({
        packageId: 'package1', rootComponentId: 'root0001', rootComponentPath: 'MainMenu.xml',
        componentPaths: {}, manifestPath: '.psd-exporter/pages/main-menu.json', diagnostics: [], reportStatus: 'written',
      }),
    };
    const service = new ExportService({
      parser,
      recognizerFactory: () => recognizer as never,
      jsonGenerator,
      htmlGenerator,
      fairyGuiExporter,
    });

    await service.export({
      psdPath: '/art/menu/main.psd',
      target: 'all',
      outputPath: '/tmp/main.json',
      assetsDir: '/tmp/assets',
    }, makeConfig());

    expect(parser.parseDocument).toHaveBeenCalledTimes(1);
    expect(parser.exportAssets).toHaveBeenCalledTimes(1);
    expect(jsonGenerator.save).toHaveBeenCalledTimes(1);
    expect(htmlGenerator.save).toHaveBeenCalledTimes(1);
    expect(fairyGuiExporter.export).toHaveBeenCalledTimes(1);
    expect(fairyGuiExporter.export.mock.calls[0][2]).toMatchObject({
      projectPath: '/fairy-project',
      pageId: 'main-menu',
      componentName: 'MainMenu',
    });
  });

  test('fairygui target does not generate UGUI assets, JSON, or HTML', async () => {
    const parser = {
      parseDocument: jest.fn().mockResolvedValue(makeDocument()),
      exportAssets: jest.fn().mockResolvedValue(undefined),
    };
    const jsonGenerator = { generate: jest.fn(), save: jest.fn() };
    const htmlGenerator = { generate: jest.fn(), save: jest.fn() };
    const fairyGuiExporter = { export: jest.fn().mockResolvedValue({ diagnostics: [] }) };
    const service = new ExportService({
      parser,
      recognizerFactory: () => ({ recognizeTree: jest.fn().mockResolvedValue(new Map()) }) as never,
      jsonGenerator: jsonGenerator as never,
      htmlGenerator: htmlGenerator as never,
      fairyGuiExporter: fairyGuiExporter as never,
    });

    await service.export({
      psdPath: '/art/menu/main.psd',
      target: 'fairygui',
      outputPath: '/tmp/should-not-exist.json',
      assetsDir: '/tmp/should-not-exist-assets',
    }, makeConfig());

    expect(parser.parseDocument).toHaveBeenCalledTimes(1);
    expect(parser.exportAssets).not.toHaveBeenCalled();
    expect(jsonGenerator.generate).not.toHaveBeenCalled();
    expect(jsonGenerator.save).not.toHaveBeenCalled();
    expect(htmlGenerator.generate).not.toHaveBeenCalled();
    expect(htmlGenerator.save).not.toHaveBeenCalled();
    expect(fairyGuiExporter.export).toHaveBeenCalledTimes(1);
  });

  test('CLI FairyGUI options override page and file configuration', async () => {
    const fairyGuiExporter = { export: jest.fn().mockResolvedValue({}) };
    const service = new ExportService({
      parser: { parseDocument: jest.fn().mockResolvedValue(makeDocument()), exportAssets: jest.fn() },
      recognizerFactory: () => ({ recognizeTree: jest.fn().mockResolvedValue(new Map()) }) as never,
      fairyGuiExporter: fairyGuiExporter as never,
    });

    await service.export({
      psdPath: '/art/menu/main.psd',
      target: 'fairygui',
      fairyGui: {
        projectPath: '/override-project',
        packageName: 'OverridePackage',
        componentName: 'OverrideComponent',
        pageId: 'override-page',
      },
    }, makeConfig());

    expect(fairyGuiExporter.export.mock.calls[0][2]).toMatchObject({
      projectPath: '/override-project',
      packageName: 'OverridePackage',
      componentName: 'OverrideComponent',
      pageId: 'override-page',
    });
  });

  test('fairygui target rejects missing project path', async () => {
    const config = makeConfig();
    delete config.fairyGui.projectPath;
    const service = new ExportService({
      parser: { parseDocument: jest.fn().mockResolvedValue(makeDocument()), exportAssets: jest.fn() },
      recognizerFactory: () => ({ recognizeTree: jest.fn().mockResolvedValue(new Map()) }) as never,
    });

    await expect(service.export({ psdPath: '/art/menu/main.psd', target: 'fairygui' }, config))
      .rejects.toThrow('requires --fairygui-project');
  });

  test('rejects duplicate configured page ids', async () => {
    const config = makeConfig();
    config.fairyGui.pages['shop/main.psd'] = { pageId: 'main-menu', componentName: 'Shop' };
    const service = new ExportService({
      parser: { parseDocument: jest.fn().mockResolvedValue(makeDocument()), exportAssets: jest.fn() },
      recognizerFactory: () => ({ recognizeTree: jest.fn().mockResolvedValue(new Map()) }) as never,
    });

    await expect(service.export({ psdPath: '/art/menu/main.psd', target: 'fairygui' }, config))
      .rejects.toThrow('configured for both');
  });
});
