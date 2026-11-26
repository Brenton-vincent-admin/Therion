"""Image-to-image pass: turns the clean procedural render into a detailed, realistic concept image while keeping
its pose, proportions and silhouette. Usage: realify.py <rgba render> <out prefix> "<prompt>" """
import sys, torch, numpy as np
from PIL import Image
from diffusers import StableDiffusionXLImg2ImgPipeline
src, prefix, prompt = sys.argv[1], sys.argv[2], sys.argv[3]
variants = [(0.5, 7), (0.62, 7), (0.62, 19), (0.74, 7), (0.74, 19), (0.85, 7)]
im = Image.open(src).convert("RGBA")
a = np.array(im)[..., 3] > 20
ys, xs = np.where(a)
x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
W, H = 1216, 832
s = min(W * 0.9 / (x1 - x0), H * 0.86 / (y1 - y0))
crop = im.crop((x0, y0, x1 + 1, y1 + 1)).resize((int((x1 - x0) * s), int((y1 - y0) * s)), Image.LANCZOS)
canvas = Image.new("RGB", (W, H), (236, 236, 236))
canvas.paste(crop, ((W - crop.width) // 2, (H - crop.height) // 2), crop)
canvas.save(prefix + "_input.png")
pipe = StableDiffusionXLImg2ImgPipeline.from_pretrained("stabilityai/stable-diffusion-xl-base-1.0", torch_dtype=torch.float16, variant="fp16", use_safetensors=True)
pipe.enable_model_cpu_offload()
neg = ("cropped, cut off, text, watermark, logo, multiple animals, scenery, landscape, ground, grass, rocks, engraving, drawing, "
       "illustration, sketch, cartoon, toy, plastic, smooth, blurry, extra legs, extra heads, open mouth, feathers")
for strength, seed in variants:
    img = pipe(prompt, negative_prompt=neg, image=canvas, strength=strength, num_inference_steps=40, guidance_scale=7.0,
               generator=torch.Generator("cpu").manual_seed(seed)).images[0]
    img.save(f"{prefix}_s{int(strength * 100)}_{seed}.png")
    print("saved", strength, seed, flush=True)
