"""Wildtide asset pipeline on Hugging Face.

    python -m wildtide list --slice
    python -m wildtide concept cindlet-stage-1 --variations 4
    python -m wildtide slice
    python -m wildtide sheet turnaround-sheet --ref assets/creatures/cindlet-stage-1/concept_02.png --name cindlet
    python -m wildtide sheet clean-view-for-3d-model --ref <pick>.png --name cindlet
    python -m wildtide mesh assets/sheets/cindlet/clean-view-for-3d-model.png
    python -m wildtide video title-screen-loop
"""
from __future__ import annotations

import argparse
import json
import random
import re
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PROMPTS = ROOT / "prompts" / "prompts.json"
ASSETS = ROOT / "assets"

WIDE = (1344, 768)
SQUARE = (1024, 1024)
# Scenes read better wide; everything built around a single subject stays square.
WIDE_SECTIONS = ("1. Style tests", "6. World", "8. Interface")
SQUARE_IDS = {"look-test-creature-render", "element-icons", "status-icons", "buttons-and-panels-kit", "logo"}
WIDE_IDS = {"store-key-art", "capture-moment", "townsfolk-lineup"}
UI_SECTION = "8. Interface"


def load() -> list[dict]:
    if not PROMPTS.exists():
        sys.exit("prompts/prompts.json missing: run `python tools/extract_prompts.py` first")
    return json.loads(PROMPTS.read_text())["prompts"]


def find(entries: list[dict], pid: str) -> dict:
    for e in entries:
        if e["id"] == pid:
            return e
    close = [e["id"] for e in entries if pid in e["id"]]
    sys.exit(f"no prompt with id {pid!r}" + (f"; did you mean: {', '.join(close[:8])}" if close else ""))


def section_dir(entry: dict) -> str:
    return re.sub(r"^\d+\.\s*", "", entry["section"]).split(" (")[0].lower().replace(" ", "-")


def size_for(entry: dict) -> tuple[int, int]:
    if entry["id"] in SQUARE_IDS:
        return SQUARE
    if entry["id"] in WIDE_IDS or entry["section"] in WIDE_SECTIONS or entry["subsection"] == "Battle stages":
        return WIDE
    return SQUARE


def fill_slots(prompt: str, fills: list[str]) -> str:
    for f in fills:
        key, _, value = f.partition("=")
        prompt = re.sub(r"\[" + re.escape(key.strip()) + r"[^\]]*\]", value.strip(), prompt, flags=re.I)
    left = re.findall(r"\[[^\]]+\]", prompt)
    if left:
        sys.exit(f"unfilled slots {left}; pass --fill 'KEY=value' for each (KEY = start of the bracket text)")
    return prompt


def rel(path: Path) -> str:
    return str(path.relative_to(ROOT)) if path.is_relative_to(ROOT) else str(path)


def log(record: dict) -> None:
    """Append to assets/manifest.jsonl so every file can be traced to its prompt, model and seed."""
    ASSETS.mkdir(exist_ok=True)
    with (ASSETS / "manifest.jsonl").open("a") as f:
        f.write(json.dumps({"time": time.strftime("%Y-%m-%dT%H:%M:%S"), **record}) + "\n")


def next_index(folder: Path, stem: str) -> int:
    nums = [int(m[1]) for p in folder.glob(f"{stem}_*.png") if (m := re.search(r"_(\d+)\.png$", p.name))]
    return max(nums, default=0) + 1


# ---- commands --------------------------------------------------------------------------
def cmd_list(args) -> None:
    for e in load():
        if args.slice and not e["vertical_slice"]:
            continue
        if args.kind and e["kind"] != args.kind:
            continue
        if args.section and args.section.lower() not in e["section"].lower():
            continue
        flag = "*" if e["vertical_slice"] else " "
        print(f"{flag} {e['kind']:<5} {e['id']:<45} {e['section']}" + (f" / {e['element']}" if e["element"] else ""))


def concept(hf, entry: dict, args) -> list[Path]:
    from . import hf as backends

    if entry["kind"] != "image":
        sys.exit(f"{entry['id']} is an {entry['kind']} prompt; use `{'sheet' if entry['kind'] == 'edit' else 'video'}`")
    prompt = fill_slots(entry["prompt"], args.fill)
    model = args.model or (backends.UI_TEXT_TO_IMAGE_MODEL if entry["section"] == UI_SECTION else backends.TEXT_TO_IMAGE_MODEL)
    width, height = size_for(entry)
    folder = ASSETS / section_dir(entry) / entry["id"]
    folder.mkdir(parents=True, exist_ok=True)
    out = []
    start = next_index(folder, "concept")
    for i in range(args.variations):
        seed = args.seed + i if args.seed is not None else random.randint(0, 2**31 - 1)
        path = folder / f"concept_{start + i:02d}.png"
        print(f"  {entry['id']} #{start + i} seed={seed} ({model}, {width}x{height})", flush=True)
        if args.style_ref:
            # Kontext can't take a separate style image, so ask it to repaint the golden reference.
            img = hf.edit(Path(args.style_ref), "Keep this exact art style, rendering and lighting. Replace the "
                          "content with: " + prompt.replace(style_suffix(), "").strip(),
                          use_space=args.use_space)
            model_used = backends.EDIT_MODEL
        else:
            img = hf.text_to_image(prompt, model=model, width=width, height=height, seed=seed)
            model_used = model
        img.save(path)
        log({"id": entry["id"], "file": rel(path), "model": model_used, "seed": seed, "prompt": prompt})
        out.append(path)
    return out


def style_suffix() -> str:
    return json.loads(PROMPTS.read_text())["style_suffix"]


def cmd_concept(args) -> None:
    from .hf import HF

    hf, entries = HF(args.provider), load()
    for pid in args.ids:
        for p in concept(hf, find(entries, pid), args):
            print(f"    -> {rel(p)}")


def cmd_slice(args) -> None:
    from .hf import HF

    hf = HF(args.provider)
    todo = [e for e in load() if e["vertical_slice"] and e["kind"] == "image"]
    print(f"vertical slice: {len(todo)} prompts x {args.variations} variations")
    failed = []
    for e in todo:
        if list((ASSETS / section_dir(e) / e["id"]).glob("concept_*.png")) and not args.again:
            print(f"  skip {e['id']} (already has concepts; --again to add more)")
            continue
        try:
            concept(hf, e, args)
        except Exception as exc:  # keep going: one provider hiccup shouldn't kill a 100-image run
            print(f"  FAILED {e['id']}: {exc}", file=sys.stderr)
            failed.append(e["id"])
    if failed:
        sys.exit(f"{len(failed)} failed: {' '.join(failed)}  (re-run `slice` to retry just these)")


def cmd_sheet(args) -> None:
    from .hf import HF, stitch

    entry = find(load(), args.id)
    if entry["kind"] != "edit":
        sys.exit(f"{entry['id']} doesn't use a reference image; use `concept`")
    refs = [Path(r) for r in args.ref]
    prompt = fill_slots(entry["prompt"], args.fill)
    folder = ASSETS / "sheets" / args.name
    folder.mkdir(parents=True, exist_ok=True)
    ref = refs[0]
    if len(refs) > 1:  # e.g. evolution lineup: give the editor every stage in one image
        ref = folder / f"{entry['id']}_refs.png"
        stitch(refs).save(ref)
    hf = HF(args.provider)
    for i in range(args.variations):
        path = folder / (f"{entry['id']}.png" if args.variations == 1 else f"{entry['id']}_{i + 1:02d}.png")
        print(f"  {entry['id']} for {args.name} #{i + 1}", flush=True)
        hf.edit(ref, prompt, use_space=args.use_space).save(path)
        log({"id": entry["id"], "name": args.name, "file": rel(path), "refs": args.ref, "prompt": prompt})
        print(f"    -> {rel(path)}")


def cmd_cutout(args) -> None:
    from .hf import cutout

    for src in map(Path, args.images):
        out = src.with_name(src.stem + "_cutout.png")
        cutout(src, out)
        log({"id": "cutout", "file": str(out), "source": str(src)})
        print(f"  -> {out}")


def cmd_mesh(args) -> None:
    from .hf import mesh

    for src in map(Path, args.images):
        out = src.with_suffix(".glb") if args.backend == "trellis" else src.with_name(f"{src.stem}_{args.backend}.glb")
        print(f"  {src} -> 3D via {args.backend} (takes a minute or two)", flush=True)
        mesh(src, out, backend=args.backend, seed=args.seed)
        log({"id": "mesh", "file": str(out), "source": str(src), "backend": args.backend})
        print(f"    -> {out}  (check topology, UVs and poly count in Blender)")


def cmd_video(args) -> None:
    from .hf import HF

    entry = find(load(), args.id)
    if entry["kind"] != "video":
        sys.exit(f"{entry['id']} is not a video prompt")
    prompt = fill_slots(entry["prompt"], args.fill)
    needs_frame = "attached image" in prompt or "first frame" in prompt
    if needs_frame and not args.frame:
        sys.exit(f"{entry['id']} animates an existing image: pass --frame path/to/image.png")
    folder = ASSETS / "video"
    folder.mkdir(parents=True, exist_ok=True)
    name = f"{entry['id']}" + (f"_{args.name}" if args.name else "")
    path = folder / f"{name}_{next_index(folder, name):02d}.mp4"
    hf = HF(args.provider)
    print(f"  {entry['id']} (video generation can take several minutes)", flush=True)
    data = hf.image_to_video(Path(args.frame), prompt, seed=args.seed) if args.frame else hf.text_to_video(prompt, seed=args.seed)
    path.write_bytes(data)
    log({"id": entry["id"], "file": rel(path), "frame": args.frame, "prompt": prompt})
    print(f"    -> {rel(path)}")


def main(argv: list[str] | None = None) -> None:
    p = argparse.ArgumentParser(prog="wildtide", description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--provider", default="auto", help="HF Inference Provider (auto, fal-ai, replicate, together, ...)")
    sub = p.add_subparsers(dest="cmd", required=True)

    s = sub.add_parser("list", help="list prompt ids")
    s.add_argument("--slice", action="store_true", help="only the vertical slice")
    s.add_argument("--kind", choices=["image", "edit", "video"])
    s.add_argument("--section")
    s.set_defaults(fn=cmd_list)

    def gen_opts(s, variations: int):
        s.add_argument("--variations", "-n", type=int, default=variations)
        s.add_argument("--seed", type=int, help="first seed; variations use seed+1, seed+2 ...")
        s.add_argument("--fill", action="append", default=[], metavar="KEY=VALUE", help="fill a [BRACKET] slot")
        s.add_argument("--use-space", action="store_true", help="run edits on a free ZeroGPU Space instead of a provider")

    s = sub.add_parser("concept", help="text-to-image concepts for one or more prompt ids")
    s.add_argument("ids", nargs="+")
    s.add_argument("--model", help="override the text-to-image model")
    s.add_argument("--style-ref", help="golden reference image to match (uses FLUX Kontext)")
    gen_opts(s, 4)
    s.set_defaults(fn=cmd_concept)

    s = sub.add_parser("slice", help="concepts for the whole vertical slice")
    s.add_argument("--model")
    s.add_argument("--style-ref")
    s.add_argument("--again", action="store_true", help="add variations even where concepts exist")
    gen_opts(s, 4)
    s.set_defaults(fn=cmd_slice)

    s = sub.add_parser("sheet", help="run a 'Using the attached ...' prompt on your picked design")
    s.add_argument("id")
    s.add_argument("--ref", action="append", required=True, help="reference image (repeat for evolution lineup)")
    s.add_argument("--name", required=True, help="asset name, e.g. cindlet")
    gen_opts(s, 1)
    s.set_defaults(fn=cmd_sheet)

    s = sub.add_parser("cutout", help="remove backgrounds (icons, UI, 3D inputs)")
    s.add_argument("images", nargs="+")
    s.set_defaults(fn=cmd_cutout)

    s = sub.add_parser("mesh", help="image to textured GLB")
    s.add_argument("images", nargs="+")
    s.add_argument("--backend", choices=["trellis", "hunyuan"], default="trellis")
    s.add_argument("--seed", type=int, default=0)
    s.set_defaults(fn=cmd_mesh)

    s = sub.add_parser("video", help="trailer shots and animation reference")
    s.add_argument("id")
    s.add_argument("--frame", help="start frame for image-to-video")
    s.add_argument("--name", help="suffix for the output file, e.g. cindlet")
    s.add_argument("--seed", type=int)
    s.add_argument("--fill", action="append", default=[], metavar="KEY=VALUE")
    s.set_defaults(fn=cmd_video)

    args = p.parse_args(argv)
    args.fn(args)


if __name__ == "__main__":
    main()
