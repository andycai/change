import Anthropic from '@anthropic-ai/sdk';
import { readFileSync } from 'fs';
import { ComponentType } from './component-types';
import { Layer } from '../parser/layer-tree';

export class AiIdentifier {
  private client: Anthropic;

  constructor(apiKey: string) {
    this.client = new Anthropic({ apiKey });
  }

  /**
   * 调用 Claude API Vision 识别组件类型
   */
  async identify(
    layer: Layer,
    imagePath: string
  ): Promise<{ type: ComponentType; confidence: number }> {
    // 读取图片并转换为 base64
    const imageBuffer = readFileSync(imagePath);
    const base64Image = imageBuffer.toString('base64');

    // 构建 prompt
    const prompt = `Analyze this UI component image and identify its type.

Layer name: ${layer.name}
Bounds: ${JSON.stringify(layer.bounds)}

Choose ONE of these component types:
- Button: clickable UI element (buttons, tabs, toggles)
- Image: static image or icon
- Text: text label or paragraph
- ScrollView: scrollable container with content
- InputField: text input box
- VerticalLayoutGroup: vertically arranged group of elements
- HorizontalLayoutGroup: horizontally arranged group of elements
- GridLayoutGroup: grid-arranged group of elements
- Unknown: cannot determine type

Return ONLY a JSON object with this format:
{
  "type": "ComponentTypeName",
  "confidence": 0.0-1.0,
  "reasoning": "brief explanation"
}`;

    try {
      const response = await this.client.messages.create({
        model: 'claude-3-5-sonnet-20241022',
        max_tokens: 1024,
        messages: [
          {
            role: 'user',
            content: [
              {
                type: 'image',
                source: {
                  type: 'base64',
                  media_type: 'image/png',
                  data: base64Image,
                },
              },
              {
                type: 'text',
                text: prompt,
              },
            ],
          },
        ],
      });

      // 解析 JSON 响应
      const content = response.content[0];
      if (content.type !== 'text') {
        throw new Error('Unexpected response type from Claude API');
      }

      const result = JSON.parse(content.text);
      return {
        type: result.type as ComponentType,
        confidence: result.confidence,
      };
    } catch (error) {
      console.error('AI identification failed:', error);
      return {
        type: 'Unknown',
        confidence: 0,
      };
    }
  }
}
