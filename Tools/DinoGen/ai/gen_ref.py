import torch, sys
from diffusers import StableDiffusionXLPipeline
pipe = StableDiffusionXLPipeline.from_pretrained("stabilityai/stable-diffusion-xl-base-1.0", torch_dtype=torch.float16, variant="fp16", use_safetensors=True)
pipe.enable_model_cpu_offload()
prompt = ("a Tyrannosaurus rex, whole animal in frame, full body three-quarter side view, standing in a neutral horizontal pose, "
          "tail straight out, mouth slightly open, realistic scientific paleoart reconstruction, detailed scaly skin, "
          "natural brown and olive colours, soft even studio lighting, isolated on a plain white background, 3d render")
neg = "cropped, cut off, text, watermark, logo, multiple animals, background scenery, grass, shadow on wall, cartoon, toy, blurry, extra legs"
for seed in (11, 23, 42, 77):
    img = pipe(prompt, negative_prompt=neg, num_inference_steps=32, guidance_scale=6.5, width=1216, height=832,
               generator=torch.Generator("cpu").manual_seed(seed)).images[0]
    img.save(f"work/ref_{seed}.png")
    print("saved", seed, flush=True)
