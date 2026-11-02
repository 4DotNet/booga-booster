"""Export ride-design.blend as the web model used by the Angular dashboard.

Run inside Blender with ride-design.blend open, either headless:

    blender -b designs/3d/ride-design.blend -P designs/3d/export_web_model.py -- <out.glb>

or from the Blender MCP server / Text Editor after setting ``OUT_PATH`` in the
globals. The default output is the app's ``public/3dmodels/booga-booster.glb``.

What it does (see openspec/changes/ride-light-show/design.md, D1):

* puts every driven pivot at rest (frame 0);
* removes the glowing bulb spheres from each light mesh (the chrome sockets stay)
  and records them on an empty child ``<object>_Bulbs`` as the custom property
  ``bulbs`` -- a flat ``[x, y, z, radius, colour, ...]`` list in glTF (Y-up)
  node-local coordinates, colour 0 = warm, 1 = pink, 2 = cyan. glTF exports custom
  properties as ``extras``; three.js exposes them as ``userData``;
* swaps the 16k backdrop texture for an 8192x2048 copy (GPU texture limits);
* exports the ride, backdrop, floor and light poles (with their spot lights) to GLB;
* re-opens the .blend from disk, so nothing it changed is ever saved.
"""

import math
import os
import sys
import tempfile

import bmesh
import bpy

BULB_MATERIALS = {"mat_bulb_warm": 0, "mat_bulb_pink": 1, "mat_bulb_cyan": 2}
EXPORT_COLLECTIONS = ("CarnivalRide", "Backdrop", "LightPoles")
STAGE_LIGHTS = {"Spot_Pink", "Spot_Blue", "Spot_Key", "Spot_Back_L", "Spot_Back_R", "Area_Top"}
BACKDROP_IMAGE = "booga-booster-backdrop-16384x4096.jpg"
WEB_TEXTURE_SIZE = (8192, 2048)


def default_out_path():
    blend_dir = os.path.dirname(bpy.data.filepath)
    repo = os.path.abspath(os.path.join(blend_dir, "..", ".."))
    return os.path.join(repo, "src", "FourDotnet.BoogaBooster.App", "public", "3dmodels", "booga-booster.glb")


def resolve_out_path():
    if "OUT_PATH" in globals():
        return globals()["OUT_PATH"]
    if "--" in sys.argv:
        args = sys.argv[sys.argv.index("--") + 1:]
        if args:
            return os.path.abspath(args[0])
    return default_out_path()


def extract_bulbs(mesh):
    """Remove bulb spheres from ``mesh``; return [(x, y, z, r, colour)] in Blender local space."""
    slot_colour = {
        i: BULB_MATERIALS[m.name]
        for i, m in enumerate(mesh.materials)
        if m is not None and m.name in BULB_MATERIALS
    }
    if not slot_colour:
        return []
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.faces.ensure_lookup_table()
    seen = set()
    bulbs = []
    doomed = []
    for face in bm.faces:
        if face.index in seen or face.material_index not in slot_colour:
            continue
        # flood-fill one bulb (an island of faces sharing the bulb material)
        island, stack = [], [face]
        seen.add(face.index)
        while stack:
            f = stack.pop()
            island.append(f)
            for e in f.edges:
                for nb in e.link_faces:
                    if nb.index not in seen and nb.material_index == face.material_index:
                        seen.add(nb.index)
                        stack.append(nb)
        verts = {v for f in island for v in f.verts}
        cx = sum(v.co.x for v in verts) / len(verts)
        cy = sum(v.co.y for v in verts) / len(verts)
        cz = sum(v.co.z for v in verts) / len(verts)
        r = max(math.dist((cx, cy, cz), tuple(v.co)) for v in verts)
        bulbs.append((cx, cy, cz, r, slot_colour[face.material_index]))
        doomed.extend(island)
    bmesh.ops.delete(bm, geom=doomed, context="FACES_ONLY")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.to_mesh(mesh)
    bm.free()
    return bulbs


def to_gltf_flat(bulbs):
    flat = []
    for x, y, z, r, c in bulbs:
        # Blender Z-up -> glTF Y-up: (x, y, z) -> (x, z, -y)
        flat.extend((round(x, 4), round(z, 4), round(-y, 4), round(r, 4), c))
    return flat


def attach_bulbs(owner, flat, export_set):
    empty = bpy.data.objects.new(f"{owner.name}_Bulbs", None)
    owner.users_collection[0].objects.link(empty)
    empty.parent = owner
    empty.matrix_parent_inverse.identity()
    empty["bulbs"] = flat
    export_set.add(empty)


def swap_backdrop_texture(tmp_dir):
    src = bpy.data.images.get(BACKDROP_IMAGE)
    if src is None:
        return
    small = src.copy()
    small.scale(*WEB_TEXTURE_SIZE)
    path = os.path.join(tmp_dir, "booga-booster-backdrop-8192x2048.jpg")
    small.filepath_raw = path
    small.file_format = "JPEG"
    bpy.context.scene.render.image_settings.quality = 88
    small.save()
    for mat in bpy.data.materials:
        if mat.node_tree is None:
            continue
        for node in mat.node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image == src:
                node.image = small


def export_kwargs(out_path):
    props = bpy.ops.export_scene.gltf.get_rna_type().properties.keys()
    wanted = {
        "filepath": out_path,
        "export_format": "GLB",
        "use_selection": True,
        "export_apply": True,
        "export_extras": True,
        "export_lights": True,
        "export_cameras": False,
        "export_animations": False,
        "export_yup": True,
        "export_image_format": "AUTO",
        "export_image_quality": 88,
    }
    return {k: v for k, v in wanted.items() if k in props}


def main():
    blend_path = bpy.data.filepath
    out_path = resolve_out_path()
    scene = bpy.context.scene
    scene.frame_set(0)

    export_set = set()
    for name in EXPORT_COLLECTIONS:
        coll = bpy.data.collections.get(name)
        if coll is None:
            continue
        for ob in coll.all_objects:
            if ob.type == "CAMERA" or ob.name in STAGE_LIGHTS:
                continue
            export_set.add(ob)

    # bulbs: process each mesh once, then attach the record to every object using it
    report = {}
    by_mesh = {}
    for ob in sorted(export_set, key=lambda o: o.name):
        if ob.type == "MESH":
            by_mesh.setdefault(ob.data.name, []).append(ob)
    for mesh_name, users in by_mesh.items():
        bulbs = extract_bulbs(bpy.data.meshes[mesh_name])
        if not bulbs:
            continue
        flat = to_gltf_flat(bulbs)
        for ob in users:
            attach_bulbs(ob, flat, export_set)
        report[mesh_name] = (len(bulbs), len(users))

    with tempfile.TemporaryDirectory() as tmp_dir:
        swap_backdrop_texture(tmp_dir)
        bpy.ops.object.select_all(action="DESELECT")
        for ob in export_set:
            ob.hide_set(False)
            ob.select_set(True)
        os.makedirs(os.path.dirname(out_path), exist_ok=True)
        bpy.ops.export_scene.gltf(**export_kwargs(out_path))

    total = 0
    for mesh_name, (count, users) in sorted(report.items()):
        print(f"  {mesh_name}: {count} bulbs x {users} objects")
        total += count * users
    print(f"exported {out_path} ({os.path.getsize(out_path) / 1e6:.1f} MB), {total} bulbs")

    # never keep the stripped scene: reload the design from disk
    if blend_path and not bpy.app.background:
        bpy.ops.wm.open_mainfile(filepath=blend_path)


main()
