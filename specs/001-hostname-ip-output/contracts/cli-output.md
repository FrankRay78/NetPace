# CLI Output Contracts: Add Hostname and IP Address to Structured Output

**Feature**: specs/001-hostname-ip-output  
**Phase**: 1 — Design  
**Date**: 2026-04-14

These contracts define the exact output shape that consumers (NMS systems, pipelines, scripts) can rely on.

---

## JSON Output Contract

**Trigger**: `netpace --json` or `netpace --json-pretty`

### Schema

```json
{
  "ServerLocation": "string",
  "ServerSponsor": "string",
  "ServerUrl": "string",
  "Timestamp": "string (formatted per --datetime-format)",
  "Latency": "string (e.g. \"45 ms\")",
  "DownloadSpeed": "string (e.g. \"15.2 Mbps\")",
  "UploadSpeed": "string (e.g. \"3.4 Mbps\")",
  "IPAddress": "string",
  "Hostname": "string"
}
```

### Field Rules

| Field | Always Present | Possible Values | Notes |
|-------|---------------|-----------------|-------|
| `IPAddress` | Yes | IPv4 address, IPv6 address, `""`, `"ERROR"` | First IPv4, else first IPv6, else `""`, else `"ERROR"` on exception |
| `Hostname` | Yes | hostname string, `""`, `"ERROR"` | OS hostname; `""` if empty; `"ERROR"` on exception |

### Example — Normal case

```json
{
  "ServerLocation": "Chicago, IL",
  "ServerSponsor": "Verizon",
  "ServerUrl": "http://speedtest.verizon.net/",
  "Timestamp": "2026-04-14 09:30:00",
  "Latency": "45 ms",
  "DownloadSpeed": "95.4 Mbps",
  "UploadSpeed": "22.1 Mbps",
  "IPAddress": "192.168.1.100",
  "Hostname": "gateway-01.example.com"
}
```

### Example — No network interface

```json
{
  "ServerLocation": "Chicago, IL",
  "ServerSponsor": "Verizon",
  "ServerUrl": "http://speedtest.verizon.net/",
  "Timestamp": "2026-04-14 09:30:00",
  "Latency": "45 ms",
  "DownloadSpeed": "95.4 Mbps",
  "UploadSpeed": "22.1 Mbps",
  "IPAddress": "",
  "Hostname": ""
}
```

### Example — Retrieval error

```json
{
  "ServerLocation": "Chicago, IL",
  "ServerSponsor": "Verizon",
  "ServerUrl": "http://speedtest.verizon.net/",
  "Timestamp": "2026-04-14 09:30:00",
  "Latency": "45 ms",
  "DownloadSpeed": "95.4 Mbps",
  "UploadSpeed": "22.1 Mbps",
  "IPAddress": "ERROR",
  "Hostname": "ERROR"
}
```

---

## CSV Output Contract

**Trigger**: `netpace --csv`

### Header Row

```
Timestamp,Latency,DownloadSpeed,UploadSpeed,IPAddress,Hostname
```

(When `--csv-header-units` is used, speed columns include unit suffixes. `IPAddress` and `Hostname` are always plain labels regardless.)

### Data Row

```
2026-04-14 09:30:00,45 ms,95.4 Mbps,22.1 Mbps,192.168.1.100,gateway-01.example.com
```

### Field Rules

| Column | Always Present | Possible Values | Notes |
|--------|---------------|-----------------|-------|
| `IPAddress` | Yes | IPv4, IPv6, empty, `ERROR` | Same logic as JSON |
| `Hostname` | Yes | hostname, empty, `ERROR` | Same logic as JSON |

### Column Position

`IPAddress` and `Hostname` are the last two columns in both header and data rows. Existing columns are unchanged and retain their current positions.

### Empty value representation

Empty string (`""`) is represented as an empty cell between delimiters: `...,95.4 Mbps,,` (IPAddress is empty here).

---

## Default and Minimal Output Contracts (unchanged)

The default rich terminal output and minimal output do not include hostname or IP address. These contracts are unchanged by this feature.

---

## Backward Compatibility Notes

- JSON: New fields are additive at the end. Consumers using key-based access are unaffected. Consumers using strict schema validation with no unknown fields allowed will need to update their schemas.
- CSV: New columns are appended at the end. Positional consumers that read only the first N columns are unaffected. Consumers that expect an exact column count will need to update.
- NetPace is pre-v1.0; these changes are accepted as intentional.
