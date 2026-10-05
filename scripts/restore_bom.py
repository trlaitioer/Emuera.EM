import sys
from pathlib import Path


def has_bom(file: Path) -> bool:
    with file.open("rb") as f:
        bom = f.read(3)
        return bom == b"\xef\xbb\xbf"


def add_bom(file: Path) -> bool:
    try:
        with file.open("r", encoding="utf-8", newline="") as f:
            content = f.read()
        with file.open("w", encoding="utf-8-sig", newline="") as f:
            f.write(content)
        return True
    except (OSError, UnicodeDecodeError) as e:
        print(f"failed to add BOM to {file}", e)
    return False


def handle_project(project: Path, exclude_paths: list[Path]) -> bool:
    any_failed = False
    for file in project.glob("**/*.cs"):
        should_skip = False
        for exclude_path in exclude_paths:
            if file.is_relative_to(exclude_path):
                should_skip = True
                break
        if should_skip or has_bom(file):
            continue
        if add_bom(file):
            print(f"added BOM to {file}")
        else:
            any_failed = True
    return any_failed


def main() -> bool:
    project_root = Path(__file__).resolve().parent.parent

    main_project = project_root.joinpath("Emuera")
    artifacts = main_project.joinpath("artifacts")

    test_project = project_root.joinpath("Emuera.Tests")
    test_bin = test_project.joinpath("bin")
    test_obj = test_project.joinpath("obj")

    any_failed = handle_project(main_project, [artifacts])
    any_failed |= handle_project(test_project, [test_bin, test_obj])
    return any_failed


if __name__ == "__main__":
    any_failed = False
    if len(sys.argv) > 1:
        for file in sys.argv[1:]:
            path = Path(file)
            if not path.is_file() or has_bom(path):
                continue
            if add_bom(path):
                print(f"added BOM to {file}")
            else:
                any_failed = True
    else:
        any_failed = main()

    if any_failed:
        sys.exit(1)
