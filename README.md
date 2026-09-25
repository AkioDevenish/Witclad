# Wildtide on Hugging Face

> **The game lives in [`unity/Wildtide`](unity/Wildtide/README.md):** a Unity 6 vertical slice for iOS and Android.
> This page covers the art pipeline that feeds it.

The Wildtide asset prompt library (161 prompts, 11 sections), plus a pipeline that turns
those prompts into assets with open models on Hugging Face instead of (or alongside) Higgsfield.

| Library step | Higgsfield (from the doc) | Hugging Face (this repo) |
|---|---|---|
| Concepts (135 prompts) | Nano Banana Pro / Soul 2.0 | **FLUX.1 Krea [dev]**; **Qwen-Image** for UI mockups |
| Model sheets, variants, icons ("Using the attached…", 14 prompts) | Nano Banana Pro + reference | **FLUX.1 Kontext [dev]** image editing |
| Icon / UI cutouts | manual | **BiRefNet** background removal Space |
| Image → 3D mesh | Higgsfield 3D Model | **TRELLIS** Space (MIT) or **Hunyuan3D-2.1** Space |
| Video (12 prompts) | Kling / Seedance / Veo | **Wan 2.2** text-to-video and image-to-video |

## Setup

```bash
python -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt
export HF_TOKEN=hf_...   # huggingface.co/settings/tokens, "Make calls to Inference Providers" enabled
```

Image, edit and video calls go through [Inference Providers](https://huggingface.co/docs/inference-providers)
and bill to your HF account (a PRO account includes monthly credit). Cutouts and meshes run on free
ZeroGPU Spaces; logging in with a token gives you a bigger GPU quota there.

## Workflow (follows the library's "How to use this")

```bash
python -m wildtide list --slice                  # * = in the vertical slice

# 1. Lock the look: 4-8 variations of each style test, pick your golden reference
python -m wildtide concept look-test-hero-scene look-test-creature-render look-test-playable-area -n 6

# 2. Concepts: everything in the vertical slice, 4 variations each (29 prompts, ~116 images)
python -m wildtide slice --style-ref assets/style-tests/look-test-creature-render/concept_03.png
python -m wildtide concept rimehare -n 4 --seed 100   # one-offs; seeds are logged for re-rolls

# 3. Model sheet from the concept you picked
python -m wildtide sheet turnaround-sheet        --ref assets/creatures/cindlet-stage-1/concept_02.png --name cindlet
python -m wildtide sheet clean-view-for-3d-model --ref assets/creatures/cindlet-stage-1/concept_02.png --name cindlet
python -m wildtide sheet rare-color-variant       --ref assets/creatures/cindlet-stage-1/concept_02.png --name cindlet \
    --fill "NEW PALETTE=silver-blue with pale gold accents"
python -m wildtide sheet evolution-lineup --name cindlet \
    --ref cindlet.png --ref scorchscale.png --ref pyrangol.png   # stitched into one reference

# 4. Mesh: feed the clean view, not the concept
python -m wildtide mesh assets/sheets/cindlet/clean-view-for-3d-model.png           # TRELLIS
python -m wildtide mesh assets/sheets/cindlet/clean-view-for-3d-model.png --backend hunyuan

# 5. 2D last: cut icons and UI off their flat backgrounds
python -m wildtide cutout assets/sheets/cindlet/party-and-codex-icon.png

# 6. Into the game: concept cutouts show as billboards, GLBs replace the placeholder shapes
python -m wildtide unity cindlet-stage-1 assets/creatures/cindlet-stage-1/concept_02_cutout.png

# Video
python -m wildtide video title-screen-loop
python -m wildtide video idle-reference --frame assets/sheets/cindlet/clean-view-for-3d-model.png --name cindlet
```

Output lands in `assets/<section>/<prompt-id>/` and every file is logged in `assets/manifest.jsonl`
with its prompt, model and seed. `assets/` is git-ignored; commit the picks you keep.

Options: `--provider fal-ai` (or `replicate`, `together`, …) pins a provider; `--model` swaps the
text-to-image model; `--use-space` runs Kontext edits on a free ZeroGPU Space; `--fill KEY=value` fills
`[BRACKET]` slots (the pipeline refuses to send a prompt with an empty slot).

## Things that differ from the Higgsfield workflow

- **Golden reference.** FLUX can't take a separate style image the way Nano Banana does. `--style-ref`
  asks Kontext to repaint your golden reference with the new subject, which holds the look well but can
  pull composition from the reference too. Compare against plain text-to-image, which already carries the
  style suffix. For 150+ assets, the more robust route is training a FLUX LoRA on your 15-30 favourite picks
  (HF AutoTrain or a LoRA trainer Space) and passing it with `--model`.
- **Evolution sequence video** wants a start and an end frame. Wan 2.2 image-to-video takes only the start
  frame here; the `mcp-tools/wan-2-2-first-last-frame` Space does both if you need it.
- **Rigging and animation** have no good open equivalent yet; keep the Blender step from the library.

## Licences to check before shipping

- FLUX.1 [dev] models (Krea, Kontext): non-commercial weights licence, but outputs may be used commercially.
  Read BFL's licence for the current terms.
- **Hunyuan3D-2.1**: Tencent Hunyuan non-commercial licence, and it excludes the EU, UK and South Korea.
  Use TRELLIS (MIT, the default) for anything you plan to sell.
- BiRefNet (MIT), Qwen-Image (Apache 2.0), Wan 2.2 (Apache 2.0).

## Updating prompts

`prompts/wildtide_prompt_library.html` is the library as exported from the artifact. Edit it (or drop in a
new export) and rebuild the JSON:

```bash
python tools/extract_prompts.py
```
