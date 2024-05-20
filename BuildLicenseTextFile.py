from dataclasses import dataclass
from json import loads
from pathlib import Path
from typing import Any, Iterable, cast


RUST_SRC = Path(__file__).parent.joinpath("WindowCapture", "window_capture", "THIRDPARTY.json")
CS_SRC = Path(__file__).parent.joinpath("CNKStyleBoostBar", "THIRDPARTY.json")


@dataclass
class LicenseInfo:
    identifier: str
    text: str


@dataclass
class LibraryInfo:
    name: str
    version: str
    license: str
    license_infos: list[LicenseInfo]


def get_rust_library_infos() -> Iterable[LibraryInfo]:
    all_lib_info = loads(RUST_SRC.read_text())["third_party_libraries"]
    for _obj in all_lib_info:
        obj = cast(dict[str, Any], _obj)

        name = str(obj["package_name"])
        version = str(obj["package_version"])
        license = str(obj["license"])

        license_infos: list[LicenseInfo] = []
        for _license_obj in obj["licenses"]:
            license_obj = cast(dict[str, Any], _license_obj)

            info = LicenseInfo(
                identifier = license_obj["license"],
                text = license_obj["text"]
            )
            license_infos.append(info)

        yield LibraryInfo(
            name = name,
            version = version,
            license = license,
            license_infos = license_infos
        )


def get_cs_library_infos() -> Iterable[LibraryInfo]:
    pass


def write_license_infos(output_path: Path, *lib_infos_iterable: Iterable[LibraryInfo]) -> None:
    separator = "\n" + ("#" * 80) + "\n\n"

    with open(output_path, "w") as f:
        f.write(separator[1:])

        for lib_infos in lib_infos_iterable:
            for lib_info in lib_infos:
                f.write(f"{lib_info.name} ({lib_info.version})\n")
                f.write(f"{lib_info.license}\n\n")
                for license_info in lib_info.license_infos:
                    f.write(f"{license_info.identifier}\n")
                    f.write(f"{license_info.text}\n")

                f.write(separator)


def main() -> None:
    write_license_infos(
        CS_SRC.parent.joinpath("THIRDPARTY.txt"),
        get_rust_library_infos(),
        # get_cs_library_infos()
    )


if __name__ == "__main__":
    main()
