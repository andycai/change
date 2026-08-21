#!/usr/bin/env node

import { Command, Option } from 'commander';
import { ConfigLoader } from '../config/config-loader';
import { ExportService, ExportTarget } from '../export/export-service';
import { Logger } from '../utils/logger';

interface CliDependencies {
  exportServiceFactory?: () => ExportService;
}

interface ParseOptions {
  output: string;
  config?: string;
  debug: boolean;
  assets?: string;
  target: ExportTarget;
  fairyguiProject?: string;
  fairyguiPackage?: string;
  fairyguiComponent?: string;
  fairyguiPageId?: string;
  fairyguiSourceRoot?: string;
  fairyguiAdoptExisting?: boolean;
}

export function createProgram(dependencies: CliDependencies = {}): Command {
  const program = new Command();

  program
    .name('psd-exporter')
    .description('PSD to Unity UGUI and FairyGUI source project exporter')
    .version('1.0.0');

  program
    .command('parse')
    .description('Parse one PSD and export the selected target')
    .argument('<psdPath>', 'Path to the PSD file')
    .option('-o, --output <path>', 'UGUI JSON output file or directory', 'output.json')
    .option('-c, --config <path>', 'Path to config file')
    .option('-d, --debug', 'Enable debug logging', false)
    .option('-a, --assets <dir>', 'UGUI/HTML asset output directory')
    .addOption(new Option('--target <target>', 'Export target').choices(['ugui', 'fairygui', 'all']).default('ugui'))
    .option('--fairygui-project <dir>', 'FairyGUI project directory')
    .option('--fairygui-package <name>', 'FairyGUI package name')
    .option('--fairygui-component <name>', 'FairyGUI root component name')
    .option('--fairygui-page-id <id>', 'Stable FairyGUI managed page id')
    .option('--fairygui-source-root <dir>', 'Source root used to derive stable page paths')
    .option('--fairygui-adopt-existing', 'Adopt an existing manual root component', false)
    .action(async (psdPath: string, options: ParseOptions) => {
      const logger = new Logger(options.debug);
      try {
        const config = ConfigLoader.load(options.config);
        if (options.debug) config.debug = true;
        logger.info(`Starting PSD export: ${psdPath}`, { target: options.target });

        const service = dependencies.exportServiceFactory?.() ?? new ExportService();
        const result = await service.export({
          psdPath,
          target: options.target,
          outputPath: options.output,
          assetsDir: options.assets,
          fairyGui: {
            projectPath: options.fairyguiProject,
            packageName: options.fairyguiPackage,
            componentName: options.fairyguiComponent,
            pageId: options.fairyguiPageId,
            sourceRoot: options.fairyguiSourceRoot,
            adoptExisting: options.fairyguiAdoptExisting,
          },
        }, config);

        if (result.jsonPath) logger.info(`JSON output saved to: ${result.jsonPath}`);
        if (result.htmlPath) logger.info(`HTML preview saved to: ${result.htmlPath}`);
        if (result.fairyGui) {
          logger.info(`FairyGUI page exported: ${result.fairyGui.rootComponentPath}`, {
            packageId: result.fairyGui.packageId,
            rootComponentId: result.fairyGui.rootComponentId,
            diagnostics: result.fairyGui.diagnostics.length,
            reportStatus: result.fairyGui.reportStatus,
            ...(result.fairyGui.reportError ? { reportError: result.fairyGui.reportError } : {}),
          });
        }
        logger.info('PSD export completed successfully!');
      } catch (error) {
        logger.error('PSD export failed', {
          error: error instanceof Error ? error.message : String(error),
        });
        throw error;
      }
    });

  return program;
}

export async function runCli(argv = process.argv, dependencies: CliDependencies = {}): Promise<void> {
  try {
    await createProgram(dependencies).parseAsync(argv);
  } catch {
    process.exitCode = 1;
  }
}

if (require.main === module) {
  void runCli();
}
