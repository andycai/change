import { writeComponentXml, writeXmlElement } from '../../src/fairygui/component-xml-writer';
import { validateXmlDocument } from '../../src/fairygui/xml-codec';

describe('FairyGUI component XML writer', () => {
  test('escapes attributes and creates a valid component document', () => {
    const child = writeXmlElement('text', { id: 'node1', name: 'A&B', text: '"quoted"' });
    const xml = writeComponentXml({
      width: 100,
      height: 40,
      attributes: { extention: 'Button' },
      controllers: [writeXmlElement('controller', { name: 'button', pages: '0,up' })],
      displayList: [child],
      footer: [writeXmlElement('Button', { mode: 'Check' })],
    });

    expect(xml).toContain('name="A&amp;B"');
    expect(xml).toContain('text="&quot;quoted&quot;"');
    expect(validateXmlDocument(xml)).toEqual([]);
  });
});
