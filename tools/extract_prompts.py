"""Turn the Wildtide prompt library (HTML export of the artifact) into prompts/prompts.json.

Usage: python tools/extract_prompts.py [prompts/wildtide_prompt_library.html]
"""
import html
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "prompts" / "wildtide_prompt_library.html"
OUT = ROOT / "prompts" / "prompts.json"

STYLE_SUFFIX = (
    "Stylized 3D game art, soft cel-shading over hand-painted textures, chunky readable silhouettes, "
    "rounded appealing proportions, saturated harmonious colors, warm key light with cool rim light, "
    "soft ambient occlusion, high detail. No text, no logos, no watermark."
)
ELEMENTS = ["Flame", "Tide", "Verdant", "Storm", "Stone", "Frost", "Gale", "Venom", "Spirit", "Iron", "Lumen", "Umbra"]

# Assets in the doc's recommended vertical slice.
VERTICAL_SLICE = {
    "look-test-hero-scene", "look-test-creature-render", "look-test-playable-area",
    "cindlet-stage-1", "scorchscale-stage-2", "pyrangol-stage-3",
    "narlet-stage-1", "tuskwave-stage-2", "narvalor-stage-3",
    "budbara-stage-1", "grovebara-stage-2", "elderbara-stage-3",
    "kilnpup", "glasscrab", "capfrog", "stratojel", "geodig", "rimehare",
    "veyrath-guardian-of-the-sea", "player-a", "player-b", "rival-kade",
    "mentor-archivist-oma-rell", "warden-forja", "brightcove-starter-town",
    "windmill-meadows-route-1", "warden-trial-arena-template", "battle-screen",
    "bond-lanterns-5-tiers",
}


def text(fragment: str) -> str:
    return html.unescape(re.sub(r"<[^>]+>", "", fragment)).strip()


def slugify(s: str) -> str:
    return re.sub(r"[^a-z0-9]+", "-", s.lower()).strip("-")


def kind_for(section: str, model: str, prompt: str) -> str:
    if section.startswith("10."):
        return "video"
    if prompt.startswith("Using the attached") or "chosen design attached" in model:
        return "edit"  # needs a reference image
    return "image"


def main() -> None:
    doc = SRC.read_text(encoding="utf-8")
    body = doc[doc.index("<h2>1. Style tests</h2>"):]
    # Tokens: headings, prompt headers (<p><strong>...), and code blocks.
    token_re = re.compile(
        r"<h2>(?P<h2>.*?)</h2>|<h3>(?P<h3>.*?)</h3>|<p><strong>(?P<title>.*?)</strong>(?P<after>.*?)</p>"
        r"|<pre><code[^>]*>(?P<code>.*?)</code></pre>",
        re.S,
    )
    section = subsection = ""
    pending = None
    entries, seen = [], {}
    for m in token_re.finditer(body):
        if m["h2"]:
            section, subsection = text(m["h2"]), ""
        elif m["h3"]:
            subsection = text(m["h3"])
        elif m["title"] is not None:
            after = m["after"] or ""
            model = text(re.search(r"<em>(.*?)</em>", after, re.S)[1]) if "<em>" in after else ""
            model = model.removeprefix("Model: ")
            head = text(after.split("<br>")[0]) if "<br>" in after else ""
            note = text(re.sub(r"<em>.*?</em>", "", after.split("<br>", 1)[1], flags=re.S)) if "<br>" in after else ""
            element = next((e for e in ELEMENTS if f"({e})" in head), None)
            pending = {"title": text(m["title"]), "element": element, "model_hint": model, "note": note}
        elif m["code"] is not None and pending:
            prompt = text(m["code"])
            slug = slugify(pending["title"])
            if slug in seen:  # e.g. "Clean view for 3D Model" appears per section
                slug = f"{slug}-{slugify(section.split('. ', 1)[-1])}"
            seen[slug] = True
            entries.append({
                "id": slug,
                "section": section,
                "subsection": subsection,
                **pending,
                "kind": kind_for(section, pending["model_hint"], prompt),
                "has_style_suffix": STYLE_SUFFIX in prompt,
                "has_slots": bool(re.search(r"\[[A-Z0-9 ,.'\-]+", prompt)),
                "vertical_slice": slug in VERTICAL_SLICE,
                "prompt": prompt,
            })
            pending = None

    missing = VERTICAL_SLICE - {e["id"] for e in entries}
    if missing:
        print(f"warning: vertical-slice ids not found: {sorted(missing)}", file=sys.stderr)
    OUT.write_text(json.dumps({"style_suffix": STYLE_SUFFIX, "prompts": entries}, indent=2, ensure_ascii=False) + "\n")
    by_kind = {}
    for e in entries:
        by_kind[e["kind"]] = by_kind.get(e["kind"], 0) + 1
    print(f"wrote {len(entries)} prompts to {OUT.relative_to(ROOT)} {by_kind}")


if __name__ == "__main__":
    main()
