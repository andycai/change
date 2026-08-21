import { mergePackageXml, validatePackageXml } from '../../src/fairygui/xml-codec';

describe('FairyGUI package XML', () => {
  const existing = `<?xml version="1.0" encoding="utf-8"?>
<packageDescription id="pkg001" compressPNG="true">
  <resources>
    <image id="manual1" name="manual.png" path="/images/"/>
    <component id="oldPage" name="Old.xml" path="/psd/old/" exported="true"/>
  </resources>
  <publish name="Demo"><atlas name="Default" index="0"/></publish>
</packageDescription>`;

  test('replaces only managed ids and preserves manual resources', () => {
    const merged = mergePackageXml(existing, new Set(['oldPage']), [
      { tag: 'component', attributes: { id: 'newPage', name: 'New.xml', path: '/psd/new/', exported: 'true' } },
    ]);

    expect(merged).toContain('id="manual1"');
    expect(merged).not.toContain('id="oldPage"');
    expect(merged).toContain('id="newPage"');
    expect(merged).toContain('compressPNG="true"');
    expect(validatePackageXml(merged)).toEqual([]);
  });

  test('preserves comments, unknown attributes, and manual resource order around managed entries', () => {
    const source = `<?xml version="1.0" encoding="utf-8"?>
<packageDescription id="pkg001" custom="keep">
  <resources>
    <!--before-managed-->
    <font id="manualBefore" name="Manual.fnt" customFont="yes"/>
    <component id="oldPage" name="Old.xml" path="/psd/old/"/>
    <movieclip id="manualAfter" name="Manual.xml" customClip="yes"/>
  </resources>
</packageDescription>`;

    const merged = mergePackageXml(source, new Set(['oldPage']), [
      { tag: 'component', attributes: { id: 'newPage', name: 'New.xml', path: '/psd/new/' } },
    ]);

    expect(merged).toContain('<!--before-managed-->');
    expect(merged).toContain('custom="keep"');
    expect(merged).toContain('customFont="yes"');
    expect(merged).toContain('customClip="yes"');
    expect(merged.indexOf('id="manualBefore"')).toBeLessThan(merged.indexOf('id="newPage"'));
    expect(merged.indexOf('id="newPage"')).toBeLessThan(merged.indexOf('id="manualAfter"'));
    expect(validatePackageXml(merged)).toEqual([]);
  });
});
