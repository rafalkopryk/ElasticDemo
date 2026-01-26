---
name: generate-products
description: Generate sample product data for the ElasticDemo application. Use when the user wants to regenerate or create new sample products for testing.
argument-hint: "[count] [--indented]"
disable-model-invocation: true
allowed-tools: Bash(dotnet run*)
model: sonnet
---

# Generate Sample Products

Generate sample product data for the ElasticDemo application using the ProductGenerator tool.

## Arguments

- `count` (optional): Number of products to generate. Default: 1000. Examples: 10000, 100000, 1000000
- `--indented` (optional): If specified, output JSON will be pretty-printed

## Instructions

Run the product generator script:

```bash
dotnet run tools/ProductGenerator/generate-products.cs -- --count $0 $1
```

Where:
- `$0` is the count (default 1000 if not provided)
- `$1` is `--indented` if true, otherwise empty

After running, report how many products were generated and the output file path.
