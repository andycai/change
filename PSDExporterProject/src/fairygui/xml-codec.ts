import { XMLBuilder, XMLParser, XMLValidator } from 'fast-xml-parser';

export interface PackageResource {
  tag: string;
  attributes: Record<string, string>;
}

export interface PackageInfo {
  packageId: string;
  resources: PackageResource[];
}

type OrderedNode = Record<string, unknown> & { ':@'?: Record<string, string> };

const options = {
  ignoreAttributes: false,
  attributeNamePrefix: '',
  preserveOrder: true,
  commentPropName: '#comment',
  parseTagValue: false,
  parseAttributeValue: false,
};

export function mergePackageXml(
  existingXml: string,
  managedIds: ReadonlySet<string>,
  resources: PackageResource[],
): string {
  const ast = new XMLParser(options).parse(existingXml) as OrderedNode[];
  const packageNode = ast.find(node => Array.isArray(node.packageDescription));
  if (!packageNode) {
    throw new Error('FairyGUI package.xml is missing packageDescription');
  }

  const children = packageNode.packageDescription as OrderedNode[];
  let resourcesNode = children.find(node => Array.isArray(node.resources));
  if (!resourcesNode) {
    resourcesNode = { resources: [] };
    children.unshift(resourcesNode);
  }

  const current = resourcesNode.resources as OrderedNode[];
  const firstManagedIndex = current.findIndex(node => {
    const id = node[':@']?.id;
    return Boolean(id && managedIds.has(id));
  });
  const retained = current.filter(node => {
    const id = node[':@']?.id;
    return !id || !managedIds.has(id);
  });
  const generated = resources.map(resource => ({
      [resource.tag]: [],
      ':@': resource.attributes,
    }));
  if (firstManagedIndex < 0) {
    resourcesNode.resources = [...retained, ...generated];
  } else {
    const insertionIndex = current
      .slice(0, firstManagedIndex)
      .filter(node => {
        const id = node[':@']?.id;
        return !id || !managedIds.has(id);
      }).length;
    resourcesNode.resources = [
      ...retained.slice(0, insertionIndex),
      ...generated,
      ...retained.slice(insertionIndex),
    ];
  }

  if (!children.some(node => Array.isArray(node.publish))) {
    children.push({
      publish: [{ atlas: [], ':@': { name: 'Default', index: '0' } }],
      ':@': { name: packageNode[':@']?.id ?? 'PSDImport' },
    });
  }

  const xml = new XMLBuilder({ ...options, format: true, suppressEmptyNode: true }).build(ast);
  return xml.startsWith('<?xml') ? xml : `<?xml version="1.0" encoding="utf-8"?>\n${xml}`;
}

export function validatePackageXml(xml: string): string[] {
  const validation = XMLValidator.validate(xml);
  if (validation !== true) {
    return [validation.err.msg];
  }

  const ast = new XMLParser(options).parse(xml) as OrderedNode[];
  const packageNode = ast.find(node => Array.isArray(node.packageDescription));
  if (!packageNode) return ['Missing packageDescription'];
  const children = packageNode.packageDescription as OrderedNode[];
  const resourcesNode = children.find(node => Array.isArray(node.resources));
  if (!resourcesNode) return ['Missing resources'];

  const errors: string[] = [];
  const ids = new Set<string>();
  for (const resource of resourcesNode.resources as OrderedNode[]) {
    if ('#comment' in resource) continue;
    const id = resource[':@']?.id;
    if (!id) {
      errors.push('Resource is missing id');
    } else if (ids.has(id)) {
      errors.push(`Duplicate resource id: ${id}`);
    } else {
      ids.add(id);
    }
  }
  return errors;
}

export function validateXmlDocument(xml: string): string[] {
  const validation = XMLValidator.validate(xml);
  return validation === true ? [] : [validation.err.msg];
}

export function createPackageXml(packageId: string, packageName: string): string {
  return `<?xml version="1.0" encoding="utf-8"?>
<packageDescription id="${escapeXml(packageId)}">
  <resources/>
  <publish name="${escapeXml(packageName)}">
    <atlas name="Default" index="0"/>
  </publish>
</packageDescription>
`;
}

export function readPackageInfo(xml: string): PackageInfo {
  const ast = new XMLParser(options).parse(xml) as OrderedNode[];
  const packageNode = ast.find(node => Array.isArray(node.packageDescription));
  if (!packageNode || !packageNode[':@']?.id) {
    throw new Error('FairyGUI package.xml is missing package id');
  }
  const children = packageNode.packageDescription as OrderedNode[];
  const resourcesNode = children.find(node => Array.isArray(node.resources));
  const resources: PackageResource[] = [];
  for (const node of (resourcesNode?.resources as OrderedNode[] | undefined) ?? []) {
    const tag = Object.keys(node).find(key => key !== ':@' && key !== '#comment');
    if (tag && node[':@']) {
      resources.push({ tag, attributes: { ...node[':@'] } });
    }
  }
  return { packageId: packageNode[':@'].id, resources };
}

export function escapeXml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&apos;')
    .replace(/\r?\n/g, '&#xA;');
}
