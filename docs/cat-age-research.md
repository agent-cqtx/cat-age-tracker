# Cat age research notes

Background research behind the age math and life-stage content in this app,
kept here per your "save research as a guide" preference. Last checked
August 2026.

## The cat-year → human-year conversion

There's no scientific formula for this — cats and humans don't age on
comparable biological clocks — but there's a widely repeated veterinary
rule of thumb often called the **"15-9-4" model**, cited consistently by
sources including PetMD, Purina, the Old Farmer's Almanac, and various vet
clinics:

- A cat reaches the equivalent of **15** human years at age **1**.
- It reaches **24** human years at age **2** (+9 in the second year).
- After that, each additional cat year is worth roughly **+4** human years.

So a 5-year-old cat is "about 36," a 10-year-old is "about 56," and so on.
Some sources round the post-2 rate to a range of 4–6 rather than a flat 4;
this app uses the more common flat +4/year for predictability.

**What this app does differently:** most public charts jump straight from
"0" to "15 at one year," which makes the first year of the slider feel like
a hard cliff. A few sources note a 6-month checkpoint of roughly 10 human
years, so this app interpolates through that point too. The resulting curve
(implemented in `Services/CatAgeMath.cs`):

| Cat age | Human-equivalent age |
|---|---|
| 0 | 0 |
| 0.5 yr (6 mo) | 10 |
| 1 yr | 15 |
| 2 yr | 24 |
| 3 yr | 28 |
| 6 yr | 40 |
| 10 yr | 56 |
| 15 yr | 76 |

Anything between those points is linearly interpolated; anything past 2
years is `24 + (years − 2) × 4`.

Sources consulted: PetMD's cat age chart, Purina's cat-years guide, the Old
Farmer's Almanac cat age calculator, Chewy's and PetPlace's cat age charts,
and Pumpkin Pet Insurance's breakdown of the same "15-9-4" model. All of
them agree on the three checkpoints above; none of them claim precision
beyond "rough guide."

## Life stages

Veterinary organizations don't fully agree on how many life stages to name.
Two frameworks came up repeatedly:

- **2021 AAHA/AAFP Feline Life Stage Guidelines** (the current official
  standard from the American Animal Hospital Association and American
  Association of Feline Practitioners) simplified things to **four**
  stages: Kitten (birth–1 yr), Young adult (1–6 yr), Mature adult (7–10
  yr), Senior (10+ yr), plus a separate non-age-specific End-of-life stage.
- The **2010 AAFP-AAHA guidelines** (still widely quoted by consumer pet
  sites) used **six**, finer-grained stages: Kitten (0–6 mo), Junior
  (7 mo–2 yr), Prime (3–6 yr), Mature (7–10 yr), Senior (11–14 yr),
  Geriatric (15+ yr).

This app uses the **six-stage** version (`Data/LifeStageCatalog.cs`),
because it gives the "scroll through their life" timeline more distinct
checkpoints to show, rather than one flat "young adult" zone spanning five
years. The boundaries were smoothed slightly (e.g. Junior ends and Prime
begins at exactly 2 years, instead of the small 2–3yr gap in the original)
so every age maps to exactly one stage with no gaps or overlaps.

If you'd rather match the current official four-stage framework exactly,
that's a small change — just replace the contents of `LifeStageCatalog.cs`.

Sources: the AAHA's own summaries of the 2021 guidelines (aaha.org), AVMA's
coverage of the same update, and a ResearchGate-hosted copy of the original
2010 AAFP-AAHA guidelines for the six-stage breakdown.

## Wording of the Kitten/Junior descriptions

The Kitten and Junior stage descriptions are written about safe, general
developmental facts true of essentially all kittens — teeth timing, weaning
age, sexual maturity age, growth-plate closure — since every cat has
already passed through those stages by the time most people are using an
age tracker. The Prime/Mature/Senior/Geriatric descriptions are written
more generically (focused on care/monitoring rather than one-time
developmental milestones), since which of those stages a given cat has
reached varies.

## Caveats worth keeping in mind

- This is a **rough population-level guide**, not a prediction for any
  individual cat. Breed, weight, indoor/outdoor lifestyle, and genetics all
  matter more than the chart.
- None of this is veterinary advice. The in-app footer says as much, and
  it's worth remembering if you're ever using the life-stage notes to
  decide whether something is "normal."
- If AAHA/AAFP publish a further revision, the numbers above should be
  re-checked — this is exactly the kind of thing that could be stale by the
  time you read it again.
