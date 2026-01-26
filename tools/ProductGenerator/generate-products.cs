#!/usr/bin/env dotnet run
#:property JsonSerializerIsReflectionEnabledByDefault=true

using System.Text.Json;

// Configuration - parse command line arguments
var outputPath = Path.Combine("src", "ElasticDemo.Api", "Features", "Products", "sample-products.json");
var totalCount = 1000; // Default total
var writeIndented = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--count" or "-c" when i + 1 < args.Length:
            totalCount = int.Parse(args[++i]);
            break;
        case "--indented" or "-i":
            writeIndented = true;
            break;
        case "--output" or "-o" when i + 1 < args.Length:
            outputPath = args[++i];
            break;
        case var arg when !arg.StartsWith('-'):
            outputPath = arg; // Legacy: positional arg is output path
            break;
    }
}

// Category definitions with product types, price ranges, and distribution weights
var categoryDefs = new Dictionary<string, (string[] Types, decimal MinPrice, decimal MaxPrice, double Weight)>
{
    ["Electronics"] = (["Laptop", "Monitor", "Keyboard", "Mouse", "Webcam", "Processor", "Graphics Card", "Motherboard"], 50, 2500, 0.275),
    ["Audio"] = (["Headphones", "Speakers", "Earbuds", "Microphone", "Soundbar", "Amplifier"], 20, 800, 0.165),
    ["Accessories"] = (["USB Cable", "Hub", "Adapter", "Stand", "Dock", "Charger", "Case", "Screen Protector"], 10, 150, 0.220),
    ["Storage"] = (["SSD", "HDD", "USB Drive", "Memory Card", "NAS", "External Drive"], 30, 500, 0.110),
    ["Furniture"] = (["Desk", "Chair", "Shelf", "Monitor Stand", "Desk Mat", "Cable Management", "Footrest"], 50, 1000, 0.115),
    ["Gaming"] = (["Controller", "Gaming Chair", "VR Headset", "Gaming Mouse", "Gaming Keyboard", "Mousepad", "Headset Stand"], 30, 1500, 0.115)
};

// Calculate counts based on total and weights
var categories = categoryDefs.ToDictionary(
    kvp => kvp.Key,
    kvp => (kvp.Value.Types, kvp.Value.MinPrice, kvp.Value.MaxPrice, Count: (int)(totalCount * kvp.Value.Weight))
);

var prefixes = new[] { "Pro", "Elite", "Ultra", "Premium", "Basic", "Advanced", "Gaming", "Business", "Home", "Studio" };
var tags = new[] { "new", "bestseller", "sale", "premium", "budget", "wireless", "ergonomic", "rgb", "compact", "portable", "high-performance", "smart", "usb", "bluetooth", "tech", "computer", "digital" };
var colors = new[] { "Black", "White", "Silver", "Space Gray", "Navy", "Red", "Blue" };
var sizes = new[] { "Small", "Medium", "Large", "XL" };

var random = new Random(42); // Fixed seed for reproducibility
var productId = 1;
var fullPath = Path.GetFullPath(outputPath);
Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

// Stream products directly to file
using var fileStream = File.Create(fullPath);
using var writer = new Utf8JsonWriter(fileStream, new JsonWriterOptions { Indented = writeIndented });

writer.WriteStartArray();

var categoryCounts = new Dictionary<string, int>();
var progressInterval = Math.Max(totalCount / 100, 1000); // Report progress every 1% or 1000 products

foreach (var (category, (types, minPrice, maxPrice, count)) in categories)
{
    categoryCounts[category] = count;

    for (var i = 0; i < count; i++)
    {
        var type = types[random.Next(types.Length)];
        var prefix = prefixes[random.Next(prefixes.Length)];
        var suffix = random.Next(10) > 6 ? $" {(char)('A' + random.Next(26))}{random.Next(100, 999)}" : "";
        var version = random.Next(10) > 7 ? $" {random.Next(1, 6)}.0" : "";

        var name = $"{prefix} {type}{suffix}{version}";
        var price = Math.Round((decimal)(random.NextDouble() * (double)(maxPrice - minPrice) + (double)minPrice), 2);

        var productTags = tags.OrderBy(_ => random.Next()).Take(random.Next(2, 5)).ToList();
        var inStock = random.Next(10) > 0; // 10% out of stock
        var createdAt = DateTime.UtcNow.AddDays(-random.Next(1, 365));

        // Write product object
        writer.WriteStartObject();
        writer.WriteString("id", $"prod-{productId:D4}");
        writer.WriteString("name", name);
        writer.WriteString("description", GenerateDescription(prefix, type, category));
        writer.WriteString("category", category);
        writer.WriteNumber("price", price);

        // Write tags array
        writer.WritePropertyName("tags");
        writer.WriteStartArray();
        foreach (var tag in productTags)
        {
            writer.WriteStringValue(tag);
        }
        writer.WriteEndArray();

        writer.WriteBoolean("inStock", inStock);
        writer.WriteString("createdAt", createdAt);

        // Write variants array
        writer.WritePropertyName("variants");
        writer.WriteStartArray();

        if (random.Next(10) > 6) // ~30% have variants
        {
            var variantCount = random.Next(2, 5);
            for (var v = 0; v < variantCount; v++)
            {
                writer.WriteStartObject();
                writer.WriteString("sku", $"SKU-{productId:D4}-{v + 1}");
                writer.WriteString("color", colors[random.Next(colors.Length)]);
                if (random.Next(2) == 0)
                {
                    writer.WriteString("size", sizes[random.Next(sizes.Length)]);
                }
                else
                {
                    writer.WriteNull("size");
                }
                writer.WriteNumber("priceAdjustment", Math.Round((decimal)(random.NextDouble() * 50 - 25), 2));
                writer.WriteNumber("stock", random.Next(0, 100));
                writer.WriteEndObject();
            }
        }

        writer.WriteEndArray(); // End variants
        writer.WriteEndObject(); // End product

        productId++;

        // Show progress for large datasets and flush buffer periodically
        if (productId % progressInterval == 0)
        {
            writer.Flush(); // Prevent buffer from growing too large
            Console.Write($"\rGenerating: {productId:N0} / {totalCount:N0} products...");
        }
    }
}

writer.WriteEndArray();
writer.Flush();

Console.WriteLine($"\rGenerated {totalCount:N0} products to: {fullPath}");
Console.WriteLine($"Categories: {string.Join(", ", categoryCounts.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}");

static string GenerateDescription(string prefix, string type, string category)
{
    var descriptions = new[]
    {
        $"High-quality {type.ToLower()} designed for professionals. Features {prefix.ToLower()} design with premium materials.",
        $"Experience the best in {category.ToLower()} with this {prefix.ToLower()} {type.ToLower()}. Built to last with premium components.",
        $"Professional-grade {type.ToLower()} with {prefix.ToLower()} performance. Designed for demanding users.",
        $"The ultimate {type.ToLower()} for {category.ToLower()} enthusiasts. Combines style with functionality.",
        $"Top-tier {type.ToLower()} featuring advanced technology. Perfect for both work and play."
    };
    return descriptions[new Random().Next(descriptions.Length)];
}
