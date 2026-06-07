## VPIN Multi Plugin

The **VPIN Multi** plug‑in is a study plug‑in that lets you run multiple VPIN configurations on the same provider and symbol at the same time.

### What it does
- Creates multiple VPIN profiles in one plug‑in instance.
- Each profile has its own bucket volume size and number of buckets.
- Each profile is exposed as its own study metric, so it can be charted, used in triggers and selected by the recorder.

### Naming
- Each VPIN profile is named from its settings using:
  - `VPIN_<numberOfBuckets>_<bucketVolumeSize>`
- Example:
  - `VPIN_50_5`

### Recorder integration
- The Data Recorder can store VPIN Multi outputs as separate columns.
- For example, selecting two VPIN profiles can produce columns such as:
  - `study_vpin_50_5`
  - `study_vpin_100_20`

