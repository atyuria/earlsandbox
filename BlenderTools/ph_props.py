# Poly Haven models (glTF, from C:/Users/Binky/PolyHaven) to FBX with their textures, into the game's
# Resources/Props, for the maps to place. Real-world size, Unity axes (the FBX exporter turns Blender's Z-up).
# blender --background --python ph_props.py -- id id ...
import bpy, sys, os
SRC = "C:/Users/Binky/PolyHaven/models/"
OUT = r"C:/Users/Binky/OneDrive/Documents/GitHub/earlsandbox/Assets/CarlSandbox/Resources/Props/"
os.makedirs(OUT, exist_ok=True)
done = []
for pid in sys.argv[sys.argv.index("--") + 1:]:
    d = SRC + pid
    if not os.path.isdir(d): print("MISSING", pid); continue
    gl = [f for f in os.listdir(d) if f.endswith(".gltf")]
    if not gl: print("NOGLTF", pid); continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(d, gl[0]))
    bpy.ops.export_scene.fbx(filepath=OUT + pid + ".fbx", use_selection=False, object_types={'MESH', 'EMPTY'},
                             path_mode='COPY', embed_textures=True, apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True)
    done.append(pid)
print("PROPS", done)
