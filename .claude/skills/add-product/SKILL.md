---
name: add-product
description: Add a product to the Elasticsearch index with optional variants
model: haiku
allowed-tools: Bash
---

Parse the user's product specification and create a product via the API.

## Input Format
Natural language like: `iPhone 17 Pro, Electronics, $1199, Black and Silver variants`

## Request Schema
```json
{
  "name": "string (required)",
  "description": "string (required - generate if not provided)",
  "category": "string (required)",
  "price": decimal (required),
  "tags": ["array", "of", "strings"] (optional - infer from product),
  "inStock": true (default),
  "variants": [
    {
      "sku": "string (generate unique)",
      "size": "string or null",
      "color": "string or null",
      "priceAdjustment": decimal,
      "stock": int
    }
  ] (optional)
}
```

## Steps
1. Extract name, category, price from user input
2. Generate a brief description if not provided
3. Infer relevant tags from product name/category
4. Parse any variant specifications (colors, sizes)
5. Generate SKUs for variants (e.g., `PROD-BLK-001`)
6. POST to API:
   ```bash
   curl -ks -X POST "https://localhost:7232/api/products" \
     -H "Content-Type: application/json" \
     -d '<json>'
   ```
7. Report: product ID on success, error message on failure