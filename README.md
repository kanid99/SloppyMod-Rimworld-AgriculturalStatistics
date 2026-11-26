# SloppyMods Agricultural Statistics

A RimWorld 1.6 / 1.7 mod that adds a **Farming** tab: every growing zone, hydroponics basin and plant pot,
what it grows, how much nutrition it produces per day, and how many people that feeds eaten raw, cooked into
simple meals, or as nutrient paste, compared with what your colony actually eats.

- **Right now** or **year average** (outdoor fields follow the map's growing season).
- Animal feed crops (haygrass) are counted against your animals, not your colonists.
- Warnings for zones that are too cold, too dark, on poor soil, or with sowing turned off.
- Click a row to jump to the zone.

Everything is read from the game's data, so modded crops, meals and races work without patches.
No Harmony, no dependencies, safe to add or remove mid-save.

## How the numbers work

Per cell, nutrition per day = harvest yield x nutrition per harvested item / real days per harvest, where real
days = grow days x (1 - regrowth after harvest) / 0.55 / growth rate. Plants only grow in daylight (vanilla rests
them for 45% of the day), and the growth rate is the product of the soil, light and temperature factors the game
itself uses. Yield includes the difficulty's crop yield and, optionally, your best grower's harvest yield.

"Cooked" uses the simple meal recipe's ratio (0.5 nutrition of ingredients into a 0.9 meal in vanilla), "paste"
the dispenser's (0.3 into 0.9). Sowing and harvesting time, blight and plant damage are not counted, so these are
best-case figures.

## Building

    bash .github/release.sh --no-release

Licence: CC0.
