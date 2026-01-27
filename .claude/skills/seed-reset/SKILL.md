---
name: seed-reset
description: Resets the products Elasticsearch index by deleting it, reinitializing, and reseeding sample data.
model: haiku
allowed-tools: Bash
---

Run these curl commands sequentially, reporting success/failure:

1. `curl -s -X DELETE "http://localhost:9200/products"`
2. `curl -ks -X POST "https://localhost:7232/api/products/init"`
3. `curl -ks -X POST "https://localhost:7232/api/products/seed"`

Verify: `curl -s "http://localhost:9200/products/_count"` — report product count.
