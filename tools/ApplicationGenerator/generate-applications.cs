#!/usr/bin/env dotnet run

using System.Text.Json;

// Configuration - parse command line arguments
var outputPath = Path.Combine("..", "..", "src", "ElasticDemo.Api", "applications.json");
var totalCount = 1000;
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
    }
}

var random = new Random(42);

// --- Name pools (Polish names for realism) ---
var firstNames = new[] {
    "Adam", "Piotr", "Krzysztof", "Tomasz", "Paweł", "Andrzej", "Jan", "Marcin", "Marek", "Michał",
    "Grzegorz", "Józef", "Łukasz", "Rafał", "Dariusz", "Jakub", "Robert", "Mateusz", "Kamil", "Artur",
    "Anna", "Maria", "Katarzyna", "Małgorzata", "Agnieszka", "Barbara", "Ewa", "Magdalena", "Monika", "Joanna",
    "Dorota", "Aleksandra", "Natalia", "Beata", "Justyna", "Karolina", "Iwona", "Elżbieta", "Zofia", "Weronika"
};

var lastNames = new[] {
    "Nowak", "Kowalski", "Wiśniewski", "Wójcik", "Kowalczyk", "Kamiński", "Lewandowski", "Zieliński", "Szymański", "Woźniak",
    "Dąbrowski", "Kozłowski", "Jankowski", "Mazur", "Kwiatkowski", "Krawczyk", "Piotrowski", "Grabowski", "Nowakowski", "Pawłowski",
    "Michalski", "Adamczyk", "Dudek", "Zając", "Wieczorek", "Jabłoński", "Król", "Majewski", "Olszewski", "Jaworski",
    "Stępień", "Malinowski", "Górski", "Rutkowski", "Sikora", "Walczak", "Baran", "Laskowski", "Kubiak", "Witkowski"
};

// --- Product definitions ---
var products = new (string Name, string Code, double Weight)[]
{
    ("CashLoan", "CL", 0.50),
    ("Installments", "IL", 0.30),
    ("CreditCards", "CC", 0.15),
    ("Overdraft", "OV", 0.05)
};

var channels = new (string Name, double Weight)[]
{
    ("Online", 0.65),
    ("Branch", 0.25),
    ("Marketing", 0.10)
};

var transactions = new (string Name, double Weight)[]
{
    ("NewProduct", 0.80),
    ("Renewal", 0.15),
    ("Amendment", 0.05)
};

// --- Pre-generate client pool ---
var uniqueClientCount = Math.Max(10, (int)(totalCount * 0.8));
var clients = new List<(string FirstName, string LastName, string NationalId, string ClientId, string Email)>(uniqueClientCount);

var usedNationalIds = new HashSet<string>();
var usedClientIds = new HashSet<string>();

for (var c = 0; c < uniqueClientCount; c++)
{
    var firstName = firstNames[random.Next(firstNames.Length)];
    var lastName = lastNames[random.Next(lastNames.Length)];

    string nationalId;
    do { nationalId = random.Next(10_000_000, 99_999_999).ToString("D8") + random.Next(100, 999).ToString(); }
    while (!usedNationalIds.Add(nationalId));

    string clientId;
    do { clientId = $"CIF{random.Next(0, 999_999_999):D9}"; }
    while (!usedClientIds.Add(clientId));

    var emailDomain = random.Next(3) switch { 0 => "gmail.com", 1 => "wp.pl", _ => "onet.pl" };
    var email = $"{firstName.ToLower()}.{lastName.ToLower()}{random.Next(1, 999)}@{emailDomain}";

    clients.Add((firstName, lastName, nationalId, clientId, email));
}

// --- Assign clients to applications ---
// 5 frequent clients: 3-5 apps each
// ~20% of remaining: exactly 2 apps each
// rest: exactly 1 app each
var applicationClientIndices = new List<int>(totalCount);

// Frequent clients (first 5)
var frequentCount = Math.Min(5, uniqueClientCount);
for (var f = 0; f < frequentCount; f++)
{
    var appCount = random.Next(3, 6); // 3-5
    for (var a = 0; a < appCount; a++)
        applicationClientIndices.Add(f);
}

// Repeat clients (~20% of remaining pool, exactly 2 apps each)
var remainingPoolStart = frequentCount;
var remainingPoolSize = uniqueClientCount - frequentCount;
var repeatCount = (int)(remainingPoolSize * 0.20);
for (var r = 0; r < repeatCount; r++)
{
    var idx = remainingPoolStart + r;
    applicationClientIndices.Add(idx);
    applicationClientIndices.Add(idx);
}

// Fill remaining slots with single-use clients
var singleStart = remainingPoolStart + repeatCount;
var singleIdx = singleStart;
while (applicationClientIndices.Count < totalCount)
{
    if (singleIdx < uniqueClientCount)
    {
        applicationClientIndices.Add(singleIdx);
        singleIdx++;
    }
    else
    {
        // If we run out of unique clients, reuse from pool
        applicationClientIndices.Add(random.Next(uniqueClientCount));
    }
}

// Shuffle the assignment list
for (var s = applicationClientIndices.Count - 1; s > 0; s--)
{
    var j = random.Next(s + 1);
    (applicationClientIndices[s], applicationClientIndices[j]) = (applicationClientIndices[j], applicationClientIndices[s]);
}

// --- Pre-generate employee pool (500 employees) ---
var employees = new string[500];
var usedEmployeeIds = new HashSet<string>();
for (var e = 0; e < 500; e++)
{
    string eid;
    do { eid = $"x{random.Next(0, 999_999):D6}"; }
    while (!usedEmployeeIds.Add(eid));
    employees[e] = eid;
}

// --- Streaming JSON output ---
var now = DateTimeOffset.UtcNow;
var sevenDaysAgo = now.AddDays(-7);
var threeYearsInDays = 365 * 3;

var fullPath = Path.GetFullPath(outputPath);
Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

using var fileStream = File.Create(fullPath);
using var writer = new Utf8JsonWriter(fileStream, new JsonWriterOptions { Indented = writeIndented });

writer.WriteStartArray();

var progressInterval = Math.Max(totalCount / 100, 1000);
var sequentialCounters = new Dictionary<string, int>(); // key: "yyyyMMdd/code" → sequential counter

for (var i = 0; i < totalCount; i++)
{
    // --- Product ---
    var productRoll = random.NextDouble();
    var cumulative = 0.0;
    var productIdx = 0;
    for (var p = 0; p < products.Length; p++)
    {
        cumulative += products[p].Weight;
        if (productRoll < cumulative) { productIdx = p; break; }
    }
    var product = products[productIdx];

    // --- Dates ---
    var createdAt = now.AddDays(-random.Next(1, threeYearsInDays));
    var updatedAt = createdAt.AddDays(random.Next(1, 31));
    if (updatedAt > now) updatedAt = now;

    // --- ID ---
    var dateKey = createdAt.ToString("yyyyMMdd");
    var seqKey = $"{dateKey}/{product.Code}";
    sequentialCounters.TryGetValue(seqKey, out var seq);
    seq++;
    sequentialCounters[seqKey] = seq;
    var id = $"{dateKey}/{product.Code}/{seq:D5}";

    // --- Channel ---
    var channelRoll = random.NextDouble();
    cumulative = 0.0;
    var channelIdx = 0;
    for (var ch = 0; ch < channels.Length; ch++)
    {
        cumulative += channels[ch].Weight;
        if (channelRoll < cumulative) { channelIdx = ch; break; }
    }
    var channel = channels[channelIdx].Name;

    // --- Transaction ---
    var txRoll = random.NextDouble();
    cumulative = 0.0;
    var txIdx = 0;
    for (var t = 0; t < transactions.Length; t++)
    {
        cumulative += transactions[t].Weight;
        if (txRoll < cumulative) { txIdx = t; break; }
    }
    var transaction = transactions[txIdx].Name;

    // --- Status ---
    string status;
    if (createdAt > sevenDaysAgo)
        status = "Active";
    else
        status = random.Next(2) == 0 ? "Rejected" : "Completed";

    // --- Branch ---
    string? branch = channel == "Branch" ? $"{random.Next(1, 101):D3}" : null;

    // --- Client ---
    var clientIdx = applicationClientIndices[i];
    var client = clients[clientIdx];

    // --- User ---
    var user = channel == "Online" ? client.ClientId : employees[random.Next(employees.Length)];

    // --- Spouse (25%) ---
    var hasSpouse = random.NextDouble() < 0.25;

    // --- CoApplicants (20%, always exactly 1) ---
    var hasCoApplicant = random.NextDouble() < 0.20;

    // --- Write JSON ---
    writer.WriteStartObject();

    writer.WriteString("id", id);
    writer.WriteString("product", product.Name);
    writer.WriteString("transaction", transaction);
    writer.WriteString("channel", channel);

    if (branch is not null)
        writer.WriteString("branch", branch);
    else
        writer.WriteNull("branch");

    writer.WriteString("status", status);
    writer.WriteString("user", user);
    writer.WriteString("createdAt", createdAt);
    writer.WriteString("updatedAt", updatedAt);

    // mainClient
    writer.WritePropertyName("mainClient");
    WriteClient(writer, client);

    // spouse
    if (hasSpouse)
    {
        var spouseIdx = random.Next(uniqueClientCount);
        // Avoid same person as main client
        if (spouseIdx == clientIdx) spouseIdx = (spouseIdx + 1) % uniqueClientCount;
        writer.WritePropertyName("spouse");
        WriteClient(writer, clients[spouseIdx]);
    }
    else
    {
        writer.WriteNull("spouse");
    }

    // coApplicants
    writer.WritePropertyName("coApplicants");
    writer.WriteStartArray();
    if (hasCoApplicant)
    {
        var coIdx = random.Next(uniqueClientCount);
        if (coIdx == clientIdx) coIdx = (coIdx + 1) % uniqueClientCount;
        WriteClient(writer, clients[coIdx]);
    }
    writer.WriteEndArray();

    writer.WriteEndObject();

    // Progress reporting
    if ((i + 1) % progressInterval == 0)
    {
        writer.Flush();
        Console.Write($"\rGenerating: {i + 1:N0} / {totalCount:N0} applications...");
    }
}

writer.WriteEndArray();
writer.Flush();

Console.WriteLine($"\rGenerated {totalCount:N0} applications to: {fullPath}");

static void WriteClient(Utf8JsonWriter writer, (string FirstName, string LastName, string NationalId, string ClientId, string Email) client)
{
    writer.WriteStartObject();
    writer.WriteString("email", client.Email);
    writer.WriteString("firstName", client.FirstName);
    writer.WriteString("lastName", client.LastName);
    writer.WriteString("nationalId", client.NationalId);
    writer.WriteString("clientId", client.ClientId);
    writer.WriteEndObject();
}
