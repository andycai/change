import { createHash } from 'crypto';

const ID_LENGTH = 8;

export interface StableIdCollision {
  namespace: string;
  key: string;
  id: string;
  salt: number;
}

export class StableIdAllocator {
  private readonly ids = new Map<string, string>();
  private readonly identities = new Map<string, string>();

  constructor(private readonly onCollision?: (collision: StableIdCollision) => void) {}

  reserve(id: string, owner: string): void {
    this.ids.set(id, `reserved\0${owner}`);
  }

  allocate(namespace: string, key: string, length = ID_LENGTH): string {
    if (!Number.isInteger(length) || length < 1) {
      throw new RangeError(`Stable ID length must be a positive integer, received ${length}`);
    }

    const capacity = 36 ** length;
    if (!Number.isSafeInteger(capacity)) {
      throw new RangeError(`Stable ID length ${length} exceeds the supported collision space`);
    }

    const identity = `${namespace}\0${key}\0${length}`;
    const existing = this.identities.get(identity);
    if (existing) return existing;

    const attemptedIds = new Set<string>();
    for (let salt = 0; salt < capacity; salt++) {
      const saltedKey = salt === 0 ? key : `${key}\0salt:${salt}`;
      const id = createStableId(namespace, saltedKey, length);
      attemptedIds.add(id);
      const owner = this.ids.get(id);
      if (!owner || owner === identity) {
        this.ids.set(id, identity);
        this.identities.set(identity, id);
        if (salt > 0) this.onCollision?.({ namespace, key, id, salt });
        return id;
      }
    }

    for (let index = 0; index < capacity; index++) {
      const id = index.toString(36).padStart(length, '0');
      if (attemptedIds.has(id)) continue;
      const owner = this.ids.get(id);
      if (!owner || owner === identity) {
        this.ids.set(id, identity);
        this.identities.set(identity, id);
        this.onCollision?.({ namespace, key, id, salt: capacity + index });
        return id;
      }
    }

    throw new Error(
      `Stable ID space exhausted for namespace "${namespace}" at length ${length} (${capacity} candidates)`,
    );
  }
}

export function createStableId(namespace: string, key: string, length = ID_LENGTH): string {
  const hex = createHash('sha256').update(`${namespace}\0${key}`).digest('hex');
  return BigInt(`0x${hex}`).toString(36).slice(0, length).padStart(length, '0');
}

export function sanitizeFairyName(value: string, fallback = 'Resource'): string {
  const sanitized = value
    .trim()
    .replace(/[<>:"/\\|?*\u0000-\u001f]/g, '_')
    .replace(/\s+/g, '_')
    .replace(/_+/g, '_')
    .replace(/^\.+|\.+$/g, '');
  return sanitized || fallback;
}

export function createStableName(value: string, key: string): string {
  return `${sanitizeFairyName(value)}_${createStableId('name', key, 6)}`;
}
