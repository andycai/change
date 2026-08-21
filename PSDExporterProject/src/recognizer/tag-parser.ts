import { TagParseResult } from './tag-parse-result';
import { TagConfig } from './tag-config-loader';

/**
 * 标签解析器 - 基于点分隔和右到左优先级的新标签体系
 *
 * 解析格式: [ref|refp] <baseName>[.tag1][.tag2]...
 * 其中 tag 使用点 (.) 分隔，右到左优先级覆盖。
 */
export class TagParser {
  private familyMaps: Map<string, Set<string>>;
  private static readonly legacyAliases: Record<string, Partial<TagParseResult['families']>> = {
    btn: { main: 'bt' },
    tmpbtn: { main: 'bt', textBackend: 'tmp' },
    tmptxt: { main: 'txt', textBackend: 'tmp' },
    fillcolor: { main: 'col' },
    tips: { role: 'placeholder' },
    iptlb: { role: 'ipttxt' },
  };

  constructor(config: TagConfig) {
    // Build family lookup maps for O(1) tag classification
    this.familyMaps = new Map();

    this.familyMaps.set('main', new Set(config.families.main.map(t => t.id)));
    this.familyMaps.set('textBackend', new Set(config.families.textBackend.map(t => t.id)));
    this.familyMaps.set('imageType', new Set(config.families.imageType.map(t => t.id)));
    this.familyMaps.set('role', new Set(config.families.role.map(t => t.id)));
  }

  /**
   * Parse layer name into structured tag result
   * @param layerName Layer name to parse
   * @returns TagParseResult or null if no valid tags found
   */
  parse(layerName: string): TagParseResult | null {
    // Step 1: Check for ref/refp prefix
    let prefix: 'ref' | 'refp' | undefined;
    let remaining = layerName;

    if (layerName.startsWith('ref ')) {
      prefix = 'ref';
      remaining = layerName.slice(4); // Remove "ref "
    } else if (layerName.startsWith('refp ')) {
      prefix = 'refp';
      remaining = layerName.slice(5); // Remove "refp "
    }

    // Step 2: Split by dot
    const tokens = remaining.split('.');
    if (tokens.length === 0) return null;

    // Step 3: Parse tags from right to left
    // Each token is checked against all families. If a match is found and
    // that family slot is not yet filled, assign it (rightmost wins).
    const families: TagParseResult['families'] = {};
    const directives: TagParseResult['directives'] = {};
    const classifiedIndices = new Set<number>();

    for (let i = tokens.length - 1; i >= 0; i--) {
      const token = tokens[i];
      if (token === 'comp') {
        directives.component = true;
        classifiedIndices.add(i);
        continue;
      }

      const scale9 = this.parseScale9(token);
      if (scale9) {
        directives.scale9 = scale9;
        classifiedIndices.add(i);
        continue;
      }
      const legacyFamilies = this.getLegacyFamilies(token, remaining);
      if (legacyFamilies) {
        for (const [familyName, familyValue] of Object.entries(legacyFamilies)) {
          if (!families[familyName as keyof typeof families]) {
            families[familyName as keyof typeof families] = familyValue;
          }
        }
        classifiedIndices.add(i);
        continue;
      }
      let found = false;

      // Try to classify this token into a family
      for (const [familyName, tagSet] of this.familyMaps.entries()) {
        if (tagSet.has(token)) {
          // Only assign if this family hasn't been set yet (right-to-left priority)
          if (!families[familyName as keyof typeof families]) {
            families[familyName as keyof typeof families] = token;
          }
          found = true;
          classifiedIndices.add(i);
          break; // Found family, move to next token
        }
      }
      // Unknown tags are silently dropped (not included in baseName)
    }

    // Step 4: Return null if no valid families found
    if (!prefix && Object.keys(families).length === 0 && Object.keys(directives).length === 0) {
      return null;
    }

    // Step 5: Build baseName by scanning left-to-right for unclassified tokens.
    // BaseName stops at:
    //   a) Classified tags (they belong to families, not baseName)
    //   b) The FIRST unclassified token after position 0 that is immediately
    //      followed by a classified tag. This handles unknown/typo tags like
    //      "name.unknowntag.bt" where "unknowntag" is not a recognized tag
    //      but sits between the baseName and a real tag.
    // Multi-segment baseNames (e.g., "my.close.button.bt") are preserved
    // because the intermediate unclassified tokens are not the first one
    // after position 0.
    const baseNameTokens: string[] = [];
    let firstUnclassifiedAfterZero: number | null = null;

    for (let i = 0; i < tokens.length; i++) {
      // Stop at classified tags
      if (classifiedIndices.has(i)) break;

      // Track the first unclassified token after position 0
      if (i > 0 && firstUnclassifiedAfterZero === null) {
        firstUnclassifiedAfterZero = i;
      }

      // If this is the first unclassified after position 0 and it is
      // immediately followed by a classified tag, treat it as an unknown
      // tag boundary -- stop before including it in baseName.
      if (
        i === firstUnclassifiedAfterZero &&
        i + 1 < tokens.length &&
        classifiedIndices.has(i + 1)
      ) {
        break;
      }

      baseNameTokens.push(tokens[i]);
    }
    const baseName = baseNameTokens.join('.');

    return {
      prefix,
      baseName,
      families,
      directives,
    };
  }

  private parseScale9(token: string): TagParseResult['directives']['scale9'] | undefined {
    const match = /^s9-(\d+)-(\d+)-(\d+)-(\d+)$/.exec(token);
    if (!match) return undefined;

    return {
      left: Number(match[1]),
      top: Number(match[2]),
      right: Number(match[3]),
      bottom: Number(match[4]),
    };
  }

  private getLegacyFamilies(
    token: string,
    layerName: string,
  ): Partial<TagParseResult['families']> | undefined {
    if (token.toLowerCase() === 'label') {
      return /dropdownlabel/i.test(layerName)
        ? { role: 'dpdlb' }
        : { role: 'tglb' };
    }
    return TagParser.legacyAliases[token.toLowerCase()];
  }

  /**
   * Check if layer name contains at least one valid tag
   */
  hasTag(layerName: string): boolean {
    return this.parse(layerName) !== null;
  }
}
