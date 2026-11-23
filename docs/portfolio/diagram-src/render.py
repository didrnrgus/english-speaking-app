"""HTML 다이어그램 → Edge headless 스크린샷 → 여백 자르기 → JPG."""
import re
import subprocess
import sys
import tempfile
import time
from pathlib import Path

from PIL import Image, ImageChops

EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
SRC = Path(__file__).parent
OUT = Path(sys.argv[1])
names = sys.argv[2:] or [p.stem for p in sorted(SRC.glob("*.html"))]
OUT.mkdir(parents=True, exist_ok=True)

for name in names:
    html = SRC / f"{name}.html"
    width = int(re.search(r"--w:\s*(\d+)px", html.read_text(encoding="utf-8")).group(1))
    png = SRC / f"{name}.png"
    png.unlink(missing_ok=True)
    subprocess.run([
        EDGE, "--headless=new", f"--user-data-dir={Path(tempfile.gettempdir()) / ("edge-profile-" + name)}", "--disable-gpu", "--hide-scrollbars",
        "--force-device-scale-factor=2", f"--window-size={width},2600",
        f"--screenshot={png}", html.as_uri(),
    ], check=True, capture_output=True)
    for _ in range(120):  # Edge 런처가 먼저 끝나고 자식 프로세스가 나중에 파일을 쓴다
        if png.exists() and png.stat().st_size > 0:
            time.sleep(0.5)
            break
        time.sleep(0.5)
    img = Image.open(png).convert("RGB")
    bg = Image.new("RGB", img.size, (255, 255, 255))
    bbox = ImageChops.difference(img, bg).getbbox()
    pad = 80
    l, t, r, b = bbox
    img = img.crop((0, 0, img.width, min(img.height, b + pad)))
    out = OUT / f"{name}.jpg"
    img.save(out, "JPEG", quality=92, subsampling=0)
    print(out, img.size, "content right edge:", r, "/", img.width)
