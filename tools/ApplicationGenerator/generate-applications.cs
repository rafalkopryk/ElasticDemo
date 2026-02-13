#!/usr/bin/env dotnet run
#:package Bogus@35.6.5

using System.Text.Json;
using Bogus;

// Configuration - parse command line arguments
var outputPath = Path.Combine("src", "ElasticDemo.Api", "applications.json");
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

Randomizer.Seed = new Random(42);
var faker = new Faker("pl");

// --- Product definitions ---
var productNames = new[] { "CashLoan", "Installment", "CreditCard", "Overdraft" };
var productCodes = new[] { "CL", "IL", "CC", "OV" };
var productWeights = new[] { 0.50f, 0.30f, 0.15f, 0.05f };

var channelNames = new[] { "Online", "Branch", "Marketing" };
var channelWeights = new[] { 0.65f, 0.25f, 0.10f };

var transactionNames = new[] { "NewProduct", "Renewal", "Amendment" };
var transactionWeights = new[] { 0.80f, 0.15f, 0.05f };

var emailDomains = new[] { "gmail.com", "wp.pl", "onet.pl" };

// --- Pre-generate client pool ---
var uniqueClientCount = Math.Max(10, (int)(totalCount * 0.8));
var clients = new List<(string FirstName, string LastName, string NationalId, string ClientId, string Email)>(uniqueClientCount);

var usedNationalIds = new HashSet<string>();
var usedClientIds = new HashSet<string>();

for (var c = 0; c < uniqueClientCount; c++)
{
    var firstName = faker.Name.FirstName();
    var lastName = faker.Name.LastName();

    string nationalId;
    do { nationalId = GeneratePesel(faker); }
    while (!usedNationalIds.Add(nationalId));

    string clientId;
    do { clientId = $"CIF{faker.Random.Number(0, 999_999_999):D9}"; }
    while (!usedClientIds.Add(clientId));

    var emailDomain = faker.Random.ArrayElement(emailDomains);
    var email = $"{firstName.ToLower()}.{lastName.ToLower()}{faker.Random.Number(1, 999)}@{emailDomain}";

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
    var appCount = faker.Random.Number(3, 5);
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
        applicationClientIndices.Add(faker.Random.Number(0, uniqueClientCount - 1));
    }
}

// Shuffle the assignment list
for (var s = applicationClientIndices.Count - 1; s > 0; s--)
{
    var j = faker.Random.Number(0, s);
    (applicationClientIndices[s], applicationClientIndices[j]) = (applicationClientIndices[j], applicationClientIndices[s]);
}

// --- Pre-generate employee pool (500 employees) ---
var employees = new string[500];
var usedEmployeeIds = new HashSet<string>();
for (var e = 0; e < 500; e++)
{
    string eid;
    do { eid = $"X{faker.Random.Number(0, 999_999):D6}"; }
    while (!usedEmployeeIds.Add(eid));
    employees[e] = eid;
}

// --- Streaming JSON output ---
var now = DateTimeOffset.UtcNow;
var sevenDaysAgo = now.AddDays(-7);
var threeYearsAgo = now.AddDays(-365 * 3);

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
    var productIdx = Array.IndexOf(productNames, faker.Random.WeightedRandom(productNames, productWeights));
    var productName = productNames[productIdx];
    var productCode = productCodes[productIdx];

    // --- Dates ---
    var createdAt = faker.Date.BetweenOffset(threeYearsAgo, now.AddDays(-1));
    var updatedAt = createdAt.AddDays(faker.Random.Number(1, 30));
    if (updatedAt > now) updatedAt = now;

    // --- ID ---
    var dateKey = createdAt.ToString("yyyyMMdd");
    var seqKey = $"{dateKey}/{productCode}";
    sequentialCounters.TryGetValue(seqKey, out var seq);
    seq++;
    sequentialCounters[seqKey] = seq;
    var id = $"{dateKey}/{productCode}/{seq:D5}";

    // --- Channel ---
    var channel = faker.Random.WeightedRandom(channelNames, channelWeights);

    // --- Transaction ---
    var transaction = faker.Random.WeightedRandom(transactionNames, transactionWeights);

    // --- Status ---
    string status;
    if (createdAt > sevenDaysAgo)
        status = "Active";
    else
        status = faker.Random.Bool() ? "Rejected" : "Completed";

    // --- Branch ---
    string? branch = channel == "Branch" ? $"{faker.Random.Number(1, 100):D3}" : null;

    // --- Client ---
    var clientIdx = applicationClientIndices[i];
    var client = clients[clientIdx];

    // --- User ---
    var user = channel == "Online" ? client.ClientId : employees[faker.Random.Number(0, employees.Length - 1)];

    // --- Main applicant spouse (25%) ---
    var mainHasSpouse = faker.Random.Bool(0.25f);

    // --- CoApplicants (20%, always exactly 1) ---
    var hasCoApplicant = faker.Random.Bool(0.20f);

    // --- Write JSON ---
    writer.WriteStartObject();

    writer.WriteString("id", id);
    writer.WriteString("product", productName);
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

    // mainApplicant
    writer.WritePropertyName("mainApplicant");
    var mainSpouseIdx = -1;
    if (mainHasSpouse)
    {
        mainSpouseIdx = faker.Random.Number(0, uniqueClientCount - 1);
        if (mainSpouseIdx == clientIdx) mainSpouseIdx = (mainSpouseIdx + 1) % uniqueClientCount;
    }
    WriteApplicant(writer, client, mainHasSpouse ? clients[mainSpouseIdx] : null);

    // coApplicants
    writer.WritePropertyName("coApplicants");
    writer.WriteStartArray();
    if (hasCoApplicant)
    {
        var coIdx = faker.Random.Number(0, uniqueClientCount - 1);
        if (coIdx == clientIdx) coIdx = (coIdx + 1) % uniqueClientCount;
        var coClient = clients[coIdx];

        // Co-applicant spouse (15%)
        var coHasSpouse = faker.Random.Bool(0.15f);
        (string, string, string, string, string)? coSpouse = null;
        if (coHasSpouse)
        {
            var coSpouseIdx = faker.Random.Number(0, uniqueClientCount - 1);
            if (coSpouseIdx == coIdx) coSpouseIdx = (coSpouseIdx + 1) % uniqueClientCount;
            coSpouse = clients[coSpouseIdx];
        }
        WriteApplicant(writer, coClient, coSpouse);
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

static string GeneratePesel(Faker faker)
{
    var birthDate = faker.Date.Between(new DateTime(1950, 1, 1), new DateTime(2005, 12, 31));
    var year = birthDate.Year;
    var month = birthDate.Month;
    var day = birthDate.Day;

    var yy = year % 100;
    var mm = month + (year >= 2000 ? 20 : 0);

    var serial = faker.Random.Number(0, 9999);

    var digits = new int[11];
    digits[0] = yy / 10;
    digits[1] = yy % 10;
    digits[2] = mm / 10;
    digits[3] = mm % 10;
    digits[4] = day / 10;
    digits[5] = day % 10;
    digits[6] = serial / 1000;
    digits[7] = (serial / 100) % 10;
    digits[8] = (serial / 10) % 10;
    digits[9] = serial % 10;

    var weights = new[] { 1, 3, 7, 9, 1, 3, 7, 9, 1, 3 };
    var sum = 0;
    for (var w = 0; w < 10; w++)
        sum += digits[w] * weights[w];
    digits[10] = (10 - (sum % 10)) % 10;

    return string.Concat(digits);
}

static void WriteApplicant(
    Utf8JsonWriter writer,
    (string FirstName, string LastName, string NationalId, string ClientId, string Email) client,
    (string FirstName, string LastName, string NationalId, string ClientId, string Email)? spouse)
{
    writer.WriteStartObject();

    writer.WritePropertyName("client");
    WriteClient(writer, client);

    if (spouse is { } s)
    {
        writer.WritePropertyName("spouse");
        WriteClient(writer, s);
    }
    else
    {
        writer.WriteNull("spouse");
    }

    writer.WriteEndObject();
}

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
