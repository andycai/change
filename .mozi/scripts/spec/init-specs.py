#!/usr/bin/env python3
"""
init-specs.py - Initialize SPEC.md files for all modules in a project.

Reads context-map-config.json, scans configured paths, and generates
SPEC.md skeleton files for directories that don't have one yet.
"""

import argparse
import fnmatch
import json
import os
import re
import sys
from datetime import datetime
from pathlib import Path

CONFIG_PATH = ".mozi/config/context-map-config.json"
TEMPLATE_PATH = ".mozi/templates/SPEC.md.template"


def load_config(config_path: str) -> dict:
    """Load and validate context-map-config.json."""
    try:
        with open(config_path, "r", encoding="utf-8") as f:
            config = json.load(f)
    except FileNotFoundError:
        print(f"ERROR: Config file not found: {config_path}", file=sys.stderr)
        sys.exit(1)
    except json.JSONDecodeError as e:
        print(f"ERROR: Invalid JSON in config: {e}", file=sys.stderr)
        sys.exit(1)

    required = ["scan_paths", "spec_filename", "output_path"]
    for field in required:
        if field not in config:
            print(f"ERROR: Missing required field '{field}' in config", file=sys.stderr)
            sys.exit(1)

    return config


def load_template(template_path: str) -> str:
    """Load SPEC.md template."""
    try:
        with open(template_path, "r", encoding="utf-8") as f:
            return f.read()
    except FileNotFoundError:
        print(f"ERROR: Template file not found: {template_path}", file=sys.stderr)
        sys.exit(1)


def is_excluded(path: str, exclude_patterns: list) -> bool:
    """Check if a path matches any exclude pattern."""
    for pattern in exclude_patterns:
        if fnmatch.fnmatch(path, pattern):
            return True
        # Also check each component
        parts = Path(path).parts
        for part in parts:
            if fnmatch.fnmatch(part, pattern.strip("*/")):
                return True
    return False


def camel_to_words(name: str) -> list:
    """Split CamelCase or PascalCase into words."""
    # Insert space before uppercase letters that follow lowercase
    words = re.sub(r"([a-z])([A-Z])", r"\1 \2", name)
    # Also handle consecutive uppercase (e.g., UIManager -> UI Manager)
    words = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1 \2", words)
    return [w.lower() for w in words.split() if w]


def infer_module_name(directory: str) -> str:
    """Infer module name from directory path (last component)."""
    return Path(directory).name


def infer_keywords(module_name: str) -> list:
    """Infer keywords from module name via camelCase splitting."""
    words = camel_to_words(module_name)
    keywords = list(set([module_name.lower()] + words))
    return sorted(keywords)


def infer_type(directory: str, rules: list) -> tuple:
    """Infer module type using type_inference_rules. Returns (type, confidence)."""
    for rule in rules:
        pattern = rule.get("pattern", "")
        # Convert glob pattern to fnmatch-compatible
        if fnmatch.fnmatch(directory, pattern) or fnmatch.fnmatch(
            directory + "/", pattern
        ):
            return rule.get("type", "Unknown"), rule.get("confidence", 0.5)
        # Try matching with path normalization
        norm_dir = directory.replace("\\", "/")
        norm_pattern = pattern.replace("\\", "/")
        if fnmatch.fnmatch(norm_dir, norm_pattern):
            return rule.get("type", "Unknown"), rule.get("confidence", 0.5)
    return "Business", 0.5


def generate_spec(template: str, metadata: dict) -> str:
    """Fill template with metadata."""
    content = template
    for key, value in metadata.items():
        if isinstance(value, list):
            value_str = json.dumps(value)
        else:
            value_str = str(value)
        content = content.replace(f"{{{{{key}}}}}", value_str)
    return content


def find_directories(
    scan_paths: list, exclude_patterns: list, project_root: str
) -> list:
    """Return each scan_path directory itself (not subdirectories)."""
    dirs = []
    for scan_path in scan_paths:
        # Normalize: strip trailing slashes
        scan_path = scan_path.rstrip("/\\")
        full_path = os.path.join(project_root, scan_path)
        if not os.path.exists(full_path):
            print(f"WARNING: Scan path does not exist: {full_path}", file=sys.stderr)
            continue
        if not os.path.isdir(full_path):
            print(
                f"WARNING: Scan path is not a directory: {full_path}", file=sys.stderr
            )
            continue
        rel_path = os.path.relpath(full_path, project_root)
        if is_excluded(rel_path, exclude_patterns):
            continue
        dirs.append(rel_path)

    return dirs


def main():
    parser = argparse.ArgumentParser(
        description="Initialize SPEC.md files for project modules"
    )
    parser.add_argument(
        "--config",
        default=CONFIG_PATH,
        help=f"Config file path (default: {CONFIG_PATH})",
    )
    parser.add_argument(
        "--template",
        default=TEMPLATE_PATH,
        help=f"Template file path (default: {TEMPLATE_PATH})",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Show what would be done without creating files",
    )
    parser.add_argument(
        "--project-root",
        default=".",
        help="Project root directory (default: current directory)",
    )
    args = parser.parse_args()

    project_root = os.path.abspath(args.project_root)
    config_path = os.path.join(project_root, args.config)
    template_path = os.path.join(project_root, args.template)

    config = load_config(config_path)
    template = load_template(template_path)

    scan_paths = config.get("scan_paths", [])
    exclude_patterns = config.get("exclude_patterns", [])
    spec_filename = config.get("spec_filename", "SPEC.md")
    type_rules = config.get("type_inference_rules", [])

    print(f"Scanning paths: {scan_paths}")
    print(f"Spec filename: {spec_filename}")
    print()

    directories = find_directories(scan_paths, exclude_patterns, project_root)

    created = 0
    skipped = 0
    errors = 0

    for directory in sorted(directories):
        spec_path = os.path.join(project_root, directory, spec_filename)

        if os.path.exists(spec_path):
            print(f"  SKIP  {directory} (SPEC.md already exists)")
            skipped += 1
            continue

        module_name = infer_module_name(directory)
        keywords = infer_keywords(module_name)
        module_type, confidence = infer_type(directory, type_rules)

        metadata = {
            "module_name": module_name,
            "directory": directory,
            "type": module_type,
            "keywords": keywords,
            "current_datetime": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
            "confidence": f"{confidence:.0%}",
        }

        content = generate_spec(template, metadata)

        if args.dry_run:
            print(f"  WOULD CREATE  {spec_path}")
            print(
                f"    module_name: {module_name}, type: {module_type} ({confidence:.0%} confidence)"
            )
            created += 1
        else:
            try:
                os.makedirs(os.path.dirname(spec_path), exist_ok=True)
                with open(spec_path, "w", encoding="utf-8") as f:
                    f.write(content)
                print(
                    f"  CREATE  {directory} → {spec_filename} (type={module_type}, conf={confidence:.0%})"
                )
                created += 1
            except OSError as e:
                print(f"  ERROR  {directory}: {e}", file=sys.stderr)
                errors += 1

    print()
    print("=" * 50)
    print(f"Summary: {created} created, {skipped} skipped, {errors} errors")
    if args.dry_run:
        print("(dry-run mode — no files were actually created)")


if __name__ == "__main__":
    main()
