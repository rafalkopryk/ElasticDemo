---
name: semantic-search
description: Search products using natural language queries (vector similarity)
argument-hint: "<query> [category] [minPrice] [maxPrice] [k=10] [numCandidates=100] [similarity=0.8]"
model: haiku
allowed-tools: Bash
---

Parse the user's search query and perform semantic/vector search via the API.

## Input Format
Natural language like:
- `lightweight running shoes under $150`
- `cozy winter jacket, Clothing`
- `durable outdoor equipment $50-$200`

## Request Schema
```json
{
  "query": "string (required - the semantic search text)",
  "category": "string (optional - filter by category)",
  "minPrice": decimal (optional),
  "maxPrice": decimal (optional),
  "k": 10 (default - number of results to return),
  "numCandidates": 100 (default),
  "similarity": float (optional - minimum similarity threshold, 0.0-1.0)
}
```

## Steps
1. Extract the core search query from user input
2. Parse optional filters:
   - Category names: Electronics, Clothing, Sports, Home & Garden, Books, Toys
   - Price: "under $X" → maxPrice, "$X-$Y" → minPrice/maxPrice
3. POST to API:
   ```bash
   curl -ks -X POST "https://localhost:7232/api/products/semantic-search" \
     -H "Content-Type: application/json" \
     -d '<json>'
   ```
4. Display results in readable format:
   - Product name, price, category
   - Brief description snippet (first ~100 chars)
   - Score if available
5. Report total matches found