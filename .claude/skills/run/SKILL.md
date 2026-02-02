---
name: run
description: Start the Aspire application in background and display service addresses
model: haiku
allowed-tools: Bash
---

1. Start the Aspire application in the background:

```bash
aspire run &
disown
```

2. Wait for services to start, then detect the Aspire Dashboard port:

```bash
sleep 8
for port in $(lsof -i -P | grep "dotnet.*LISTEN" | grep -oE "localhost:[0-9]+" | cut -d: -f2 | sort -u); do
  if curl -s -o /dev/null -w "%{http_code}" http://localhost:$port 2>/dev/null | grep -qE "^(200|307)$"; then
    echo "Aspire Dashboard: http://localhost:$port"
    break
  fi
done
```

3. Display service addresses:

```
Application started in background.

Service Addresses:
- Aspire Dashboard: <detected URL from step 2>
- API (HTTP):       http://localhost:5275
- API (HTTPS):      https://localhost:7232
- Elasticsearch:    http://localhost:9200
- Kibana:           http://localhost:5601
- OpenAPI spec:     http://localhost:5275/openapi/v1.json
```
