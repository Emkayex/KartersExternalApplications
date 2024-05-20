from dataclasses import dataclass
from json import loads
from pathlib import Path
from typing import Any, Iterable, cast
from urllib.error import HTTPError
from urllib.request import urlopen

from bs4 import BeautifulSoup # pip install beautifulsoup4
from striprtf.striprtf import rtf_to_text # pip install striprtf


RUST_SRC = Path(__file__).parent.joinpath("WindowCapture", "window_capture", "THIRDPARTY.json")
CS_SRC = Path(__file__).parent.joinpath("CNKStyleBoostBar", "THIRDPARTY.json")

LICENSE_PREFIXES = ("LICENSE", "LICENCE")
LICENSE_PREFIXES = LICENSE_PREFIXES + tuple(prefix.lower() for prefix in LICENSE_PREFIXES)
LICENSE_PREFIXES = tuple(set(LICENSE_PREFIXES + tuple(prefix.title() for prefix in LICENSE_PREFIXES)))

LICENSE_SUFFIXES = ("", ".md", ".txt")

BRANCH_NAMES = ("master", "main")


def change_github_url_to_raw_url(url: str) -> str:
    to_replace = "https://github.com/"
    replacement = "https://raw.githubusercontent.com/"
    return url.replace(to_replace, replacement).replace("/blob/", "/")


def iterate_possible_license_names(repo_url: str) -> Iterable[str]:
    for prefix in LICENSE_PREFIXES:
        for suffix in LICENSE_SUFFIXES:
            for branch_name in BRANCH_NAMES:
                base_url = f"{repo_url}/{branch_name}/{prefix}{suffix}"
                new_url = change_github_url_to_raw_url(base_url)
                yield new_url


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


def try_find_cs_license_text(repo_url: str) -> str:
    for possible_license_url in iterate_possible_license_names(repo_url):
        try:
            resp = urlopen(possible_license_url)
        except HTTPError as e:
            continue

        return resp.read().decode()

    return ""


def get_cs_library_infos() -> Iterable[LibraryInfo]:
    all_lib_info = loads(CS_SRC.read_text())
    for _obj in all_lib_info:
        obj = cast(dict[str, Any], _obj)

        name = str(obj["PackageName"])
        version = str(obj["PackageVersion"])
        license = str(obj["LicenseType"])

        # No attribution required for MS-EULA libraries (System.* C# libraries)
        if license.upper() == "MS-EULA":
            continue

        license_url = str(obj.get("LicenseUrl", ""))
        repo_info = cast(dict[str, Any], obj["Repository"])
        repo_url = str(repo_info["Url"]).removesuffix(".git")

        text = ""
        if repo_url.startswith("http"):
            text = try_find_cs_license_text(repo_url)

        if (len(text) <= 0) and (len(license_url) > 0):
            new_url = change_github_url_to_raw_url(license_url)
            try:
                data = cast(bytes, urlopen(new_url).read())
            except HTTPError:
                print(f"WARNING: Error while getting license text for {name} ({license_url})")

            try:
                text = data.decode("utf-8")
            except UnicodeDecodeError:
                text = data.decode("latin-1")

        # This doesn't require any attribution like the MS-EULA (more Microsoft libraries)
        if "MICROSOFT SOFTWARE LICENSE TERMS" in text:
            continue

        if len(text) <= 0:
            print(f"WARNING: Could not find license text for {name} ({repo_url})")
            continue

        if "<div" in text:
            soup = BeautifulSoup(text)
            text = soup.get_text()

        elif r"{\rtf" in text:
            text = str(rtf_to_text(text))

        yield LibraryInfo(
            name = name,
            version = version,
            license = license,
            license_infos = [
                LicenseInfo(
                    identifier = license,
                    text = text
                )
            ]
        )


def write_license_infos(output_path: Path, *lib_infos_iterable: Iterable[LibraryInfo]) -> None:
    separator = "\n" + ("#" * 80) + "\n\n"

    with open(output_path, "w", errors = "ignore") as f:
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
        get_cs_library_infos()
    )


if __name__ == "__main__":
    main()
