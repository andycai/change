import { escapeXml } from './xml-codec';

export type XmlAttributeValue = string | number | boolean | undefined;
export type XmlAttributes = Record<string, XmlAttributeValue>;

export function writeXmlElement(
  name: string,
  attributes: XmlAttributes = {},
  children?: readonly string[],
): string {
  const attributeText = Object.entries(attributes)
    .filter(([, value]) => value !== undefined)
    .map(([key, value]) => ` ${key}="${escapeXml(String(value))}"`)
    .join('');
  if (!children || children.length === 0) return `<${name}${attributeText}/>`;
  return `<${name}${attributeText}>\n${indent(children.join('\n'), 2)}\n</${name}>`;
}

export interface ComponentXmlOptions {
  width: number;
  height: number;
  attributes?: XmlAttributes;
  controllers?: readonly string[];
  displayList: readonly string[];
  footer?: readonly string[];
}

export function writeComponentXml(options: ComponentXmlOptions): string {
  const rootAttributes: XmlAttributes = {
    size: `${Math.round(options.width)},${Math.round(options.height)}`,
    ...options.attributes,
  };
  const attributeText = Object.entries(rootAttributes)
    .filter(([, value]) => value !== undefined)
    .map(([key, value]) => ` ${key}="${escapeXml(String(value))}"`)
    .join('');
  const displayList = `<displayList>\n${indent(options.displayList.join('\n'), 2)}\n</displayList>`;
  const children = [
    ...(options.controllers ?? []),
    displayList,
    ...(options.footer ?? []),
  ];
  return `<?xml version="1.0" encoding="utf-8"?>\n<component${attributeText}>\n${indent(children.join('\n'), 2)}\n</component>\n`;
}

function indent(value: string, spaces: number): string {
  const prefix = ' '.repeat(spaces);
  return value ? value.split('\n').map(line => `${prefix}${line}`).join('\n') : '';
}
