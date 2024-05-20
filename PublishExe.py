from argparse import ArgumentParser
from os import system as os_system
from re import match as re_match


SEMVER_REGEX = r"^(?P<major>0|[1-9]\d*)\.(?P<minor>0|[1-9]\d*)\.(?P<patch>0|[1-9]\d*)(?:-(?P<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+(?P<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$"


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

    cmd = " ".join([
        "dotnet",
        "publish",
        "-c",
        "Release",
        "-r",
        "win-x64", # Only AMD64 Windows will be supported officially
        f'/p:Version="{version}"'
    ])

    os_system(cmd)


if __name__ == "__main__":
    main()
