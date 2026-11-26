"""Image -> 3D shape with TripoSG (MIT). Uses rembg/u2net for the cut-out instead of the non-commercial RMBG model,
and the standard (non-flash) decoder."""
import sys, os, time
import numpy as np, torch, trimesh
from PIL import Image
from huggingface_hub import snapshot_download
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "TripoSG"))
from triposg.pipelines.pipeline_triposg import TripoSGPipeline
from rembg import remove, new_session

src, dst = sys.argv[1], sys.argv[2]
seed = int(sys.argv[3]) if len(sys.argv) > 3 else 42
img = Image.open(src).convert("RGB")
cut = remove(img, session=new_session("u2net"))
a = np.array(cut)[..., 3] > 128
ys, xs = np.where(a)
x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
side = int(max(x1 - x0, y1 - y0) * 1.2)
canvas = Image.new("RGB", (side, side), (255, 255, 255))
crop = cut.crop((x0, y0, x1 + 1, y1 + 1))
canvas.paste(crop, ((side - crop.width) // 2, (side - crop.height) // 2), crop)
canvas = canvas.resize((1024, 1024), Image.LANCZOS)
canvas.save(dst.replace(".glb", "_input.png"))
wdir = os.path.join(HERE, "TripoSG", "pretrained_weights", "TripoSG")
snapshot_download(repo_id="VAST-AI/TripoSG", local_dir=wdir)
pipe = TripoSGPipeline.from_pretrained(wdir).to("cuda", torch.float16)
t = time.time()
out = pipe(image=canvas, generator=torch.Generator(device="cuda").manual_seed(seed), num_inference_steps=50,
           guidance_scale=7.0, use_flash_decoder=False, dense_octree_depth=8, hierarchical_octree_depth=9).samples[0]
mesh = trimesh.Trimesh(out[0].astype(np.float32), np.ascontiguousarray(out[1]))
print("seconds", round(time.time() - t, 1), "faces", len(mesh.faces), "peak vram GB", round(torch.cuda.max_memory_allocated() / 1e9, 2), flush=True)
mesh.export(dst)
