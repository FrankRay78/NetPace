# Byte Fields Take the Library's Name

**Intent:** Give CSV and JSON a figure for the bytes each direction moved, under names a reader can trace back to the count the library already exposes.

**Behaviour:** See issue #592 and its confirmed decisions. `USER_GUIDE.md` carries the two field tables and says what the figures leave out.

**Constraints:** A CSV column name and a JSON field name are interface contracts, so the names and the column position are settled here rather than per release. Nothing in `NetPace.Core` changed: the figure is `SpeedTestResult.BytesProcessed`, which library consumers already read.

**Decisions:**

- `DownloadBytesProcessed` / `UploadBytesProcessed`. Rejected: `DownloadBytes` / `UploadBytes`, shorter but no longer traceable to the library member behind them; and `speedtest-cli`'s `bytes_received` / `bytes_sent`, which break the `Download…` / `Upload…` prefix every other per-direction field uses.
- Each figure sits straight after that direction's request counts. Rejected: both appended at the end of the row, which moves fewer columns but separates a figure from the measurement it belongs to.

**Date:** 2026-11-24
