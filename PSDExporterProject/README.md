# PSD Exporter - PSD to Unity UGUI JSON Converter

A Node.js tool that parses PSD files and automatically analyzes UI component properties through tag parsing and AI recognition, outputting JSON configuration files for Unity UGUI.

## Features

- **PSD Parsing**: Parse PSD files using ag-psd library, extract complete layer tree
- **Component Recognition**: Two-level recognition strategy
  - Level 1: Tag-based recognition (PSD2UGUI compatible)
  - Level 2: AI-powered recognition using Claude Vision API
- **Asset Export**: Export image layers as PNG files using sharp
- **JSON Generation**: Generate structured JSON configuration with Zod schema validation
- **CLI Interface**: Easy-to-use command-line interface
- **Configurable**: Support configuration file and environment variables
- **Structured Logging**: Debug mode with detailed logging

## Installation

```bash
cd PSDExporterProject
npm install
```

## Configuration

### Option 1: Configuration File

Create `psd-exporter.config.json` in your project root:

```json
{
  "aiThreshold": 0.7,
  "cvConfidenceMin": 0.6,
  "enableAI": true,
  "debug": false,
  "claudeApiKey": "YOUR_CLAUDE_API_KEY_HERE"
}
```

### Option 2: Environment Variable

```bash
export CLAUDE_API_KEY="your-api-key-here"
```

### Configuration Options

- `aiThreshold`: Confidence threshold for AI recognition (0-1, default: 0.7)
- `cvConfidenceMin`: Minimum confidence for component validation (0-1, default: 0.6)
- `enableAI`: Enable AI-powered recognition (default: true)
- `debug`: Enable debug logging (default: false)
- `claudeApiKey`: Claude API key for AI recognition (optional, can use env var)

## Usage

### Basic Usage

```bash
npm run build
node dist/cli/index.js parse <path-to-psd> -o <output-dir>
```

### CLI Options

```bash
Options:
  -o, --output <path>   Output directory for JSON (default: "./output")
  -c, --config <path>   Config file path (default: "./psd-exporter.config.json")
  -d, --debug          Enable debug mode
  --assets <path>      Assets output directory (default: "./output/assets")
  -h, --help           Display help
```

### Examples

```bash
# Parse PSD with default settings
node dist/cli/index.js parse ./test.psd

# Parse with custom output directory
node dist/cli/index.js parse ./test.psd -o ./my-output

# Parse with debug mode
node dist/cli/index.js parse ./test.psd -d

# Parse with custom config
node dist/cli/index.js parse ./test.psd -c ./my-config.json
```

## PSD Layer Naming Convention (PSD2UGUI Compatible)

Use tag prefixes in layer names for automatic component type recognition:

| Tag | Component Type | Example |
|-----|---------------|---------|
| `btn_` | Button | `btn_close`, `btn_submit` |
| `txt_` | Text | `txt_title`, `txt_description` |
| `img_` | Image | `img_logo`, `img_avatar` |
| `sv_` | ScrollView | `sv_content`, `sv_list` |
| `ipt_` | InputField | `ipt_username`, `ipt_password` |
| `vbox_` | VerticalLayoutGroup | `vbox_menu`, `vbox_items` |
| `hbox_` | HorizontalLayoutGroup | `hbox_toolbar`, `hbox_buttons` |
| `grid_` | GridLayoutGroup | `grid_inventory`, `grid_icons` |

## Output Format

The tool generates a JSON file with the following structure:

```json
{
  "metadata": {
    "psdPath": "/path/to/source.psd",
    "canvasSize": { "x": 0, "y": 0, "width": 1920, "height": 1080 },
    "timestamp": "2026-06-24T10:00:00.000Z",
    "exportedBy": "psd-exporter v1.0.0"
  },
  "layers": [
    {
      "id": "root_0",
      "name": "btn_close",
      "type": "group",
      "bounds": { "x": 100, "y": 50, "width": 80, "height": 80 },
      "visible": true,
      "opacity": 1.0,
      "component": {
        "type": "Button",
        "confidence": 1.0,
        "source": "tag",
        "needsReview": false
      }
    }
  ]
}
```

## Development

### Build

```bash
npm run build
```

### Test

```bash
npm test
```

### Lint

```bash
npm run lint
```

### Format

```bash
npm run format
```

## Architecture

### Pipeline Architecture

```
PSD File → Parser → Component Recognizer → JSON Generator → Output
                         ↓
                   Tag Parser
                         ↓
                   AI Identifier
```

### Two-Level Recognition Strategy

1. **Tag Parsing** (Level 1)
   - Parse layer name for PSD2UGUI tags
   - Confidence: 1.0 (100%)
   - Fast and deterministic

2. **AI Recognition** (Level 2)
   - Use Claude Vision API for image analysis
   - Confidence: 0.0-1.0 (variable)
   - Fallback when tag parsing fails

### Modules

- `src/parser/`: PSD parsing and asset export
- `src/recognizer/`: Component recognition (tag + AI)
- `src/generator/`: JSON generation with Zod validation
- `src/config/`: Configuration loader
- `src/utils/`: Logger utility
- `src/cli/`: Command-line interface

## Testing

The project includes comprehensive test coverage:

- Unit tests for all modules
- Integration tests for end-to-end workflow
- Total: 92 tests passing

```bash
npm test
```

## Phase 1 MVP Limitations

This is Phase 1 MVP release. The following features are planned for future phases:

- **Phase 2**: Computer Vision heuristics for enhanced recognition
- **Phase 2**: Nine-slice detection for sprite slicing
- **Phase 2**: Layout analysis for auto-layout groups

## Requirements

- Node.js 18.x or higher
- TypeScript 5.x
- Claude API key (for AI recognition)

## Dependencies

- `ag-psd`: PSD file parsing
- `sharp`: Image processing and export
- `@anthropic-ai/sdk`: Claude API client
- `commander`: CLI framework
- `zod`: Schema validation
- `jest`: Testing framework

## License

MIT

## Contributing

This project follows TDD (Test-Driven Development) principles. All code changes must include tests.

1. Write tests first
2. Implement functionality
3. Ensure all tests pass
4. Submit pull request

## Support

For issues and feature requests, please open an issue in the repository.

## Changelog

### v1.0.0 (2026-06-24)

- Initial Phase 1 MVP release
- PSD parsing with ag-psd
- Tag-based component recognition
- AI-powered recognition with Claude Vision
- JSON output with Zod validation
- CLI interface with configuration support
- Comprehensive test suite (92 tests)
