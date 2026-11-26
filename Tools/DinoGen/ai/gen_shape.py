import sys, time, torch
from PIL import Image
from hy3dgen.rembg import BackgroundRemover
from hy3dgen.shapegen import Hunyuan3DDiTFlowMatchingPipeline
src, dst = sys.argv[1], sys.argv[2]
image = Image.open(src).convert("RGB")
image = BackgroundRemover()(image)
image.save(dst.replace(".glb", "_cutout.png"))
pipe = Hunyuan3DDiTFlowMatchingPipeline.from_pretrained('tencent/Hunyuan3D-2', subfolder='hunyuan3d-dit-v2-0', variant='fp16')
t = time.time()
mesh = pipe(image=image, num_inference_steps=50, octree_resolution=380, num_chunks=20000,
            generator=torch.manual_seed(12345), output_type='trimesh')[0]
print("seconds", time.time() - t, "verts", len(mesh.vertices), "faces", len(mesh.faces), flush=True)
print("peak vram GB", torch.cuda.max_memory_allocated() / 1e9)
mesh.export(dst)
