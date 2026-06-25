#!/usr/bin/env node

import { Command } from 'commander';
import * as path from 'path';
import * as fs from 'fs';
import { PsdParser } from '../parser/psd-parser';
import { ComponentRecognizer } from '../recognizer/component-recognizer';
import { AiIdentifier } from '../recognizer/ai-identifier';
import { JsonGenerator } from '../generator/json-generator';
import { ConfigLoader } from '../config/config-loader';
import { Logger } from '../utils/logger';
import { LayerTree } from '../parser/layer-tree';
import { ComponentInfo } from '../recognizer/component-types';

const program = new Command();

program
  .name('psd-exporter')
  .description('PSD to Unity UGUI JSON exporter')
  .version('1.0.0');

program
  .command('parse')
  .description('Parse a PSD file and generate JSON output')
  .argument('<psdPath>', 'Path to the PSD file')
  .option('-o, --output <path>', 'Output JSON file path', 'output.json')
  .option('-c, --config <path>', 'Path to config file')
  .option('-d, --debug', 'Enable debug logging', false)
  .option('-a, --assets <dir>', 'Asset output directory')
  .action(async (psdPath: string, options: {
    output: string;
    config?: string;
    debug: boolean;
    assets?: string;
  }) => {
    const logger = new Logger(options.debug);

    try {
      logger.info(`Starting PSD parsing: ${psdPath}`);

      const config = ConfigLoader.load(options.config);
      if (options.debug) config.debug = true;
      logger.info('Configuration loaded', {
        aiThreshold: config.aiThreshold,
        cvConfidenceMin: config.cvConfidenceMin,
        enableAI: config.enableAI,
      });

      logger.info('Parsing PSD file...');
      const parser = new PsdParser();
      const layerTree: LayerTree = await parser.parse(psdPath, options.assets);
      logger.info('PSD parsed successfully', { canvasSize: layerTree.metadata.canvasSize });

      let aiIdentifier: AiIdentifier | null = null;
      if (config.enableAI && config.claudeApiKey) {
        aiIdentifier = new AiIdentifier(config.claudeApiKey);
        logger.info('AI identification enabled');
      } else if (config.enableAI) {
        logger.warn('AI identification enabled but no API key configured. Running without AI.');
      }

      const recognizer = new ComponentRecognizer(aiIdentifier, {
        enableAI: config.enableAI,
        aiThreshold: config.aiThreshold,
        cvConfidenceMin: config.cvConfidenceMin,
      });

      logger.info('Recognizing components...');
      const components: Map<string, ComponentInfo> =
        await recognizer.recognizeTree(layerTree.root, options.assets);

      let tagCount = 0, aiCount = 0, unknownCount = 0, reviewCount = 0;
      for (const [, info] of components) {
        if (info.source === 'tag') tagCount++;
        else if (info.source === 'ai') aiCount++;
        if (info.type === 'Unknown') unknownCount++;
        if (info.needsReview) reviewCount++;
      }
      logger.info('Component recognition complete', {
        total: components.size, tagged: tagCount, aiIdentified: aiCount,
        unknown: unknownCount, needsReview: reviewCount,
      });

      logger.info('Generating JSON output...');
      const generator = new JsonGenerator();
      const jsonConfig = generator.generate(layerTree, components);

      let outputPath = options.output;
      const isExistingDir = fs.existsSync(outputPath) && fs.statSync(outputPath).isDirectory();
      const looksLikeDir = !isExistingDir && !path.extname(outputPath);
      if (isExistingDir || looksLikeDir) {
        if (!fs.existsSync(outputPath)) {
          fs.mkdirSync(outputPath, { recursive: true });
        }
        const baseName = path.basename(psdPath, path.extname(psdPath));
        outputPath = path.join(outputPath, `${baseName}.json`);
      } else {
        const dir = path.dirname(outputPath);
        if (dir && !fs.existsSync(dir)) {
          fs.mkdirSync(dir, { recursive: true });
        }
      }

      await generator.save(jsonConfig, outputPath);
      logger.info(`JSON output saved to: ${outputPath}`);

      logger.info('PSD export completed successfully!');
    } catch (error) {
      logger.error('PSD export failed', {
        error: error instanceof Error ? error.message : String(error),
      });
      process.exit(1);
    }
  });

program.parse(process.argv);
