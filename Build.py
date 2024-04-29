from os import chdir, system as os_system
from pathlib import Path
from shutil import copyfile


def main() -> None:
    rust_src_path = Path(__file__).parent.joinpath("WindowCapture", "window_capture")
    rust_dll_path = rust_src_path.joinpath("target", "release", "window_capture.dll")
    cs_dll_path = Path(__file__).parent.joinpath("WindowCapture", rust_dll_path.name)
    cs_src_path = cs_dll_path.with_name("WindowCapture.cs")

    start_cwd = Path.cwd()
    chdir(str(rust_src_path))
    os_system("cargo build --release")
    chdir(str(start_cwd))

    os_system(f"rnet-gen {rust_dll_path} > {cs_src_path}")

    copyfile(rust_dll_path, cs_dll_path)


if __name__ == "__main__":
    main()
