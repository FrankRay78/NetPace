# NetPace User Guide

Run a simple speed test using the nearest available server:  
```bash
NetPace
```

Produce CSV output with test results in megabits, suitable for parsing:  
```bash
NetPace --csv --unit-scale Mega
```

Run download test only:  
```bash
NetPace --no-upload
```

Run upload test only:  
```bash
NetPace --no-download
```

Run speed tests continuously, with a 15 minute delay between each:  
```bash
NetPace --loop --delay 00:15:00
```

Run 3 tests in a row, with 30 seconds delay between each:  
```bash
NetPace --count 3 --delay 00:00:30
```

List the nearest speed test servers:  
```bash
NetPace servers
```

Use a specific server URL for testing:  
```bash
NetPace --server https://speedtest.example.com/
```

Show speeds in bytes/sec, using IEC units (KiB, MiB, GiB):  
```bash
NetPace --unit BytesPerSecond --unit-system IEC
```

Add a timestamp in custom date format:  
```bash
NetPace --timestamp --datetimeformat "dd/MM/yyyy HH:mm"
```

Limit download to 50 MiB and upload to 20 MiB (for low bandwidth connections):  
```bash
NetPace --downloadsize 50 --uploadsize 20
```

---

For more options and details, run:  
```bash
NetPace --help
```

To report problems or suggest features, visit [GitHub Issues](https://github.com/FrankRay78/NetPace/issues).
