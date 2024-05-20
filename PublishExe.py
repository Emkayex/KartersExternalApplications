from argparse import ArgumentParser
from os import system as os_system
from pathlib import Path
from re import match as re_match
from shutil import make_archive
from tempfile import TemporaryDirectory


SEMVER_REGEX = r"^(?P<major>0|[1-9]\d*)\.(?P<minor>0|[1-9]\d*)\.(?P<patch>0|[1-9]\d*)(?:-(?P<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+(?P<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$"
CS_PROJ = Path(__file__).parent.joinpath("CNKStyleBoostBar", "CNKStyleBoostBar.csproj")
RID = "win-x64" # Only AMD64 Windows will be supported officially


def main() -> None:
    # Create the argument parser
    parser = ArgumentParser()
    parser.add_argument("version", help = "The version of the application to publish following semantic versioning.")
    args = parser.parse_args()

    # Get the version number and verify it follows semver
    version = str(args.version)
    match = re_match(SEMVER_REGEX, version)
    if match is None:
        raise RuntimeError(f"Invalid version number: {version}")

    # Build the output to a temporary directory and then ZIP all the executable contents
    with TemporaryDirectory() as td:
        cmd = " ".join([
            "dotnet",
            "publish",
            "-c",
            "Release",
            "-r",
            RID,
            "-o",
            f'"{td}"',
            f'/p:Version="{version}"',
            f'"{CS_PROJ}"'
        ])

        os_system(cmd)
        make_archive(f"{CS_PROJ.stem}-{RID}", "zip", td)


if __name__ == "__main__":
    main()
