"""Builds the two hero models (Lyra the battle-mage, Sir Gareth the paladin) in Blender from code.

Stylised, chunky proportions so they read on a phone from the isometric camera. Each hero is one mesh with
flat-coloured materials, feet at the origin, facing -Y (which lands as +Z, forward, in Unity), about 1.35 m tall.

    pip install bpy==4.2.0          # Blender as a Python module (Python 3.11)
    python tools/blender/heroes.py  # writes FBX into the Unity project, the .blend and preview renders into tools/blender/

Options: --no-render skips the preview renders (they take a minute or two on CPU).
"""
import math
import sys
from pathlib import Path

import bpy  # must come first: it makes bmesh and mathutils importable
import bmesh
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
UNITY_OUT = ROOT / "unity" / "Wildtide" / "Assets" / "Wildtide" / "Resources" / "Characters"
SOURCE_OUT = ROOT / "tools" / "blender" / "source"
RENDER_OUT = ROOT / "tools" / "blender" / "renders"
TARGET_HEIGHT = 1.35  # body height without props; matches the player's collider in Unity


# ---- helpers ----------------------------------------------------------------------------------------

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"


_materials = {}


def mat(name, hex_rgb, rough=0.6, metal=0.0, emit=0.0):
    key = name
    if key in _materials:
        return _materials[key]
    # The sRGB colour rides in the name too, so the game can rebuild the exact colour with its toon shader
    # whatever the importer does with FBX colours.
    m = bpy.data.materials.new(f"{name} #{hex_rgb:06X}")
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    rgb = tuple(((hex_rgb >> s) & 0xFF) / 255.0 for s in (16, 8, 0))
    lin = tuple(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in rgb)
    bsdf.inputs["Base Color"].default_value = (*lin, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    if emit > 0:
        bsdf.inputs["Emission Color"].default_value = (*lin, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emit
    m.diffuse_color = (*lin, 1.0)  # what the FBX exporter writes as the colour
    _materials[key] = m
    return m


class Builder:
    """Collects parts for one character."""

    def __init__(self, name):
        self.name = name
        self.parts = []

    def _finish(self, obj, material, name, rot, smooth=True):
        obj.name = name
        if rot:
            obj.rotation_euler = [math.radians(a) for a in rot]
        obj.data.materials.append(material)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        for p in obj.data.polygons:
            p.use_smooth = smooth
        self.parts.append(obj)
        return obj

    def sphere(self, loc, scale, material, name="Sphere", rot=None, segments=24, smooth=True):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=segments // 2, radius=1, location=loc)
        o = bpy.context.active_object
        o.scale = scale if hasattr(scale, "__len__") else (scale, scale, scale)
        return self._finish(o, material, name, rot, smooth)

    def cyl(self, loc, radius, depth, material, name="Cylinder", rot=None, verts=20, scale=None):
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc)
        o = bpy.context.active_object
        if scale:
            o.scale = scale
        return self._finish(o, material, name, rot)

    def cone(self, loc, r1, r2, depth, material, name="Cone", rot=None, verts=24, scale=None):
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=depth, location=loc)
        o = bpy.context.active_object
        if scale:
            o.scale = scale
        return self._finish(o, material, name, rot)

    def box(self, loc, size, material, name="Box", rot=None, bevel=0.0):
        bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
        o = bpy.context.active_object
        o.scale = size
        o = self._finish(o, material, name, rot, smooth=False)
        if bevel > 0:
            mod = o.modifiers.new("Bevel", "BEVEL")
            mod.width = bevel
            mod.segments = 2
            apply_modifiers(o)
        return o

    def ico(self, loc, scale, material, name="Crystal", rot=None):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=1, location=loc)
        o = bpy.context.active_object
        o.scale = scale
        return self._finish(o, material, name, rot, smooth=False)

    def torus(self, loc, major, minor, material, name="Ring", rot=None, scale=None):
        bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=28, minor_segments=8, location=loc)
        o = bpy.context.active_object
        if scale:
            o.scale = scale
        return self._finish(o, material, name, rot)

    def slab(self, points_xz, thickness, y, material, name="Slab", rot=None, offset=(0, 0, 0)):
        """Extrudes a 2D outline (x, z pairs) by `thickness` along +Y, starting at `y`."""
        mesh = bpy.data.meshes.new(name)
        bm = bmesh.new()
        verts = [bm.verts.new((x + offset[0], y + offset[1], z + offset[2])) for x, z in points_xz]
        face = bm.faces.new(verts)
        ext = bmesh.ops.extrude_face_region(bm, geom=[face])
        moved = [e for e in ext["geom"] if isinstance(e, bmesh.types.BMVert)]
        bmesh.ops.translate(bm, verts=moved, vec=(0, thickness, 0))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(mesh)
        bm.free()
        o = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(o)
        bpy.context.view_layer.objects.active = o
        for other in bpy.context.selected_objects:
            other.select_set(False)
        o.select_set(True)
        if rot:
            # rotate about the slab's own centre
            centre = sum((Vector(v.co) for v in mesh.vertices), Vector()) / len(mesh.vertices)
            bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="MEDIAN")
            o.rotation_euler = [math.radians(a) for a in rot]
        o.data.materials.append(material)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        for p in o.data.polygons:
            p.use_smooth = False
        self.parts.append(o)
        return o

    def join(self, height_ref):
        """Joins all parts, scales so the body (not props) is TARGET_HEIGHT tall, and puts the feet on the origin."""
        for o in bpy.context.selected_objects:
            o.select_set(False)
        for o in self.parts:
            o.select_set(True)
        bpy.context.view_layer.objects.active = self.parts[0]
        bpy.ops.object.join()
        obj = bpy.context.active_object
        obj.name = self.name
        obj.data.name = self.name
        s = TARGET_HEIGHT / height_ref
        obj.scale = (s, s, s)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        bpy.context.scene.cursor.location = (0, 0, 0)
        bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
        return obj


def apply_modifiers(o):
    for other in bpy.context.selected_objects:
        other.select_set(False)
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    for m in list(o.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)


def cut(o, point, normal, keep_front=False, thickness=0.0):
    """Slices a part with a plane and removes the side the normal points to (or the other side),
    then gives the open shell some thickness so it has no see-through backfaces."""
    bm = bmesh.new()
    bm.from_mesh(o.data)
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=point, plane_no=normal,
                           clear_outer=not keep_front, clear_inner=keep_front)
    bm.to_mesh(o.data)
    bm.free()
    if thickness > 0:
        mod = o.modifiers.new("Solidify", "SOLIDIFY")
        mod.thickness = thickness
        mod.offset = 0
        apply_modifiers(o)
    return o


# ---- Lyra ---------------------------------------------------------------------------------------------

def build_lyra():
    b = Builder("Lyra")
    skin = mat("Lyra Skin", 0xF1C7A8)
    hair = mat("Lyra Hair", 0x9C9891, rough=0.55)
    eye = mat("Lyra Eyes", 0x2FA37A, rough=0.2)
    dark = mat("Lyra Dark", 0x1C1C22)
    dress = mat("Lyra Dress", 0x2B3A67)
    sleeve = mat("Lyra Sleeve", 0x34467A)
    corset = mat("Lyra Corset", 0x6B4424, rough=0.45)
    belt = mat("Lyra Belt", 0x4A2E18, rough=0.45)
    boots = mat("Lyra Boots", 0x5A3A22, rough=0.5)
    trousers = mat("Lyra Trousers", 0x2A2A33)
    cloak = mat("Lyra Cloak", 0x6B5B3E, rough=0.8)
    cloak_in = mat("Lyra Cloak Lining", 0x4E4230, rough=0.8)
    wood = mat("Lyra Staff", 0x5C3D22, rough=0.7)
    crystal = mat("Lyra Crystal", 0x3A6BFF, rough=0.1, emit=1.5)
    gold = mat("Lyra Clasp", 0xC8A040, rough=0.3, metal=0.8)

    # Legs and boots
    for x in (-0.09, 0.09):
        b.cyl((x, 0, 0.17), 0.075, 0.32, boots, "Boot")
        b.sphere((x, -0.05, 0.05), (0.08, 0.12, 0.06), boots, "Toe")
        b.cyl((x, 0, 0.36), 0.082, 0.05, belt, "Boot Cuff")
        b.cyl((x, 0, 0.42), 0.06, 0.2, trousers, "Leg")
    # Dress and bodice
    b.cone((0, 0, 0.55), 0.3, 0.16, 0.5, dress, "Skirt")
    b.cyl((0, 0, 0.3), 0.3, 0.02, sleeve, "Hem")
    b.cyl((0, 0, 0.84), 0.17, 0.2, corset, "Corset")
    for z in (0.79, 0.84, 0.89):
        b.cyl((0, -0.005, z), 0.172, 0.008, belt, "Lacing")
    b.cyl((0, 0, 0.74), 0.19, 0.045, belt, "Belt")
    b.box((0, -0.19, 0.74), (0.05, 0.02, 0.05), gold, "Buckle")
    b.box((0.16, -0.1, 0.7), (0.1, 0.07, 0.1), corset, "Pouch", rot=(0, 0, -25), bevel=0.012)
    b.box((-0.17, -0.06, 0.69), (0.08, 0.06, 0.09), corset, "Pouch", rot=(0, 0, 30), bevel=0.012)
    b.sphere((0, 0, 1.0), (0.17, 0.13, 0.13), sleeve, "Chest")
    # Neck and head
    b.cyl((0, 0, 1.1), 0.05, 0.08, skin, "Neck")
    b.sphere((0, 0, 1.28), (0.2, 0.19, 0.21), skin, "Head")
    for x in (-0.07, 0.07):
        b.sphere((x, -0.168, 1.27), (0.035, 0.02, 0.042), eye, "Eye")
        b.sphere((x - 0.01, -0.183, 1.285), 0.009, mat("White", 0xFFFFFF), "Shine")
        b.box((x, -0.175, 1.33), (0.06, 0.01, 0.012), mat("Lyra Brow", 0x8C8680), "Brow", rot=(0, 8 if x < 0 else -8, 0))
    b.sphere((0, -0.19, 1.2), (0.03, 0.01, 0.008), mat("Lyra Lips", 0xC07A6A), "Mouth")
    # Hair: a cap cut back from the face, plus a braid over her right shoulder
    cap = b.sphere((0, 0.02, 1.32), (0.225, 0.215, 0.215), hair, "Hair")
    cut(cap, (0, -0.1, 1.33), (0, -0.62, -0.78), thickness=0.02)
    b.sphere((0, 0.1, 1.2), (0.19, 0.12, 0.17), hair, "Hair Back")
    braid = [(0.14, -0.06, 1.18), (0.16, -0.09, 1.1), (0.17, -0.11, 1.02), (0.175, -0.12, 0.94), (0.18, -0.125, 0.87), (0.18, -0.13, 0.81)]
    for i, p in enumerate(braid):
        b.sphere(p, (0.048 - i * 0.004, 0.044 - i * 0.004, 0.06), hair, "Braid")
    b.cyl((0.18, -0.13, 0.77), 0.02, 0.03, belt, "Braid Tie")
    # Hood (down, resting behind the head) and cloak
    hood = b.sphere((0, 0.08, 1.22), (0.25, 0.22, 0.24), cloak, "Hood")
    cut(hood, (0, 0.02, 1.2), (0, -1, 0), thickness=0.03)
    shell = b.cone((0, 0.03, 0.66), 0.38, 0.2, 0.86, cloak, "Cloak")
    cut(shell, (0, 0.05, 0.6), (0, -1, 0), thickness=0.025)
    b.cone((0, 0.05, 0.66), 0.36, 0.19, 0.82, cloak_in, "Cloak Lining", scale=(0.98, 0.9, 1))
    cut(b.parts[-1], (0, 0.06, 0.6), (0, -1, 0), thickness=0.01)
    b.torus((0, 0, 1.09), 0.13, 0.03, cloak, "Collar", scale=(1.1, 0.95, 1))
    b.sphere((0.1, -0.1, 1.08), 0.025, gold, "Clasp")
    # Arms: left hangs, right grips the staff
    b.cyl((-0.21, 0, 0.94), 0.055, 0.2, sleeve, "Upper Arm", rot=(0, -12, 0))
    b.cyl((-0.235, -0.01, 0.77), 0.06, 0.16, mat("Lyra Bracer", 0x7A4E2A, rough=0.45), "Bracer", rot=(0, -8, 0))
    b.sphere((-0.245, -0.015, 0.66), (0.05, 0.045, 0.06), skin, "Hand")
    b.sphere((-0.2, 0, 1.03), 0.075, sleeve, "Shoulder")
    b.sphere((0.2, 0, 1.03), 0.075, sleeve, "Shoulder")
    b.cyl((0.23, -0.04, 0.94), 0.055, 0.2, sleeve, "Upper Arm", rot=(20, 12, 0))
    b.cyl((0.26, -0.11, 0.8), 0.06, 0.16, mat("Lyra Bracer", 0x7A4E2A), "Bracer", rot=(35, 8, 0))
    b.sphere((0.27, -0.16, 0.71), (0.055, 0.05, 0.06), skin, "Hand")
    # Staff: a gnarled branch, forked at the top around a cluster of blue crystals
    sx, sy = 0.29, -0.17
    b.cyl((sx, sy, 0.8), 0.022, 1.56, wood, "Staff", rot=(0, 2, 0), verts=10)
    for z, dx in ((0.35, 0.015), (0.95, -0.012), (1.3, 0.014)):
        b.sphere((sx + dx, sy, z), (0.03, 0.03, 0.045), wood, "Knot", segments=10)
    for ang in (0, 120, 240):
        a = math.radians(ang)
        b.cyl((sx + math.cos(a) * 0.04, sy + math.sin(a) * 0.04, 1.63), 0.013, 0.14, wood, "Prong",
              rot=(math.sin(a) * -25, math.cos(a) * 25, 0), verts=8)
    b.ico((sx, sy, 1.72), (0.05, 0.05, 0.11), crystal, "Crystal")
    b.ico((sx + 0.05, sy - 0.02, 1.68), (0.03, 0.03, 0.07), crystal, "Crystal", rot=(0, 25, 0))
    b.ico((sx - 0.045, sy + 0.02, 1.67), (0.03, 0.03, 0.065), crystal, "Crystal", rot=(0, -25, 0))
    b.ico((sx + 0.01, sy - 0.05, 1.66), (0.025, 0.025, 0.055), crystal, "Crystal", rot=(25, 0, 0))
    return b.join(height_ref=1.49)


# ---- Sir Gareth ---------------------------------------------------------------------------------------

def build_gareth():
    b = Builder("Gareth")
    steel = mat("Gareth Steel", 0x9AA0A6, rough=0.35, metal=0.9)
    dsteel = mat("Gareth Dark Steel", 0x5E6368, rough=0.4, metal=0.85)
    gold = mat("Gareth Gold", 0xC8A040, rough=0.3, metal=0.9)
    red = mat("Gareth Red", 0x9E2B25, rough=0.7)
    dred = mat("Gareth Dark Red", 0x6E1C19, rough=0.75)
    skin = mat("Gareth Skin", 0xD39A78)
    hair = mat("Gareth Hair", 0x5A3A22, rough=0.7)
    beard = mat("Gareth Beard", 0x4E3220, rough=0.8)
    leather = mat("Gareth Leather", 0x5A3A22, rough=0.5)
    wood = mat("Gareth Wood", 0x6B4A2A, rough=0.7)
    dark = mat("Gareth Eyes", 0x22201E)

    # Legs: sabatons, greaves, knee cops, tassets
    for x in (-0.11, 0.11):
        b.box((x, -0.04, 0.05), (0.13, 0.22, 0.1), dsteel, "Sabaton", bevel=0.02)
        b.cyl((x, 0, 0.22), 0.085, 0.28, steel, "Greave")
        b.sphere((x, -0.04, 0.37), (0.075, 0.06, 0.06), gold, "Knee")
        b.cyl((x, 0, 0.47), 0.08, 0.16, leather, "Thigh")
    b.cone((0, 0, 0.62), 0.28, 0.22, 0.2, steel, "Tassets", verts=12)
    b.cyl((0, 0, 0.53), 0.281, 0.02, gold, "Tasset Trim", verts=12)
    # Tabard, belt, breastplate
    b.box((0, -0.285, 0.5), (0.17, 0.02, 0.34), red, "Tabard", rot=(-6, 0, 0))
    b.box((0, -0.297, 0.5), (0.12, 0.01, 0.3), gold, "Tabard Trim", rot=(-6, 0, 0))
    b.box((0, -0.302, 0.5), (0.1, 0.01, 0.28), dred, "Tabard Inset", rot=(-6, 0, 0))
    b.cyl((0, 0, 0.73), 0.225, 0.06, leather, "Belt")
    b.box((0, -0.225, 0.73), (0.08, 0.02, 0.07), gold, "Buckle", bevel=0.01)
    b.sphere((0, 0, 0.93), (0.26, 0.2, 0.25), steel, "Breastplate")
    b.torus((0, 0, 0.8), 0.23, 0.018, gold, "Plate Trim", scale=(1, 0.8, 1))
    b.box((0, -0.19, 0.95), (0.04, 0.03, 0.18), gold, "Plate Ridge", rot=(-12, 0, 0))
    # Cape and collar
    cape = b.cone((0, 0.06, 0.7), 0.4, 0.24, 0.9, red, "Cape")
    cut(cape, (0, 0.07, 0.6), (0, -1, 0), thickness=0.03)
    b.torus((0, 0.02, 1.1), 0.16, 0.05, dred, "Mantle", scale=(1.25, 1, 1))
    # Head: neck, face, hair, beard
    b.cyl((0, 0, 1.12), 0.07, 0.08, skin, "Neck")
    b.sphere((0, 0, 1.28), (0.18, 0.18, 0.2), skin, "Head")
    for x in (-0.065, 0.065):
        b.sphere((x, -0.16, 1.3), (0.025, 0.015, 0.02), dark, "Eye")
        b.box((x, -0.165, 1.345), (0.07, 0.02, 0.022), hair, "Brow", rot=(0, -10 if x < 0 else 10, 0))
    b.sphere((0, -0.18, 1.26), (0.03, 0.035, 0.045), skin, "Nose")
    top = b.sphere((0, 0.02, 1.33), (0.2, 0.2, 0.19), hair, "Hair")
    cut(top, (0, -0.08, 1.36), (0, -0.55, -0.83), thickness=0.02)
    for dx in (-0.08, 0, 0.08):
        b.sphere((dx, -0.02, 1.47), (0.06, 0.07, 0.05), hair, "Hair Tuft", rot=(0, dx * 200, 0))
    bd = b.sphere((0, -0.03, 1.2), (0.19, 0.17, 0.16), beard, "Beard")
    cut(bd, (0, -0.02, 1.25), (0, 0.45, 0.89), thickness=0.02)
    b.sphere((0, -0.18, 1.22), (0.07, 0.02, 0.018), beard, "Moustache")
    b.sphere((0, -0.185, 1.2), (0.025, 0.01, 0.01), mat("Gareth Lips", 0x9A5E4E), "Mouth")
    # Shoulders and arms
    for side in (-1, 1):
        pad = b.sphere((side * 0.27, 0, 1.06), (0.14, 0.14, 0.12), steel, "Pauldron")
        cut(pad, (side * 0.27, 0, 1.01), (0, 0, -1), thickness=0.02)
        b.torus((side * 0.27, 0, 1.01), 0.135, 0.015, gold, "Pauldron Trim")
        b.sphere((side * 0.27, 0, 1.12), 0.035, gold, "Rivet")
    b.cyl((-0.3, -0.02, 0.9), 0.07, 0.22, dsteel, "Upper Arm", rot=(20, -10, 0))
    b.cyl((-0.32, -0.1, 0.76), 0.075, 0.16, steel, "Vambrace", rot=(60, -5, 0))
    b.sphere((-0.33, -0.19, 0.72), (0.07, 0.065, 0.07), dsteel, "Gauntlet")
    b.cyl((0.3, -0.02, 0.9), 0.07, 0.22, dsteel, "Upper Arm", rot=(15, 10, 0))
    b.cyl((0.34, -0.1, 0.73), 0.075, 0.16, steel, "Vambrace", rot=(30, 15, 0))
    b.sphere((0.37, -0.15, 0.62), (0.07, 0.065, 0.07), dsteel, "Gauntlet")
    # Warhammer in the right hand, head up by the shoulder
    hx, hy = 0.4, -0.16
    b.cyl((hx, hy, 0.78), 0.028, 0.95, wood, "Hammer Haft", rot=(-8, 0, 0), verts=10)
    b.cyl((hx, hy + 0.01, 0.4), 0.036, 0.1, leather, "Grip Wrap", verts=10)
    b.box((hx, hy - 0.07, 1.26), (0.18, 0.34, 0.18), dsteel, "Hammer Head", rot=(-8, 0, 0), bevel=0.025)
    for dy in (-0.19, 0.19):
        b.box((hx, hy - 0.07 + dy, 1.26 - dy * 0.14), (0.2, 0.04, 0.2), steel, "Hammer Face", rot=(-8, 0, 0), bevel=0.012)
    b.box((hx, hy - 0.07, 1.26), (0.19, 0.08, 0.19), gold, "Hammer Band", rot=(-8, 0, 0))
    # Heater shield on the left arm: gold rim behind a red face, gold lion on the front
    shield_face = [(0.19, 0.2)] + [(0.19 * math.cos(math.radians(180 * i / 12)),
                                    -0.02 - 0.32 * math.sin(math.radians(180 * i / 12)) ** 1.5) for i in range(13)] + [(-0.19, 0.2)]
    rim = [(x * 1.1, z * 1.07 + 0.01) for x, z in shield_face]
    sc = (-0.42, -0.22, 0.8)
    b.slab(rim, 0.03, 0.0, gold, "Shield Rim", rot=(0, 0, -20), offset=sc)
    b.slab(shield_face, 0.02, -0.018, red, "Shield", rot=(0, 0, -20), offset=sc)
    lion = [(-0.05, 0.12), (0.0, 0.14), (0.06, 0.11), (0.08, 0.06), (0.05, 0.03), (0.08, 0.0), (0.1, -0.06),
            (0.06, -0.05), (0.05, -0.1), (0.08, -0.16), (0.03, -0.16), (0.0, -0.1), (-0.03, -0.16), (-0.08, -0.16),
            (-0.05, -0.08), (-0.07, 0.0), (-0.1, 0.04), (-0.11, 0.1), (-0.08, 0.08)]
    b.slab(lion, 0.012, -0.03, gold, "Lion", rot=(0, 0, -20), offset=(sc[0], sc[1], sc[2] + 0.03))
    return b.join(height_ref=1.49)


# ---- export and preview -------------------------------------------------------------------------------

def export(obj):
    UNITY_OUT.mkdir(parents=True, exist_ok=True)
    for o in bpy.context.selected_objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    path = UNITY_OUT / f"{obj.name}.fbx"
    # -Z forward, Y up, transforms baked: Blender's -Y front becomes Unity's +Z forward at scale 1.
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             mesh_smooth_type="FACE", use_mesh_modifiers=True, add_leaf_bones=False,
                             path_mode="STRIP")
    print("wrote", path.relative_to(ROOT))


def render(objs, name, cam_loc, target, ortho=1.9):
    RENDER_OUT.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 48
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = 1000, 1000
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("World") if scene.world is None else scene.world
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.78, 0.86, 0.92, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8

    if "Ground" not in bpy.data.objects:
        bpy.ops.mesh.primitive_plane_add(size=20, location=(0, 0, 0))
        g = bpy.context.active_object
        g.name = "Ground"
        g.data.materials.append(mat("Ground", 0xE6DCC4, rough=0.9))
        bpy.ops.object.light_add(type="SUN", location=(3, -4, 6))
        sun = bpy.context.active_object
        sun.data.energy = 3.5
        sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(35))
        bpy.ops.object.light_add(type="AREA", location=(-2.5, -3, 2.5))
        fill = bpy.context.active_object
        fill.data.energy = 250
        fill.data.size = 3
        fill.rotation_euler = (math.radians(60), 0, math.radians(-40))
        bpy.ops.object.camera_add()
        scene.camera = bpy.context.active_object

    cam = scene.camera
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = ortho
    cam.location = cam_loc
    direction = Vector(target) - Vector(cam_loc)
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()

    for o in bpy.data.objects:
        if o.type == "MESH" and o.name != "Ground":
            o.hide_render = o not in objs
    scene.render.filepath = str(RENDER_OUT / f"{name}.png")
    bpy.ops.render.render(write_still=True)
    print("rendered", (RENDER_OUT / f"{name}.png").relative_to(ROOT))


def main():
    do_render = "--no-render" not in sys.argv
    reset()
    lyra = build_lyra()
    gareth = build_gareth()
    export(lyra)
    export(gareth)
    SOURCE_OUT.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0  # no heroes.blend1 backups
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_OUT / "heroes.blend"), compress=True)
    if do_render:
        gareth.location.x = 0
        lyra.location.x = 0
        render([lyra], "lyra", (1.6, -3.2, 1.6), (0, 0, 0.78))
        render([gareth], "gareth", (1.6, -3.2, 1.6), (0, 0, 0.78))
        lyra.location.x = -0.55
        gareth.location.x = 0.55
        render([lyra, gareth], "heroes", (1.8, -3.6, 1.8), (0, 0, 0.75), ortho=2.4)
        lyra.location.x = gareth.location.x = 0


if __name__ == "__main__":
    main()
