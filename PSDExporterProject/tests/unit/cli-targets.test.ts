import { createProgram, runCli } from '../../src/cli/index';
import { ConfigLoader } from '../../src/config/config-loader';

jest.mock('../../src/config/config-loader');

describe('CLI export targets', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    (ConfigLoader.load as jest.Mock).mockReturnValue({
      aiThreshold: 0.7,
      cvConfidenceMin: 0.6,
      enableAI: false,
      debug: false,
      fairyGui: {
        packageName: 'PSDImport',
        defaultScale9: { unit: 'ratio', left: 0.3, top: 0.3, right: 0.3, bottom: 0.3 },
        fontMappings: {},
        references: { local: {}, external: {} },
        pages: {},
      },
    });
  });

  test('defaults to the existing ugui target', async () => {
    const service = { export: jest.fn().mockResolvedValue({ target: 'ugui' }) };
    await createProgram({ exportServiceFactory: () => service as never })
      .parseAsync(['node', 'psd-exporter', 'parse', '/art/menu.psd']);

    expect(service.export.mock.calls[0][0]).toMatchObject({ target: 'ugui' });
  });

  test('passes all FairyGUI CLI overrides to the export service', async () => {
    const service = {
      export: jest.fn().mockResolvedValue({
        target: 'fairygui',
        fairyGui: {
          packageId: 'package1',
          rootComponentId: 'root0001',
          rootComponentPath: 'MainMenu.xml',
          componentPaths: {},
          manifestPath: '.psd-exporter/pages/main-menu.json',
          diagnostics: [],
          reportStatus: 'written',
        },
      }),
    };
    await createProgram({ exportServiceFactory: () => service as never }).parseAsync([
      'node', 'psd-exporter', 'parse', '/art/menu.psd',
      '--target', 'fairygui',
      '--fairygui-project', '/ui-project',
      '--fairygui-package', 'Menus',
      '--fairygui-component', 'MainMenu',
      '--fairygui-page-id', 'main-menu',
      '--fairygui-source-root', '/art',
      '--fairygui-adopt-existing',
    ]);

    expect(service.export.mock.calls[0][0]).toMatchObject({
      target: 'fairygui',
      fairyGui: {
        projectPath: '/ui-project',
        packageName: 'Menus',
        componentName: 'MainMenu',
        pageId: 'main-menu',
        sourceRoot: '/art',
        adoptExisting: true,
      },
    });
  });

  test('sets a non-zero exit code when export fails', async () => {
    const previousExitCode = process.exitCode;
    process.exitCode = undefined;
    const service = { export: jest.fn().mockRejectedValue(new Error('missing FairyGUI project')) };

    await runCli(
      ['node', 'psd-exporter', 'parse', '/art/menu.psd', '--target', 'fairygui'],
      { exportServiceFactory: () => service as never },
    );

    expect(process.exitCode).toBe(1);
    process.exitCode = previousExitCode;
  });

  test('keeps a successful exit code when the project committed but report writing failed', async () => {
    const previousExitCode = process.exitCode;
    process.exitCode = undefined;
    const service = {
      export: jest.fn().mockResolvedValue({
        target: 'fairygui',
        fairyGui: {
          packageId: 'package1',
          rootComponentId: 'root0001',
          rootComponentPath: 'MainMenu.xml',
          componentPaths: {},
          manifestPath: '.psd-exporter/pages/main-menu.json',
          diagnostics: [{ level: 'warning', code: 'REPORT_WRITE_FAILED', message: 'report disk failure' }],
          reportStatus: 'failed',
          reportError: 'report disk failure',
        },
      }),
    };

    await runCli(
      ['node', 'psd-exporter', 'parse', '/art/menu.psd', '--target', 'fairygui'],
      { exportServiceFactory: () => service as never },
    );

    expect(process.exitCode).toBeUndefined();
    process.exitCode = previousExitCode;
  });
});
