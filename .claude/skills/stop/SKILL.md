---
name: stop
description: Stop the running Aspire application
model: haiku
allowed-tools: Bash
---

1. Stop the Aspire application:

```bash
pkill -9 -f "aspire run"
```

2. Verify it's stopped:

```bash
pgrep -fl "aspire run" || echo "Aspire application stopped."
```
