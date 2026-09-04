# Benchmarking Instructions

```bash
# Run all benchmarks
dotnet run -c Release

# Run specific benchmark
dotnet run -c Release -- --filter '*Download*'

# Run with JSON export (for CI)
dotnet run -c Release -- --exporters json

# Run in CI with specific config
dotnet run -c Release -- --filter '*' --memory
```
