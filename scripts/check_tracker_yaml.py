# /// script
# requires-python = ">=3.14"
# dependencies = ["pyyaml"]
# ///


import datetime
import io
import re
import sys
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parent.parent
TRACKER = ROOT / "tracker"

ISSUE_STATUSES = {
    "draft",
    "ready",
    "in-progress",
    "need-check",
    "implemented",
    "wontfix",
    "superseded",
}
SPEC_STATUSES = {"draft", "ready", "in-progress", "implemented", "wontfix"}
TYPES = {"task", "bug"}

ISSUE_KEY_ORDER = ["id", "status", "type", "blocked_by", "superseded_by"]
ISSUE_REQUIRED_KEYS = {"id", "status"}
SPEC_KEY_ORDER = ["date", "status"]
SPEC_REQUIRED_KEYS = {"date", "status"}

ISSUE_NAME_PATTERN = re.compile(r"\d{2}\-[\w-]+")
DATE_PATTERN = re.compile(r"\d{4}-\d{2}-\d{2}")


def has_bom(file: Path) -> bool:
    with file.open("rb") as f:
        bom = f.read(3)
        return bom == b"\xef\xbb\xbf"


def check_spec(data: dict) -> list[str]:
    errors: list[str] = []
    for key in SPEC_REQUIRED_KEYS:
        if key not in data:
            errors.append(f"spec 缺少必需的键 {key}")

    last_order = -1
    for key in data:
        if key not in SPEC_KEY_ORDER:
            errors.append(f"未知的键 {key}")
            continue
        order = SPEC_KEY_ORDER.index(key)
        if order < last_order:
            errors.append(
                f"spec 键的顺序不正确，{key} 应在 {SPEC_KEY_ORDER[last_order]} 之前"
            )
        last_order = order

    for k, v in data.items():
        match k:
            case "date":
                if isinstance(v, datetime.date):
                    pass
                elif not DATE_PATTERN.fullmatch(v):
                    errors.append("spec date 格式不正确，应为 YYYY-MM-DD")
            case "status":
                if v not in SPEC_STATUSES:
                    errors.append(f"未知的状态 {v}")
    return errors


def check_issue(data: dict, issue_id: str, issue_ids: set[str]) -> list[str]:
    errors: list[str] = []

    for key in ISSUE_REQUIRED_KEYS:
        if key not in data:
            errors.append(f"issue 缺少必需的键 {key}")

    last_order = -1
    for key in data:
        if key not in ISSUE_KEY_ORDER:
            errors.append(f"未知的键 {key}")
            continue
        order = ISSUE_KEY_ORDER.index(key)
        if order < last_order:
            errors.append(
                f"issue 键的顺序不正确，{key} 应在 {ISSUE_KEY_ORDER[last_order]} 之前"
            )
        last_order = order

    for k, v in data.items():
        match k:
            case "id":
                if v != issue_id:
                    errors.append(f"issue id 错误，应为 {issue_id}，实际为 {v}")
            case "status":
                if v not in ISSUE_STATUSES:
                    errors.append(f"未知的状态 {v}")
                elif v == "superseded":
                    if "superseded_by" not in data:
                        errors.append("状态为 superseded 时，应包含 superseded_by 键")
                elif "superseded_by" in data:
                    errors.append("状态不为 superseded 时，不应包含 superseded_by 键")
            case "type":
                if v not in TYPES:
                    errors.append(f"未知的类型 {v}")
            case "blocked_by" | "superseded_by":
                if not isinstance(v, list):
                    errors.append(f"{k} 应为列表")
                else:
                    for i in v:
                        if i not in issue_ids:
                            errors.append(f"{k} 中的 {i} 不是有效的 issue id")
    return errors


def check_file(path: Path, issue_ids: set[str]) -> list[str]:
    if not path.is_relative_to(TRACKER):
        return []

    relative_path = path.relative_to(TRACKER)

    if has_bom(path):
        return [f"{relative_path} 编码为 utf-8-bom"]

    feature_slug = relative_path.parts[0]
    is_spec = path.name == "spec.md"
    is_issue = not is_spec and ISSUE_NAME_PATTERN.fullmatch(path.stem)

    if not is_spec and not is_issue:
        return []

    try:
        with path.open("r", encoding="utf-8") as f:
            if f.readline().strip("\r\n") != "---":
                return [f"{relative_path} 文档开头缺少 YAML front matter"]
            lines = []
            end = False
            for line in f:
                if line.strip("\r\n") != "---":
                    lines.append(line)
                else:
                    end = True
                    break
            if not end:
                return [f"{relative_path} 缺少 YAML front matter 结束标记"]
            data = yaml.safe_load("\n".join(lines))
    except Exception as e:
        return [f"无法解析 YAML: {e}"]

    if not isinstance(data, dict):
        return [f"{relative_path} YAML front matter 应为键值对"]

    if is_spec:
        return check_spec(data)
    else:
        issue_id = f"{feature_slug}-{path.stem[:2]}"
        return check_issue(data, issue_id, issue_ids)


def build_index() -> tuple[list[str], set[str]]:
    errors: list[str] = []
    issue_ids: set[str] = set()
    for path in TRACKER.glob("**/*.md"):
        relative_path = path.relative_to(TRACKER)
        feature_slug = relative_path.parts[0]
        if path.name == "spec.md":
            continue
        if ISSUE_NAME_PATTERN.fullmatch(path.stem):
            issue_id = f"{feature_slug}-{path.stem[:2]}"
            if issue_id in issue_ids:
                errors.append(f"重复的 issue id: {issue_id}")
            else:
                issue_ids.add(issue_id)
    return errors, issue_ids


def main() -> int:
    errors, issue_ids = build_index()
    any_error = len(errors) > 0
    if errors:
        print("== 构建索引时发现以下错误 ==")
        print("\n".join(errors))

    if len(sys.argv) > 1:
        for path in sys.argv[1:]:
            errors = check_file(ROOT / Path(path), issue_ids)
            if errors:
                any_error = True
                print(f"== {path} ==")
                print("\n".join(errors))
    else:
        for path in TRACKER.glob("**/*.md"):
            errors = check_file(path, issue_ids)
            if errors:
                any_error = True
                print(f"== {path.relative_to(ROOT)} ==")
                print("\n".join(errors))
    return 1 if any_error else 0


if __name__ == "__main__":
    for stream in (sys.stdout, sys.stderr):
        if isinstance(stream, io.TextIOWrapper):
            stream.reconfigure(encoding="utf-8")
    sys.exit(main())
