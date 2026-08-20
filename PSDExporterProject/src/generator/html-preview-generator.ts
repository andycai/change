import { writeFile } from 'fs/promises';
import * as path from 'path';
import { ComponentInfo } from '../recognizer/component-types';
import {
  GlowEffect,
  Layer,
  LayerTree,
  Rect,
  ShadowEffect,
  TextStyles,
} from '../parser/layer-tree';

type ComponentMap = Map<string, ComponentInfo>;

interface RenderOptions {
  parentX: number;
  parentY: number;
  outputPath?: string;
  extraClass?: string;
  extraAttributes?: string;
}

export class HtmlPreviewGenerator {
  generate(
    layerTree: LayerTree,
    components: ComponentMap = new Map(),
    outputPath?: string,
  ): string {
    const { width, height } = layerTree.metadata.canvasSize;
    const psdName = path.basename(layerTree.metadata.psdPath);
    const title = this.escapeHtml(psdName);
    const children = (layerTree.root.children ?? [])
      .map((layer) => this.renderLayer(layer, components, {
        parentX: layerTree.root.bounds.x,
        parentY: layerTree.root.bounds.y,
        outputPath,
      }))
      .join('');

    return `<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>${title} - PSD 交互预览</title>
  <style>
    :root { color-scheme: dark; }
    * { box-sizing: border-box; }
    html, body { min-height: 100%; margin: 0; }
    body {
      padding: 24px;
      overflow: auto;
      background: #15171a;
      color: #f5f7fa;
      font-family: system-ui, sans-serif;
    }
    .preview-toolbar {
      position: sticky;
      z-index: 1000;
      top: 0;
      display: flex;
      align-items: center;
      gap: 12px;
      width: min(${width}px, 100%);
      margin: 0 auto 12px;
      padding: 8px 12px;
      border: 1px solid #343a40;
      border-radius: 8px;
      background: rgb(24 27 31 / 94%);
      color: #adb5bd;
      font-size: 13px;
      backdrop-filter: blur(8px);
    }
    .preview-toolbar strong { color: #f5f7fa; font-weight: 600; }
    .preview-status { margin-left: auto; color: #74c0fc; }
    .preview-viewport {
      width: min(${width}px, 100%);
      margin: 0 auto;
      overflow: auto;
      border: 1px solid #343a40;
      border-radius: 8px;
      background: #24272b;
      box-shadow: 0 16px 48px rgb(0 0 0 / 45%);
    }
    .preview-stage {
      position: relative;
      width: ${width}px;
      height: ${height}px;
      overflow: hidden;
      background: transparent;
      transform-origin: top left;
    }
    .preview-layer {
      position: absolute;
      box-sizing: border-box;
    }
    .preview-layer[aria-hidden="true"] { display: none; }
    .preview-image {
      display: block;
      width: 100%;
      height: 100%;
      object-fit: fill;
      pointer-events: none;
      user-select: none;
    }
    .preview-text {
      display: flex;
      overflow: hidden;
      align-items: flex-start;
      justify-content: flex-start;
      padding: 0;
      color: #fff;
      white-space: pre-wrap;
      word-break: break-word;
      user-select: none;
    }
    .preview-button,
    .preview-dropdown-trigger {
      position: absolute;
      display: block;
      margin: 0;
      padding: 0;
      overflow: visible;
      border: 0;
      background: transparent;
      cursor: pointer;
      text-align: inherit;
    }
    .preview-button:hover,
    .preview-dropdown-trigger:hover { filter: brightness(1.12); }
    .preview-button:active,
    .preview-button.is-pressed { filter: brightness(.86); transform: translateY(1px); }
    .preview-toggle { cursor: pointer; }
    .preview-toggle > input,
    .preview-input-control,
    .preview-slider-control {
      position: absolute;
      z-index: 20;
    }
    .preview-toggle > input {
      width: 100%;
      height: 100%;
      margin: 0;
      cursor: pointer;
      opacity: 0;
    }
    .preview-toggle[data-checked="false"] [data-layer-role="mark"] { opacity: .2; }
    .preview-input-control {
      display: block;
      margin: 0;
      padding: 0 14px;
      border: 1px solid rgb(255 255 255 / 20%);
      border-radius: 6px;
      outline: none;
      background: rgb(20 20 20 / 18%);
      color: #fff;
      font: inherit;
    }
    .preview-input-control:focus {
      border-color: #74c0fc;
      box-shadow: 0 0 0 2px rgb(116 192 252 / 30%);
    }
    .preview-slider-control {
      appearance: none;
      height: 24px;
      margin: 0;
      border-radius: 999px;
      outline: none;
      background: linear-gradient(to right, #4dabf7 0%, #4dabf7 var(--value), rgb(255 255 255 / 25%) var(--value), rgb(255 255 255 / 25%) 100%);
      cursor: pointer;
    }
    .preview-slider-control::-webkit-slider-thumb {
      width: 24px;
      height: 24px;
      appearance: none;
      border: 2px solid #fff;
      border-radius: 50%;
      background: #228be6;
      box-shadow: 0 2px 6px rgb(0 0 0 / 40%);
    }
    .preview-slider-control::-moz-range-thumb {
      width: 20px;
      height: 20px;
      border: 2px solid #fff;
      border-radius: 50%;
      background: #228be6;
      box-shadow: 0 2px 6px rgb(0 0 0 / 40%);
    }
    .preview-dropdown { overflow: visible; }
    .preview-dropdown-menu {
      position: absolute;
      z-index: 50;
      overflow: auto;
      border: 1px solid rgb(255 255 255 / 30%);
      border-radius: 6px;
      background: #343a40;
      box-shadow: 0 8px 20px rgb(0 0 0 / 45%);
    }
    .preview-dropdown-menu[aria-hidden="true"] { display: none; }
    .preview-dropdown-menu [data-component="Toggle"] { cursor: pointer; }
    .preview-scroll-view {
      overflow: auto;
      scrollbar-color: #74c0fc #343a40;
      scrollbar-width: auto;
    }
    .preview-scroll-view::-webkit-scrollbar { width: 16px; height: 16px; }
    .preview-scroll-view::-webkit-scrollbar-track { background: #343a40; }
    .preview-scroll-view::-webkit-scrollbar-thumb { border: 3px solid #343a40; border-radius: 999px; background: #74c0fc; }
    .preview-scroll-content { position: relative; }
    .preview-scroll-view > [data-layer-role="vbar"],
    .preview-scroll-view > [data-layer-role="vbarbg"] { pointer-events: none; opacity: .3; }
    .preview-unknown { pointer-events: none; }
  </style>
</head>
<body>
  <div class="preview-toolbar">
    <strong>${title}</strong>
    <span>可交互 PSD 页面预览 · ${width} × ${height}</span>
    <span class="preview-status" id="preview-status" aria-live="polite">就绪</span>
  </div>
  <div class="preview-viewport">
    <div class="preview-stage" id="preview-stage" data-width="${width}" data-height="${height}">${children}</div>
  </div>
  <script>
    (() => {
      const status = document.getElementById('preview-status');
      const setStatus = (message) => { if (status) status.textContent = message; };

      document.querySelectorAll('[data-component="Button"]').forEach((button) => {
        button.addEventListener('click', () => {
          const active = button.classList.toggle('is-pressed');
          button.setAttribute('aria-pressed', String(active));
          setStatus((button.dataset.layerName || '按钮') + (active ? '：已按下' : '：已释放'));
        });
      });

      document.querySelectorAll('[data-component="Toggle"] > input').forEach((input) => {
        const toggle = input.closest('[data-component="Toggle"]');
        const sync = () => {
          if (!toggle) return;
          toggle.dataset.checked = String(input.checked);
          toggle.setAttribute('aria-checked', String(input.checked));
          setStatus((toggle.dataset.layerName || '开关') + (input.checked ? '：开启' : '：关闭'));
        };
        input.addEventListener('change', sync);
        sync();
      });

      document.querySelectorAll('[data-component="Slider"] input[type="range"]').forEach((input) => {
        const sync = () => {
          const value = Number(input.value);
          const min = Number(input.min);
          const max = Number(input.max);
          const ratio = max === min ? 0 : ((value - min) / (max - min)) * 100;
          input.style.setProperty('--value', ratio + '%');
          setStatus((input.dataset.layerName || '滑块') + '：' + Math.round(value));
        };
        input.addEventListener('input', sync);
        sync();
      });

      document.querySelectorAll('[data-component="Dropdown"]').forEach((dropdown) => {
        const trigger = dropdown.querySelector('[data-dropdown-trigger]');
        const menu = dropdown.querySelector('[data-dropdown-menu]');
        const close = () => {
          if (!menu || !trigger) return;
          menu.setAttribute('aria-hidden', 'true');
          trigger.setAttribute('aria-expanded', 'false');
        };
        trigger?.addEventListener('click', (event) => {
          event.stopPropagation();
          if (!menu) return;
          const open = menu.getAttribute('aria-hidden') !== 'true';
          menu.setAttribute('aria-hidden', String(open));
          trigger.setAttribute('aria-expanded', String(!open));
          setStatus(open ? '下拉框：已关闭' : '下拉框：已打开');
        });
        menu?.addEventListener('click', (event) => {
          const option = event.target.closest('[data-dropdown-option]');
          if (!option) return;
          const label = dropdown.querySelector('[data-dropdown-label]');
          const optionText = option.querySelector('[data-dropdown-option-text]');
          if (label && optionText) label.textContent = optionText.textContent;
          close();
          setStatus('下拉框：已选择 ' + (optionText?.textContent || '选项'));
        });
      });

      document.addEventListener('click', (event) => {
        document.querySelectorAll('[data-component="Dropdown"]').forEach((dropdown) => {
          if (!dropdown.contains(event.target)) {
            const menu = dropdown.querySelector('[data-dropdown-menu]');
            const trigger = dropdown.querySelector('[data-dropdown-trigger]');
            menu?.setAttribute('aria-hidden', 'true');
            trigger?.setAttribute('aria-expanded', 'false');
          }
        });
      });
    })();
  </script>
</body>
</html>
`;
  }

  async save(html: string, outputPath: string): Promise<void> {
    await writeFile(outputPath, html, 'utf-8');
  }

  private renderLayer(layer: Layer, components: ComponentMap, options: RenderOptions): string {
    const bounds = this.getEffectiveBounds(layer);
    const component = this.resolveComponent(layer, components);
    const className = ['preview-layer', this.layerClass(layer), options.extraClass]
      .filter(Boolean)
      .join(' ');
    const attributes = [
      `data-layer-id="${this.escapeAttribute(layer.id)}"`,
      `data-layer-name="${this.escapeAttribute(layer.name)}"`,
      `data-component="${component}"`,
      this.inferLayerRole(layer.name)
        ? `data-layer-role="${this.escapeAttribute(this.inferLayerRole(layer.name))}"`
        : '',
      options.extraAttributes,
    ].filter(Boolean).join(' ');
    const style = this.layerStyle(layer, bounds, options);
    const hidden = layer.visible ? '' : ' aria-hidden="true"';
    const childOptions = {
      parentX: bounds.x,
      parentY: bounds.y,
      outputPath: options.outputPath,
    };

    if (component === 'Button') {
      return `<button type="button" class="${className} preview-button" ${attributes} style="${style}" aria-pressed="false">${this.renderChildren(layer, components, childOptions)}</button>`;
    }
    if (component === 'Toggle') {
      return this.renderToggle(layer, components, bounds, className, attributes, style, hidden, childOptions);
    }
    if (component === 'InputField') {
      return this.renderInputField(layer, components, bounds, className, attributes, style, hidden, childOptions);
    }
    if (component === 'Dropdown') {
      return this.renderDropdown(layer, components, bounds, className, attributes, style, hidden, childOptions);
    }
    if (component === 'Slider') {
      return this.renderSlider(layer, components, bounds, className, attributes, style, hidden, childOptions);
    }
    if (component === 'ScrollView') {
      return this.renderScrollView(layer, components, bounds, className, attributes, style, hidden, childOptions);
    }

    if (layer.type === 'text') {
      return `<span class="${className} preview-text" ${attributes}${hidden} style="${style}${this.textStyle(layer.textStyles)}">${this.escapeHtml(layer.text || '')}</span>`;
    }
    if (layer.type === 'image' || layer.type === 'shape') {
      const source = this.assetSource(layer.assetPath, options.outputPath);
      if (source) {
        return `<img class="${className} preview-image" ${attributes}${hidden} style="${style}" src="${this.escapeAttribute(source)}" alt="${this.escapeAttribute(layer.name)}">`;
      }
    }

    const groupClass = layer.children?.length ? className : `${className} preview-unknown`;
    return `<div class="${groupClass}" ${attributes}${hidden} style="${style}">${this.renderChildren(layer, components, childOptions)}</div>`;
  }

  private renderToggle(
    layer: Layer,
    components: ComponentMap,
    bounds: Rect,
    className: string,
    attributes: string,
    style: string,
    hidden: string,
    childOptions: RenderOptions,
  ): string {
    const children = this.renderChildren(layer, components, childOptions);
    return `<label class="${className} preview-toggle" ${attributes}${hidden} style="${style}" aria-checked="false" data-checked="false"><input type="checkbox" aria-label="${this.escapeAttribute(layer.name)}"><span class="preview-toggle-visual">${children}</span></label>`;
  }

  private renderInputField(
    layer: Layer,
    components: ComponentMap,
    bounds: Rect,
    className: string,
    attributes: string,
    style: string,
    hidden: string,
    childOptions: RenderOptions,
  ): string {
    const placeholderLayer = this.findTextLayer(layer, /placeholder|tips/i);
    const valueLayer = this.findTextLayer(layer, /iptlb|ipttxt|label/i);
    const placeholder = placeholderLayer?.text || '';
    const value = valueLayer?.text || '';
    const children = this.renderChildren(layer, components, childOptions, (child) => {
      return child.type !== 'text' || !/placeholder|tips|iptlb|ipttxt/i.test(child.name);
    });
    return `<label class="${className} preview-input" ${attributes}${hidden} style="${style}">${children}<input class="preview-input-control" type="text" value="${this.escapeAttribute(value)}" placeholder="${this.escapeAttribute(placeholder)}" aria-label="${this.escapeAttribute(layer.name)}" style="left:0;top:0;width:${bounds.width}px;height:${bounds.height}px"></label>`;
  }

  private renderDropdown(
    layer: Layer,
    components: ComponentMap,
    bounds: Rect,
    className: string,
    attributes: string,
    style: string,
    hidden: string,
    childOptions: RenderOptions,
  ): string {
    const listLayer = (layer.children ?? []).find((child) => /listview|template/i.test(child.name));
    const itemLayer = (layer.children ?? []).find((child) => /item/i.test(child.name));
    const triggerChildren = (layer.children ?? [])
      .filter((child) => child !== listLayer && child !== itemLayer)
      .map((child) => this.renderLayer(child, components, childOptions))
      .join('');
    const triggerBounds = this.getChildrenBounds({
      ...layer,
      children: (layer.children ?? []).filter((child) => child !== listLayer && child !== itemLayer),
    }) || bounds;
    const triggerStyle = `left:${triggerBounds.x - bounds.x}px;top:${triggerBounds.y - bounds.y}px;width:${triggerBounds.width}px;height:${triggerBounds.height}px`;
    const triggerMarkup = triggerChildren.replace(
      /data-layer-role="label"/,
      'data-layer-role="label" data-dropdown-label',
    );
    const trigger = `<button type="button" class="preview-dropdown-trigger" data-dropdown-trigger aria-expanded="false" aria-haspopup="listbox" style="${triggerStyle}">${triggerMarkup}</button>`;
    const listBounds = listLayer ? this.getEffectiveBounds(listLayer) : bounds;
    const menuChildren = listLayer
      ? this.renderChildren(listLayer, components, {
        parentX: listBounds.x,
        parentY: listBounds.y,
        outputPath: childOptions.outputPath,
      })
      : '';
    const item = itemLayer
      ? this.renderLayer(itemLayer, components, {
        parentX: listBounds.x,
        parentY: listBounds.y,
        outputPath: childOptions.outputPath,
        extraAttributes: 'data-dropdown-option role="option"',
      })
      : '';
    const optionMarkup = item.replace(
      /data-layer-role="label"/g,
      'data-layer-role="label" data-dropdown-option-text',
    );
    const menuStyle = `left:${listBounds.x - bounds.x}px;top:${listBounds.y - bounds.y}px;width:${listBounds.width}px;height:${listBounds.height}px`;
    const menu = `<div class="preview-dropdown-menu" data-dropdown-menu aria-hidden="true" role="listbox" style="${menuStyle}">${menuChildren}${optionMarkup}</div>`;
    return `<div class="${className} preview-dropdown" ${attributes}${hidden} style="${style}">${trigger}${menu}</div>`;
  }

  private renderSlider(
    layer: Layer,
    components: ComponentMap,
    bounds: Rect,
    className: string,
    attributes: string,
    style: string,
    hidden: string,
    childOptions: RenderOptions,
  ): string {
    const children = this.renderChildren(layer, components, childOptions);
    const track = (layer.children ?? []).find((child) => /sliderbg|track/i.test(child.name)) || layer.children?.[0];
    const trackBounds = track ? this.getEffectiveBounds(track) : bounds;
    const left = trackBounds.x - bounds.x;
    const top = trackBounds.y - bounds.y;
    const range = `<input class="preview-slider-control" type="range" min="0" max="100" value="50" aria-label="${this.escapeAttribute(layer.name)}" data-layer-name="${this.escapeAttribute(layer.name)}" style="left:${left}px;top:${top}px;width:${trackBounds.width}px;--value:50%">`;
    return `<div class="${className} preview-slider" ${attributes}${hidden} style="${style}">${children}${range}</div>`;
  }

  private renderScrollView(
    layer: Layer,
    components: ComponentMap,
    bounds: Rect,
    className: string,
    attributes: string,
    style: string,
    hidden: string,
    childOptions: RenderOptions,
  ): string {
    const contentBounds = this.getChildrenBounds(layer);
    const contentWidth = Math.max(bounds.width, contentBounds ? contentBounds.x + contentBounds.width - bounds.x : bounds.width);
    const contentHeight = Math.max(
      bounds.height + Math.max(80, Math.round(bounds.height * 0.35)),
      contentBounds ? contentBounds.y + contentBounds.height - bounds.y : bounds.height,
    );
    const children = this.renderChildren(layer, components, {
      ...childOptions,
      parentX: bounds.x,
      parentY: bounds.y,
    });
    return `<div class="${className} preview-scroll-view" ${attributes}${hidden} style="${style}" data-scroll-container><div class="preview-scroll-content" style="width:${contentWidth}px;height:${contentHeight}px">${children}</div></div>`;
  }

  private renderChildren(
    layer: Layer,
    components: ComponentMap,
    options: RenderOptions,
    filter: (child: Layer) => boolean = () => true,
  ): string {
    return (layer.children ?? [])
      .filter(filter)
      .map((child) => this.renderLayer(child, components, options))
      .join('');
  }

  private resolveComponent(layer: Layer, components: ComponentMap): ComponentInfo['type'] {
    const mapped = components.get(layer.id)?.type;
    if (mapped && mapped !== 'Unknown') return mapped;
    if (!layer.children?.length) return mapped || 'Unknown';
    if (/(^|[._\s])(?:bt|btn|tmpbtn|button)(?:$|[._\s])/i.test(layer.name)) return 'Button';
    return mapped || 'Unknown';
  }

  private getEffectiveBounds(layer: Layer): Rect {
    if (layer.bounds.width > 0 && layer.bounds.height > 0) return layer.bounds;
    const childrenBounds = this.getChildrenBounds(layer);
    return childrenBounds || layer.bounds;
  }

  private getChildrenBounds(layer: Layer): Rect | undefined {
    const children = layer.children ?? [];
    if (children.length === 0) return undefined;
    let result: Rect | undefined;
    for (const child of children) {
      const bounds = this.getEffectiveBounds(child);
      result = result
        ? this.unionBounds(result, bounds)
        : { ...bounds };
    }
    return result;
  }

  private unionBounds(first: Rect, second: Rect): Rect {
    const right = Math.max(first.x + first.width, second.x + second.width);
    const bottom = Math.max(first.y + first.height, second.y + second.height);
    const x = Math.min(first.x, second.x);
    const y = Math.min(first.y, second.y);
    return { x, y, width: right - x, height: bottom - y };
  }

  private layerStyle(layer: Layer, bounds: Rect, options: RenderOptions): string {
    return `left:${bounds.x - options.parentX}px;top:${bounds.y - options.parentY}px;width:${bounds.width}px;height:${bounds.height}px;opacity:${layer.opacity}`;
  }

  private textStyle(styles?: TextStyles): string {
    if (!styles) return '';
    const color = this.rgba(styles.color);
    const alignment = styles.alignment.horizontal === 'justify' ? 'justify' : styles.alignment.horizontal;
    const vertical = {
      top: 'flex-start',
      middle: 'center',
      bottom: 'flex-end',
    }[styles.alignment.vertical];
    const shadows = (styles.effects ?? [])
      .filter((effect): effect is ShadowEffect | GlowEffect =>
        effect.enabled && (effect.type === 'dropShadow' || effect.type === 'outerGlow'))
      .map((effect) => {
        if (effect.type === 'dropShadow') return `${effect.offsetX}px ${effect.offsetY}px ${effect.blur}px ${this.rgba(effect.color)}`;
        if (effect.type === 'outerGlow') return `0 0 ${effect.size}px ${this.rgba(effect.color)}`;
        return '';
      })
      .join(',');
    return `font-family:${this.escapeAttribute(styles.fontName)},sans-serif;font-size:${styles.fontSize}px;font-weight:${styles.fontStyle.bold ? '700' : '400'};font-style:${styles.fontStyle.italic ? 'italic' : 'normal'};color:${color};text-align:${alignment};align-items:${vertical};${shadows ? `text-shadow:${shadows};` : ''}`;
  }

  private assetSource(assetPath: string | undefined, outputPath?: string): string | undefined {
    if (!assetPath) return undefined;
    const resolved = path.resolve(assetPath);
    if (outputPath) {
      return path.relative(path.dirname(path.resolve(outputPath)), resolved).split(path.sep).join('/');
    }
    return assetPath;
  }

  private findTextLayer(layer: Layer, namePattern: RegExp): Layer | undefined {
    for (const child of layer.children ?? []) {
      if (child.type === 'text' && namePattern.test(child.name)) return child;
      const nested = this.findTextLayer(child, namePattern);
      if (nested) return nested;
    }
    return undefined;
  }

  private inferLayerRole(name: string): string {
    const match = name.match(/(?:^|\.)(bg|label|mark|vbarbg|vbar|fill|handle|placeholder|item)(?:\.|$)/i);
    return match ? match[1].toLowerCase() : '';
  }

  private layerClass(layer: Layer): string {
    return `layer-${layer.id.replace(/[^a-zA-Z0-9_-]/g, '-')}`;
  }

  private rgba(color: { r: number; g: number; b: number; a: number }): string {
    return `rgba(${Math.round(color.r * 255)},${Math.round(color.g * 255)},${Math.round(color.b * 255)},${color.a})`;
  }

  private escapeHtml(value: string): string {
    return value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  private escapeAttribute(value: string): string {
    return this.escapeHtml(value);
  }
}
