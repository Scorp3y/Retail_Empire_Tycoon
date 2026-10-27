# Shopper lineup — concept v1

Status: concept art only; no meshes, rigs or Unity prefabs have been generated for this lineup.

Generator: built-in image_gen (not the API/CLI fallback).

Final asset: `ShopperLineup-v1.png` — visually checked: 10 columns × 5 rows, 50 full-body characters, sequential IDs 01–50, all labels visible.

Reference roles: Unity asset browser screenshot is a supporting style reference; the close-up smiling ginger-haired miniature man is the primary proportions and geometry reference. Reference screenshots are not edit targets.

## Initial generation prompt

Use case: stylized-concept.
Asset type: concept-art catalogue for 50 future low-poly shopper NPC game models, NOT actual model files.
Input images: first supplied image is only a supporting reference of the existing Unity mini-character collection. Second supplied image (the smiling reddish-haired suited miniature man) is the PRIMARY shape, face and polygon-budget reference. Do not reproduce the Unity interface or environment.

Create ONE large, high-resolution landscape character design contact sheet, preferably 3840x2160, with EXACTLY 50 DIFFERENT ordinary supermarket customers arranged in a precise grid of 10 columns by 5 rows. Exactly one full-body character in each cell, no extra figures. All fifty visible head to shoes, none cropped, evenly sized and spaced. Use a pale warm gray background, very subtle thin grid separators, a small unobtrusive header "SHOPPERS — 50 LOW POLY CHARACTERS", and ONLY the two-digit number beneath each character. Labels run 01–10 on row 1, 11–20 on row 2, 21–30 on row 3, 31–40 on row 4, 41–50 on row 5, without skipped or duplicated numbers.

STYLE MUST closely match the primary existing game NPC: extremely simple chunky low-poly mini adults with enormous softly chamfered BOX-SHAPED heads, tiny squat torso, short thick separate limbs, simple block mitt hands, big blocky shoes. The head is almost half the overall height, the body including legs roughly the other half. Very small simple black curved/dot eyes and a friendly painted curved smile; no detailed noses, no eye highlights, no eyelashes, no realistic facial modelling. Hair is a few large faceted solid angular masses, NOT detailed strands. Simple solid-color clothing meshes with occasional broad color bands, no fabric texture or tiny wrinkles, matte flat-shaded polygonal surfaces. This is NOT smooth Pixar figurines, NOT Funko cylindrical vinyl, NOT Lego construction toys, NOT realistic humans, NOT tall humanoid proportions. Keep the reference's low geometry simplicity and cohesive chunky silhouette. Gentle ambient occlusion and soft consistent studio light, short grounded soft shadows. Neutral relaxed A-pose with arms angled outward enough to show outfit. Same mild front three-quarter camera angle for every character, faces fully legible. Distinguish designs by clothing silhouettes, hairstyles, color combinations and some simple glasses/hats, rather than making fifty recolors. Include a balanced range of men and women, adult ages including some elderly shoppers, and varied skin tones. They are shoppers, not store employees: no weapons, no fantasy costumes, no police/firefighter/medical uniforms, no aprons, no shopping carts obscuring bodies. Each design must be straightforward to build later with one shared low-poly rig and palette textures. No held props needed.

Exact cell design plan in reading order:
01 man, ginger swept chunky hair, dark suit with white shirt and red tie, like reference as the style anchor.
02 woman, dark brown bob, mustard cardigan, navy trousers, cream shoes.
03 man, dark skin, short black hair, teal hoodie, charcoal jeans, white sneakers.
04 woman, blonde ponytail, coral tee, denim skirt, white sneakers.
05 older man, gray hair, small square glasses, olive vest over cream shirt, brown trousers.
06 woman, medium brown skin, black bun, plum sweater, pale gray trousers.
07 man, navy baseball cap, burgundy sweatshirt, blue jeans.
08 woman, red bob, forest-green jacket, cream tee, dark trousers.
09 man, black curly block hair, orange jacket, black trousers.
10 older woman, gray bob, lavender cardigan, dark teal skirt.
11 man, brown quiff, blue overshirt, white tee, tan trousers.
12 woman, dark skin, short black coiled hair, turquoise dress, dark shoes.
13 man, light brown beanie, cream hoodie, olive trousers.
14 woman, chestnut ponytail, pale pink jacket, navy jeans.
15 older man, balding head with gray side hair, burgundy vest, beige trousers.
16 woman, blonde bob, navy blazer over white blouse, gray trousers.
17 man, brown skin, black side-part, sage polo, charcoal trousers.
18 woman, olive skin, long black angular hair, rust sweater, cream trousers.
19 man, ginger short beard, denim jacket, tan trousers.
20 woman, white cap, yellow windbreaker, dark blue trousers.
21 man, dark skin, rounded block afro, white broad-striped navy tee, khaki shorts.
22 woman, copper bun, blue hoodie, dark gray trousers.
23 older woman, white short hair, simple round glasses, mint sweater, brown skirt.
24 man, tan skin, shaved head, red polo, blue jeans.
25 woman, brown wavy bob, mustard raincoat, dark trousers.
26 man, black side-part, pale gray suit, teal shirt.
27 woman, dark skin, black ponytail, orange cardigan, white blouse, navy trousers.
28 man, green beanie, dark teal puffer vest, cream sleeves, brown trousers.
29 woman, blonde hair, maroon sweater dress, dark boots.
30 man, reddish brown hair, pale blue shirt, charcoal trousers, simple glasses.
31 woman, short black bob, cream sweater with one green broad stripe, blue jeans.
32 man, olive skin, brown curly block hair, lilac hoodie, gray trousers.
33 older man, white mustache, blue cap, tan jacket, navy trousers.
34 woman, medium brown skin, black bun, sage long coat, cream trousers.
35 man, ginger messy hair, bright yellow tee, charcoal shorts.
36 woman, auburn shoulder-length block hair, teal blouse, brown skirt.
37 man, dark skin, very short black hair, brick-red jacket, tan trousers.
38 woman, ash blonde short hair, black simple glasses, gray cardigan, purple trousers.
39 man, brown hair, dark green sweater with cream collar, beige trousers.
40 woman, olive skin, dark ponytail, coral jacket, black leggings and white shoes.
41 older woman, silver bun, blue cardigan, cream skirt.
42 man, medium brown skin, black quiff, white polo with broad teal stripe, navy trousers.
43 woman, strawberry blonde bob, olive overalls over cream tee.
44 man, black beanie, gray zip hoodie, dark jeans.
45 woman, dark skin, blocky natural curls, mustard blouse, terracotta trousers.
46 man, brown hair and short beard, navy cardigan, light shirt, olive trousers.
47 woman, dark brown side-part bob, burgundy blazer, gray skirt.
48 man, tan skin, dark brown hair, turquoise windbreaker, charcoal trousers.
49 older woman, short white hair, coral sweater, navy trousers, square glasses.
50 man, sandy blonde hair, rust sweatshirt, dark olive trousers.

All characters remain the SAME GAME STYLE and near-identical squat proportions as the reference, with attractive readable color blocks. No decorative scenery or dramatic cinematic rendering. Prioritize exact count, numbered organization, recognisable variety and very simple modellable geometry.

## Targeted correction prompt

Edit the generated 50-character concept sheet. Preserve all fifty designs, outfits, colors, expressions, positions, proportions, low-poly shading, header and the 10-column × 5-row layout. Change only number labels and outer margins. Add sufficient bottom padding so every shoe and full two-digit label is visible. Keep exactly 50 characters and label them strictly in reading order:

- Row 1: 01 02 03 04 05 06 07 08 09 10
- Row 2: 11 12 13 14 15 16 17 18 19 20
- Row 3: 21 22 23 24 25 26 27 28 29 30
- Row 4: 31 32 33 34 35 36 37 38 39 40
- Row 5: 41 42 43 44 45 46 47 48 49 50

Do not redesign characters, repeat numbers, add figures or crop the image edges.

