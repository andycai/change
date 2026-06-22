#!/usr/bin/env python3
"""
aggregate-context-map.py - Scan all SPEC.md files and generate context-map.json
"""

import json
import re
import sys
from datetime import datetime
from pathlib import Path


def load_config(config_path: Path) -> dict:
    with open(config_path) as f:
        return json.load(f)


def matches_exclude(path: Path, exclude_patterns: list) -> bool:
    import fnmatch

    path_str = str(path)
    for pattern in exclude_patterns:
        if fnmatch.fnmatch(path_str, pattern):
            return True
    return False


def find_spec_files(
    scan_paths: list, exclude_patterns: list, spec_filename: str, base_dir: Path
) -> list:
    spec_files = []
    for scan_path in scan_paths:
        search_dir = base_dir / scan_path
        if not search_dir.exists():
            print(f"  [WARN] Scan path not found: {search_dir}", file=sys.stderr)
            continue
        for spec_file in search_dir.rglob(spec_filename):
            if not matches_exclude(spec_file, exclude_patterns):
                spec_files.append(spec_file)
    return spec_files


def parse_frontmatter(filepath: Path) -> tuple[dict, list]:
    """Parse YAML frontmatter from SPEC.md. Returns (data, errors)."""
    errors = []
    try:
        content = filepath.read_text(encoding="utf-8")
    except Exception as e:
        return {}, [f"Cannot read file: {e}"]

    if not content.startswith("---"):
        return {}, ["No YAML frontmatter found (must start with ---)"]

    parts = content.split("---", 2)
    if len(parts) < 3:
        return {}, ["Malformed frontmatter (missing closing ---)"]

    yaml_text = parts[1].strip()
    data = {}
    try:
        import yaml

        data = yaml.safe_load(yaml_text) or {}
    except Exception:
        # Manual parse fallback
        lines = yaml_text.splitlines()
        current_key = None
        for line in lines:
            stripped = line.strip()
            if not stripped or stripped.startswith("#"):
                continue
            if stripped.startswith("- "):
                if current_key:
                    item = stripped[2:].strip().strip('"').strip("'")
                    if current_key not in data:
                        data[current_key] = []
                    data[current_key].append(item)
                continue
            if ":" in stripped and not stripped.startswith("#"):
                raw_key, _, raw_val = stripped.partition(":")
                current_key = raw_key.strip()
                # Strip inline comments (from first #, but only after a space)
                val = raw_val.strip()
                comment_idx = val.find("  #")
                if comment_idx >= 0:
                    val = val[:comment_idx].strip()
                val = val.strip('"').strip("'")
                if not val:
                    # Key with empty value — list items follow on subsequent lines
                    if current_key not in data:
                        data[current_key] = []
                    continue
                if val.startswith("[") and val.endswith("]"):
                    val = [
                        v.strip().strip('"').strip("'")
                        for v in val[1:-1].split(",")
                        if v.strip()
                    ]
                data[current_key] = val

    return data, errors


def validate_module(module: dict, filepath: Path) -> list:
    errors = []
    required = ["module_name", "directory", "type", "keywords"]
    for field in required:
        if field not in module or not module[field]:
            errors.append(f"Missing required field: {field}")
    return errors


def generate_context_map(modules: list, config: dict) -> dict:
    return {
        "generated_at": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
        "total_modules": len(modules),
        # "config": {
        #     "scan_paths": config.get("scan_paths", []),
        #     "spec_filename": config.get("spec_filename", "SPEC.md"),
        # },
        "modules": modules,
    }


def main():
    base_dir = Path(__file__).parent.parent.parent.parent
    config_path = base_dir / ".mozi" / "config" / "context-map-config.json"

    if not config_path.exists():
        print(f"[ERROR] Config not found: {config_path}")
        sys.exit(1)

    config = load_config(config_path)
    scan_paths = config.get("scan_paths", [])
    exclude_patterns = config.get("exclude_patterns", [])
    spec_filename = config.get("spec_filename", "SPEC.md")
    output_path = base_dir / config.get("output_path", ".mozi/context-map.json")

    print(f"Scanning for {spec_filename} files...")
    spec_files = find_spec_files(scan_paths, exclude_patterns, spec_filename, base_dir)
    print(f"Found {len(spec_files)} spec files")

    modules = []
    errors_total = []
    warnings = []
    seen_names = {}

    for spec_file in sorted(spec_files):
        rel_path = spec_file.relative_to(base_dir)
        data, parse_errors = parse_frontmatter(spec_file)

        if parse_errors:
            for e in parse_errors:
                errors_total.append(f"{rel_path}: {e}")
            continue

        val_errors = validate_module(data, spec_file)
        if val_errors:
            for e in val_errors:
                errors_total.append(f"{rel_path}: {e}")
            continue

        module_name = data.get("module_name", "")
        if module_name in seen_names:
            warnings.append(
                f"Duplicate module_name '{module_name}': {rel_path} and {seen_names[module_name]}"
            )
        else:
            seen_names[module_name] = str(rel_path)

        modules.append(
            {
                "module_name": module_name,
                "directory": data.get(
                    "directory", str(spec_file.parent.relative_to(base_dir))
                ),
                "type": data.get("type", "Unknown"),
                "keywords": data.get("keywords", []),
                "spec_path": str(rel_path),
            }
        )

    context_map = generate_context_map(modules, config)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(context_map, f, indent=2, ensure_ascii=False)

    print(f"\n✅ Generated: {output_path.relative_to(base_dir)}")
    print(f"   Modules: {len(modules)}")

    if warnings:
        print(f"\n⚠️  Warnings ({len(warnings)}):")
        for w in warnings:
            print(f"   - {w}")

    if errors_total:
        print(f"\n❌ Errors ({len(errors_total)}):")
        for e in errors_total:
            print(f"   - {e}")
        sys.exit(1)


if __name__ == "__main__":
    main()
