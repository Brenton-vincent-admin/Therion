# Island preview (no Unity needed)

`island.py` is a Python copy of the island generator in `Assets/_Game/Scripts/World/Generation`. It draws the
island top-down with biomes, water, nests (yellow), drinking puddles (blue), the volcano (triangle), the
waterfall (square) and the Great Old Tree (green ring), and prints the coverage numbers.

```
pip install numpy pillow
python island.py preview.png
```

Takes about 20 seconds. If you change a layout number in `IslandSpec.cs` (`ResetToDefaults`), change the
same constant at the top of `island.py` so the preview stays truthful. The noise functions are identical, so
the shapes match; food and decor scattering is not previewed.
