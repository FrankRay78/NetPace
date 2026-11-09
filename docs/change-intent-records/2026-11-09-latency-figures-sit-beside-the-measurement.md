# Latency Figures Sit Beside The Measurement They Belong To

**Intent:** Place the under-load latency and idle jitter figures in CSV and JSON so a reader finds each beside the measurement it describes, accepting that it moves every column after it.

**Behaviour:** See issue #527 and its confirmed decisions for the names and the order.

**Constraints:**

- The names are an interface contract that 1.0 freezes: idle `JitterMilliseconds`, and per direction `…LatencyMilliseconds`, `…LowestLatencyMilliseconds`, `…HighestLatencyMilliseconds`, `…JitterMilliseconds`, the upload mirroring the download word for word.
- The JSON measurement properties are declared in the CSV column order, because the source generator writes them in declaration order and the two formats must read alike.

**Decisions:**

- Each figure beside its measurement. Rejected: appending the nine columns after the existing ones, which would have kept every CSV position stable — but it separates a direction's latency from its speed for good, and pre-1.0 is the only time the layout can still be fixed.
- An unmeasured figure is an empty cell and an absent field. Rejected: zero — a chart would read it as a perfect line, and a transfer whose every request failed reports exactly that.

**Date:** 2026-11-09
