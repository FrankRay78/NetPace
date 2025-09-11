# NetPace User Guide

This guide will help you get the most out of your network speed testing.

---

## Basic Usage

To run a simple speed test using the nearest available server:

```bash
NetPace
```

---

## Common Scenarios

### Minimal CSV Output

For scripting or parsing results with minimal output:

```bash
NetPace --csv
```

### Download Only

To test only download speed (skip upload):

```bash
NetPace --no-upload
```

### Upload Only

To test only upload speed (skip download):

```bash
NetPace --no-download
```

### Multiple Tests with Delay

To run several tests in a row with a delay between each:

```bash
NetPace --count 3 --delay 00:00:30
```
(This runs 3 tests with a 30 second delay between each)

### Find Nearest Servers

To list the closest speed test servers:

```bash
NetPace servers
```

### Use a Specific Server

To specify a server URL for testing:

```bash
NetPace --server https://speedtest.example.com/
```

### Show Speeds in Bytes/Second and IEC Units

Display results in bytes per second using IEC units (KiB, MiB, GiB):

```bash
NetPace --unit BytesPerSecond --unit-system IEC
```

### Include Timestamp with Custom Date Format

Add a timestamp to the results, using a custom format (example here uses "dd/MM/yyyy HH:mm"):

```bash
NetPace --timestamp --datetimeformat "dd/MM/yyyy HH:mm"
```

### Restrict Payload Size for Low Bandwidth Connections

Limit the size of the download and upload tests (useful for slow or metered connections):

```bash
NetPace --downloadsize 50 --uploadsize 25
```
(This limits the download test to 50 MiB and the upload test to 25 MiB)

---

## Advanced Usage

You can always run:

```bash
NetPace --help
```

to see the full list of options and usage instructions.

---

## Troubleshooting

- **Platform:** Ensure you are running the correct binary for your operating system (Windows, Linux, or macOS).
- **Permissions:** Some systems may require elevated permissions for network tests.
- **Server Selection:** If you experience issues with a server, try another from the `NetPace servers` list.
- **Issues & Feedback:** Report problems or suggest features at [GitHub Issues](https://github.com/FrankRay78/NetPace/issues).