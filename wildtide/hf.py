"""Hugging Face backends for the Wildtide asset pipeline.

Everything here runs on Hugging Face: Inference Providers (via ``InferenceClient``) for
text-to-image, image editing and video, and public Spaces (via ``gradio_client``) for
background removal and image-to-3D. Set ``HF_TOKEN`` in your environment first.
"""
from __future__ import annotations

import os
import shutil
from dataclasses import dataclass
from pathlib import Path

from PIL import Image

# Defaults. Every one can be overridden from the CLI.
TEXT_TO_IMAGE_MODEL = "black-forest-labs/FLUX.1-Krea-dev"  # best painterly concept art
UI_TEXT_TO_IMAGE_MODEL = "Qwen/Qwen-Image"                 # better layout/text for UI mockups
EDIT_MODEL = "black-forest-labs/FLUX.1-Kontext-dev"        # "Using the attached ..." prompts
VIDEO_MODEL = "Wan-AI/Wan2.2-T2V-A14B"
IMAGE_TO_VIDEO_MODEL = "Wan-AI/Wan2.2-I2V-A14B"
CUTOUT_SPACE = "not-lain/background-removal"               # BiRefNet, MIT licensed
MESH_SPACES = {
    "trellis": "trellis-community/TRELLIS",                # MIT licensed
    "hunyuan": "tencent/Hunyuan3D-2.1",                    # Tencent non-commercial licence
}
EDIT_SPACE = "mcp-tools/FLUX.1-Kontext-Dev"                # fallback when no provider serves Kontext


def _token() -> str | None:
    return os.environ.get("HF_TOKEN") or os.environ.get("HUGGING_FACE_HUB_TOKEN")


@dataclass
class HF:
    provider: str = "auto"

    def __post_init__(self) -> None:
        from huggingface_hub import InferenceClient

        self.client = InferenceClient(provider=self.provider, token=_token())

    # ---- images -------------------------------------------------------------
    def text_to_image(self, prompt: str, *, model: str, width: int, height: int, seed: int | None) -> Image.Image:
        return self.client.text_to_image(prompt, model=model, width=width, height=height, seed=seed)

    def edit(self, image: Path, prompt: str, *, model: str = EDIT_MODEL, use_space: bool = False) -> Image.Image:
        """Image + instruction -> image. Used for every prompt that starts 'Using the attached ...'."""
        if not use_space:
            return self.client.image_to_image(image.read_bytes(), prompt=prompt, model=model)
        from gradio_client import handle_file

        result, _seed, _btn = _space(EDIT_SPACE).predict(
            handle_file(str(image)), prompt, 0, True, 2.5, 28, api_name="/infer"
        )
        return Image.open(_path(result))

    # ---- video --------------------------------------------------------------
    def text_to_video(self, prompt: str, *, model: str = VIDEO_MODEL, seed: int | None = None) -> bytes:
        return self.client.text_to_video(prompt, model=model, seed=seed)

    def image_to_video(self, image: Path, prompt: str, *, model: str = IMAGE_TO_VIDEO_MODEL, seed: int | None = None) -> bytes:
        return self.client.image_to_video(image.read_bytes(), prompt=prompt, model=model, seed=seed)


# ---- Spaces -------------------------------------------------------------------
_clients: dict = {}


def _space(name: str):
    from gradio_client import Client

    if name not in _clients:
        _clients[name] = Client(name, token=_token(), verbose=False)
    return _clients[name]


def _path(value) -> str:
    """Gradio returns file outputs as a path string or a {'path': ...}/{'value': ...} dict."""
    if isinstance(value, dict):
        return value.get("path") or value.get("value") or value["url"]
    if isinstance(value, (list, tuple)):
        return _path(value[-1])
    return value


def cutout(image: Path, out: Path) -> Path:
    """Remove the background (transparent PNG). Also the right prep for image-to-3D."""
    from gradio_client import handle_file

    result = _space(CUTOUT_SPACE).predict(handle_file(str(image)), api_name="/png")
    shutil.copy(_path(result), out)
    return out


def mesh(image: Path, out: Path, *, backend: str = "trellis", seed: int = 0, texture_size: int = 1024) -> Path:
    """Image -> textured GLB. Feed it the 'Clean view for 3D Model' render, not the concept."""
    from gradio_client import handle_file

    client = _space(MESH_SPACES[backend])
    if backend == "trellis":
        # Same session: preprocess (crop + mask), then generate and extract the GLB.
        pre = client.predict(handle_file(str(image)), api_name="/preprocess_image")
        _state, _video, glb, _dl = client.predict(
            handle_file(_path(pre)), [], False, seed, 7.5, 12, 3.0, 12, "stochastic", 0.95, texture_size,
            api_name="/generate_and_extract_glb",
        )
    elif backend == "hunyuan":
        _white, textured, _html, _stats, _seed = client.predict(
            None, handle_file(str(image)), None, None, None, None, 30, 5.0, seed, 256, True, 8000, False,
            api_name="/generation_all",
        )
        glb = textured
    else:
        raise ValueError(f"unknown mesh backend {backend!r}; pick one of {sorted(MESH_SPACES)}")
    shutil.copy(_path(glb), out)
    return out


def stitch(images: list[Path], height: int = 768) -> Image.Image:
    """Put several references side by side so single-image editors can 'see' them all."""
    tiles = []
    for p in images:
        im = Image.open(p).convert("RGB")
        tiles.append(im.resize((round(im.width * height / im.height), height)))
    canvas = Image.new("RGB", (sum(t.width for t in tiles), height), "white")
    x = 0
    for t in tiles:
        canvas.paste(t, (x, 0))
        x += t.width
    return canvas
