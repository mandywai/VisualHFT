## Data Recorder Plugin

The **Data Recorder** plug‑in is a built‑in study that stores live market data and selected study metrics to disk for later analysis.

### What it records
- Selected market fields such as best bid/ask, sizes, mid price, spread, imbalance, sequence and optional depth levels.
- Selected study outputs emitted by other VisualHFT studies, for example **LOB Imbalance** or any VPIN profile that appears in the study picker.

### Capture modes
- **Event driven**: write a row whenever the selected market stream or selected study updates.
- **Time driven**: write one snapshot row on a fixed interval using the latest known market and study values.

### Output format
- Each session writes to its own output folder with:
  - `data.jsonl`: flat row-based records
  - `metadata.json`: session metadata and configuration
- JSONL rows are intentionally flat and data-oriented, for example:
  - `row_timestamp_utc`
  - `market_best_bid_price`
  - `market_mid_price`
  - `study_lob_imbalance`

### Settings
- Choose provider/exchange and symbol.
- Choose output folder.
- Choose capture mode: **Event driven** or **Time driven**.
- Choose fixed interval when using **Time driven** mode.
- Choose whether the recorder runs indefinitely or for a fixed duration in minutes.
- Choose which market fields and study metrics to persist.

### Metadata
- `metadata.json` is written at start and updated again when recording stops.
- It includes the session start/end time, provider, symbol, capture mode, interval, selected fields, selected studies, output location and record count.

