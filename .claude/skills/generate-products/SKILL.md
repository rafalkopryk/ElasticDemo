---
name: generate-products
description: Generate sample product data for testing.
argument-hint: "[count] [indented]"
disable-model-invocation: true
allowed-tools: Bash(dotnet run*)
model: sonnet
---

If `$1` is `true`: run `dotnet run tools/ProductGenerator/generate-products.cs -- --count $0 --indented`
Otherwise: run `dotnet run tools/ProductGenerator/generate-products.cs -- --count $0`

`$0`: count (default 1000). `$1`: `true` for indented output, `false` or omit for compact.

Report count and output path.
