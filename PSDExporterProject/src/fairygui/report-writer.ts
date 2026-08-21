import { mkdir, rename, writeFile } from 'fs/promises';
import { dirname, join } from 'path';
import {
  FairyAiSuggestion,
  FairyDiagnostic,
  FairyLayerMapping,
  ManagedPageManifest,
} from './model';

export interface FairyGuiReport {
  pageId: string;
  packageName: string;
  componentName: string;
  sourcePath: string;
  generatedAt: string;
  generatorVersion: string;
  packageId: string;
  rootComponentId: string;
  generation: string;
  diagnostics: FairyDiagnostic[];
  mappings: FairyLayerMapping[];
  aiSuggestions: FairyAiSuggestion[];
  managedFiles: string[];
  resources: ManagedPageManifest['resources'];
}

export function createReportFiles(
  manifest: ManagedPageManifest,
  diagnostics: FairyDiagnostic[],
  mappings: FairyLayerMapping[],
  aiSuggestions: FairyAiSuggestion[],
): Map<string, string> {
  const report: FairyGuiReport = {
    pageId: manifest.pageId,
    packageName: manifest.packageName,
    componentName: manifest.componentName,
    sourcePath: manifest.sourcePath,
    generatedAt: manifest.generatedAt,
    generatorVersion: manifest.generatorVersion,
    packageId: manifest.packageId,
    rootComponentId: manifest.rootComponentId,
    generation: manifest.generation,
    diagnostics,
    mappings,
    aiSuggestions,
    managedFiles: manifest.files,
    resources: manifest.resources,
  };
  const reportDir = join('.psd-exporter', 'reports');
  const json = `${JSON.stringify(report, null, 2)}\n`;

  const diagnosticLines = diagnostics.length === 0
    ? ['- 无']
    : diagnostics.map(item => `- **${item.level.toUpperCase()} ${item.code}**：${item.message}`);
  const fileLines = manifest.files.map(file => `- \`${file}\``);
  const mappingLines = mappings.length === 0
    ? ['- 无']
    : mappings.map(item => `- \`${item.layerId}\` ${item.name} → **${item.target}**${item.role ? `（role: \`${item.role}\`）` : ''}`);
  const aiLines = aiSuggestions.length === 0
    ? ['- 无']
    : aiSuggestions.map(item => `- \`${item.layerId}\` → **${item.suggestedType}**，置信度 ${item.confidence.toFixed(2)}${item.needsReview ? '，需人工复核' : ''}`);
  const markdown = `# FairyGUI 导出报告

- 页面：\`${manifest.pageId}\`
- 包：\`${manifest.packageName}\`（\`${manifest.packageId}\`）
- 根组件：\`${manifest.componentName}\`（\`${manifest.rootComponentId}\`）
- 来源：\`${manifest.sourcePath}\`

## 诊断

${diagnosticLines.join('\n')}

## 图层映射

${mappingLines.join('\n')}

## AI 建议

${aiLines.join('\n')}

## 受管文件

${fileLines.join('\n')}
`;
  return new Map([
    [join(reportDir, `${manifest.pageId}.json`), json],
    [join(reportDir, `${manifest.pageId}.md`), markdown],
  ]);
}

export async function writeReportFiles(projectPath: string, files: ReadonlyMap<string, string>): Promise<void> {
  for (const [relativePath, content] of files) {
    const reportPath = join(projectPath, relativePath);
    await mkdir(dirname(reportPath), { recursive: true });
    const temporaryPath = `${reportPath}.${process.pid}.tmp`;
    await writeFile(temporaryPath, content, 'utf8');
    await rename(temporaryPath, reportPath);
  }
}
