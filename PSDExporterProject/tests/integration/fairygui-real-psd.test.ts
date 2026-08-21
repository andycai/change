import { mkdtemp, readFile, rm } from 'fs/promises';
import { tmpdir } from 'os';
import { join, resolve } from 'path';
import { XMLValidator } from 'fast-xml-parser';
import { FairyGuiExporter } from '../../src/fairygui/fairygui-exporter';
import { PsdParser } from '../../src/parser/psd-parser';
import { ComponentRecognizer } from '../../src/recognizer/component-recognizer';

describe('FairyGUI real PSD integration', () => {
  test('exports demo.psd with stable resources and valid XML', async () => {
    const projectPath = await mkdtemp(join(tmpdir(), 'psd-fgui-real-'));
    try {
      const psdPath = resolve(__dirname, '../../../UIArtifacts/psd/demo.psd');
      const document = await new PsdParser().parseDocument(psdPath);
      expect(document.rasterSources.has('root_3_1')).toBe(true);
      expect(document.rasterSources.has('root_3_2')).toBe(true);
      expect(document.rasterSources.has('root_14_3_1')).toBe(true);
      const components = await new ComponentRecognizer(null, {
        enableAI: false,
        aiThreshold: 0.7,
        cvConfidenceMin: 0.6,
      }).recognizeTree(document.tree.root);
      const exporter = new FairyGuiExporter();

      const first = await exporter.export(document, components, {
        projectPath,
        packageName: 'PSDImport',
        pageId: 'demo',
        sourceRoot: resolve(__dirname, '../../../UIArtifacts/psd'),
      });
      const firstManifest = JSON.parse(await readFile(join(projectPath, first.manifestPath), 'utf8'));
      const rootXml = await readFile(join(projectPath, first.rootComponentPath), 'utf8');
      const packageXml = await readFile(join(projectPath, 'assets/PSDImport/package.xml'), 'utf8');

      expect(XMLValidator.validate(rootXml)).toBe(true);
      expect(XMLValidator.validate(packageXml)).toBe(true);
      expect(firstManifest.resources.length).toBeGreaterThan(20);
      expect(firstManifest.resources.every((resource: { contentHash?: string }) => resource.contentHash?.length === 64)).toBe(true);
      expect(first.diagnostics).toContainEqual(expect.objectContaining({ code: 'REFERENCE_FALLBACK' }));

      const second = await exporter.export(document, components, {
        projectPath,
        packageName: 'PSDImport',
        pageId: 'demo',
        sourceRoot: resolve(__dirname, '../../../UIArtifacts/psd'),
      });
      const secondManifest = JSON.parse(await readFile(join(projectPath, second.manifestPath), 'utf8'));

      expect(second.packageId).toBe(first.packageId);
      expect(second.rootComponentId).toBe(first.rootComponentId);
      expect(secondManifest.generation).toBe(firstManifest.generation);
      expect(secondManifest.resources.map((resource: { id: string }) => resource.id)).toEqual(
        firstManifest.resources.map((resource: { id: string }) => resource.id),
      );
    } finally {
      await rm(projectPath, { recursive: true, force: true });
    }
  }, 30000);
});
