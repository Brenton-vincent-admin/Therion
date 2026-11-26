import torch
from diffusers import StableDiffusionXLPipeline
pipe = StableDiffusionXLPipeline.from_pretrained("stabilityai/stable-diffusion-xl-base-1.0", torch_dtype=torch.float16, variant="fp16", use_safetensors=True)
pipe.enable_model_cpu_offload()
prompt = ("a Carnotaurus dinosaur, whole animal in frame, full body three-quarter side view, standing still in a neutral "
          "horizontal pose with two legs side by side, tail held straight out, mouth closed, short deep blunt head with two "
          "short thick horns above the eyes, tiny stubby arms, long muscular hind legs, rows of small bumps along the back "
          "and flanks, detailed pebbly scaly skin, sandy ochre body with dark charcoal bands and a pale cream belly, "
          "realistic paleoart reconstruction, photorealistic 3d render, soft even studio lighting, isolated on a plain white background")
neg = ("cropped, cut off, text, watermark, logo, multiple animals, scenery, landscape, trees, ground, grass, rocks, engraving, "
       "drawing, illustration, sketch, vintage, cartoon, toy, blurry, extra legs, three legs, extra heads, open mouth, "
       "running, red skin, spikes, sail")
for seed in (3, 8, 15, 21, 34, 55):
    img = pipe(prompt, negative_prompt=neg, num_inference_steps=34, guidance_scale=6.5, width=1216, height=832,
               generator=torch.Generator("cpu").manual_seed(seed)).images[0]
    img.save(f"carno/refb_{seed}.png")
    print("saved", seed, flush=True)
