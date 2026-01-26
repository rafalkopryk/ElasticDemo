# Generate Sample Products

Generate sample product data for the ElasticDemo application.

## Usage

```
/generate-products [count] [--indented]
```

## Arguments

- `count` (optional): Number of products to generate. Default: 1000. Examples: 10000, 100000, 1000000
- `--indented` (optional): If specified, output JSON will be pretty-printed

## Instructions

Run the product generator script with the provided arguments:

```bash
dotnet run tools/ProductGenerator/generate-products.cs -- --count $COUNT $INDENTED_FLAG
```

Where:
- `$COUNT` is the count argument (default 1000 if not provided)
- `$INDENTED_FLAG` is `--indented` if the user requested indented output, otherwise empty

After running, report how many products were generated and the output file path.
