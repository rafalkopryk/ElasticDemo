---
name: seed-reset
description: "Resets Elasticsearch indices. Args: 'products' (default), 'applications', or 'all'."
argument-hint: "[products|applications|all]"
model: haiku
allowed-tools: Bash
---

The first argument (`$0`) selects which index to reset: `products` (default if no arg), `applications`, or `all`.

## Products reset (when arg is `products`, empty, or `all`)

Run these curl commands sequentially, reporting success/failure:

1. `curl -s -X DELETE "http://localhost:9200/products"` (ignore 404)
2. `curl -s -X DELETE "http://localhost:9200/products-archive-*"` (ignore 404)
3. `curl -s -X DELETE "http://localhost:9200/_index_template/products-archive-template"` (ignore 404)
4. `curl -ks -X POST "https://localhost:7232/api/products/init"`
5. `curl -ks -X POST "https://localhost:7232/api/products/seed" -F "file=@src/ElasticDemo.Api/Features/Products/sample-products.json"`

Verify: `curl -s "http://localhost:9200/products/_count"` — report product count.

## Applications reset (when arg is `applications` or `all`)

Run these curl commands sequentially, reporting success/failure:

1. `curl -s -X DELETE "http://localhost:9200/applications"` (ignore 404)
2. `curl -ks -X POST "https://localhost:7232/api/applications/init"`
3. `curl -ks -X POST "https://localhost:7232/api/applications/seed" -F "file=@src/ElasticDemo.Api/applications.json"`

Verify: `curl -s "http://localhost:9200/applications/_count"` — report application count.
