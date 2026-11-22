"""Build the Flight Effects mod as a Sentinel payload (.spf) for the Project Edge launcher.

Usage:
  python tools/package_spf.py FlightEffects/bin/Release/net48/FlightEffects.dll [--out FlightEffects_0.1.1.spf]

The .spf is a ZIP with `payloadinfo` and clientmodifications/BepInEx/plugins/FlightEffects/FlightEffects.dll.
Install: copy it into <launcher>/payloads/ and enable "Flight Effects" in the launcher's payload manager.
"""
import argparse
import zipfile
from pathlib import Path

VERSION = "0.1.1"
PAYLOADINFO = (f"Type: Resource\nPayload-ID: httyd_flighteffects\nPayload-Version: {VERSION}\n"
               "Payload-Name: Flight Effects\nGame-ID: schoolofdragons\n")
DIRS = ("clientmodifications/", "clientmodifications/BepInEx/", "clientmodifications/BepInEx/plugins/",
        "clientmodifications/BepInEx/plugins/FlightEffects/")


def build_spf(dll: Path, out: Path) -> Path:
    out.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(out, "w", compression=zipfile.ZIP_DEFLATED) as z:
        z.writestr("payloadinfo", PAYLOADINFO)
        for d in DIRS:
            z.mkdir(d)
        z.write(dll, DIRS[-1] + "FlightEffects.dll")
    return out


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("dll", type=Path)
    ap.add_argument("--out", type=Path, default=Path(f"FlightEffects_{VERSION}.spf"))
    a = ap.parse_args()
    spf = build_spf(a.dll, a.out)
    print(f"wrote {spf} ({spf.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
