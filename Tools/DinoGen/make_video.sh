#!/usr/bin/env bash
# Joins the rendered clip frames into one labelled MP4.
set -e
C="$1"; OUT="$2"; FONT=/usr/share/fonts/liberation/LiberationSans-Bold.ttf
TMP=$(mktemp -d); : > "$TMP/list.txt"
for spec in "Idle:0:Idle" "Walk:3:Walk" "Run:5:Run" "Attack:1:Attack (bite)" "Ability:0:Ability (roar)" "Eat:0:Eat" "Drink:0:Drink" "Rest:0:Rest" "Hurt:2:Hurt" "Faint:0:Faint"; do
  IFS=: read -r name loops label <<< "$spec"
  [ -d "$C/$name" ] || continue
  fps=$(cat "$C/$name/fps.txt")
  ffmpeg -loglevel error -y -stream_loop "$loops" -framerate "$fps" -i "$C/$name/%04d.png" \
    -vf "drawtext=fontfile=$FONT:text='$label':x=24:y=20:fontsize=34:fontcolor=white:box=1:boxcolor=black@0.55:boxborderw=10,fps=30,format=yuv420p" \
    -c:v libx264 -crf 20 "$TMP/$name.mp4"
  echo "file '$TMP/$name.mp4'" >> "$TMP/list.txt"
done
ffmpeg -loglevel error -y -f concat -safe 0 -i "$TMP/list.txt" -c copy "$OUT"
rm -rf "$TMP"; ls -la "$OUT"
