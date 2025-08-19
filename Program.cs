using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace WplaceScanner;

public record PixelInfo
{
    [JsonPropertyName("paintedBy")] public required PaintedBy PaintedBy { get; init; }

    [JsonPropertyName("region")] public required Region Region { get; init; }
}

public record PaintedBy
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("allianceId")] public int AllianceId { get; init; }
    [JsonPropertyName("allianceName")] public string AllianceName { get; init; } = string.Empty;
    [JsonPropertyName("equippedFlag")] public int EquippedFlag { get; init; }
}

public record Region
{
    [JsonPropertyName("id")] public int Id { get; init; }

    [JsonPropertyName("cityId")] public int CityId { get; init; }

    [JsonPropertyName("name")] public required string Name { get; init; }

    [JsonPropertyName("number")] public int Number { get; init; }

    [JsonPropertyName("countryId")] public int CountryId { get; init; }
}

public static class Program
{
    public static async Task Main(string[] args)
    {
        var dbPath = "path-to-the-db";
        var connectionString = $"Data Source={dbPath}";
        using var dbConnection = new SqliteConnection(connectionString);
        dbConnection.Open();

        SelectAnOption:
        Console.WriteLine("Select an option:");
        Console.WriteLine("1. Scan Wplace");
        Console.WriteLine("2. Find Pixel Info");

        var option = Console.ReadLine();

        switch (option)
        {
            case "1":
                await WplaceScanner.Init(dbConnection);
                break;
            case "2":
                // FindPixelInfo(dbConnection);
                break;
            default:
                Console.WriteLine("Invalid option.");
                break;
        }

        goto SelectAnOption;
    }
}

public static class WplaceScanner
{
    private static string? FromTlx { get; set; }
    private static string? FromTly { get; set; }
    private static string? ToTlx { get; set; }
    private static string? ToTly { get; set; }

    public static async Task Init(SqliteConnection dbConnection)
    {
        RangeScanning:
        Console.WriteLine("Introduce the zone range for scanning:");
        Console.WriteLine("FROM");
        Console.WriteLine("Top left X (TL X):");
        FromTlx = Console.ReadLine();
        Console.WriteLine("Top left Y (TL Y):");
        FromTly = Console.ReadLine();
        Console.WriteLine();
        Console.WriteLine("TO");
        Console.WriteLine("Top left X (TL X):");
        ToTlx = Console.ReadLine();
        Console.WriteLine("Top left Y (TL Y):");
        ToTly = Console.ReadLine();

        Console.Clear();
        Console.WriteLine("Coordinates:");
        Console.WriteLine($"FROM: ({FromTlx}, {FromTly})");
        Console.WriteLine($"TO: ({ToTlx}, {ToTly})");
        Console.WriteLine("Is this correct? (y/n)");
        var confirmation = Console.ReadLine();

        if (confirmation?.ToLower() == "n")
            goto RangeScanning;

        Console.Clear();
        Console.WriteLine("Select a scanning method:");
        Console.WriteLine("1. Scan on background");
        Console.WriteLine("2. Verbose scan");

        var method = Console.ReadLine();

        switch (method ?? string.Empty)
        {
            case "1":
                // Implement background scanning logic here
                break;
            case "2":
                await VerboseScan(dbConnection);
                break;
            default:
                Console.WriteLine("Invalid scanning method.");
                break;
        }
    }

    private static async Task VerboseScan(SqliteConnection dbConnection)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Apidog/1.0.0 (https://apidog.com)");
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");

        int fromTlx = int.Parse(FromTlx!);
        int fromTly = int.Parse(FromTly!);
        int toTlx = int.Parse(ToTlx!);
        int toTly = int.Parse(ToTly!);

        int startTlx = Math.Min(fromTlx, toTlx);
        int endTlx = Math.Max(fromTlx, toTlx);
        int startTly = Math.Min(fromTly, toTly);
        int endTly = Math.Max(fromTly, toTly);

        for (int tlx = startTlx; tlx <= endTlx; tlx++)
        {
            for (int tly = startTly; tly <= endTly; tly++)
            {
                if (Console.KeyAvailable)
                {
                    Console.WriteLine("Scan stopped by user.");
                    return;
                }

                var url = $"https://backend.wplace.live/s0/pixel/{tlx}/{tly}";
                for (var x = 0; x < 1000; x += 10)
                {
                    for (var y = 0; y < 1000; y += 10)
                    {
                        var pixelUrl = $"{url}?x={x}&y={y}";
                        var response = await httpClient.GetAsync(pixelUrl);

                        if (!response.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"Error: {response.StatusCode}");
                            continue;
                        }

                        string json = await response.Content.ReadAsStringAsync();

                        using var doc = JsonDocument.Parse(json);
                        string prettyJson =
                            JsonSerializer.Serialize(doc.RootElement,
                                new JsonSerializerOptions { WriteIndented = true });

                        Console.WriteLine($"From {FromTlx}, {FromTly} to {ToTlx}, {ToTly}:");
                        Console.WriteLine($"TLX: {tlx}, TLY: {tly}, Pxx: {x}, Pyy: {y}");
                        Console.WriteLine(prettyJson);

                        var info = JsonSerializer.Deserialize<PixelInfo>(prettyJson);

                        if (info == null)
                            continue;

                        if (info.PaintedBy.Name != string.Empty)
                        {
                            await using var cmd = dbConnection.CreateCommand();
                            cmd.CommandText = "INSERT OR IGNORE INTO user(name) VALUES (@name)";
                            cmd.Parameters.AddWithValue("@name", info.PaintedBy.Name);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        if (info?.PaintedBy is { AllianceId: not 0, AllianceName: var name } &&
                            !string.IsNullOrEmpty(name))
                        {
                            await using var cmd = dbConnection.CreateCommand();
                            cmd.CommandText = "INSERT OR IGNORE INTO alliance(id, name) VALUES (@id, @name)";
                            cmd.Parameters.AddWithValue("@id", info.PaintedBy.AllianceId);
                            cmd.Parameters.AddWithValue("@name", info.PaintedBy.AllianceName);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        if (info?.PaintedBy?.Name != string.Empty && info?.PaintedBy?.AllianceName != string.Empty)
                        {
                            await using var cmd = dbConnection.CreateCommand();
                            cmd.CommandText =
                                "INSERT OR IGNORE INTO user_alliance(user_name, alliance_id) VALUES (@user_name, @alliance_id)";
                            cmd.Parameters.AddWithValue("@user_name", info.PaintedBy.Name);
                            cmd.Parameters.AddWithValue("@alliance_id", info.PaintedBy.AllianceId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        await using (var cmd = dbConnection.CreateCommand())
                        {
                            cmd.CommandText =
                                "INSERT OR IGNORE INTO region(id, name, number, country_id, tl_x, tl_y) VALUES (@id, @name, @number, @country_id, @tl_x, @tl_y)";
                            cmd.Parameters.AddWithValue("@id", info.Region.Id);
                            cmd.Parameters.AddWithValue("@name", info.Region.Name);
                            cmd.Parameters.AddWithValue("@number", info.Region.Number);
                            cmd.Parameters.AddWithValue("@country_id", info.Region.CountryId);
                            cmd.Parameters.AddWithValue("@tl_x", FromTlx);
                            cmd.Parameters.AddWithValue("@tl_y", FromTly);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        if (info.PaintedBy.Name != string.Empty)
                        {
                            await using (var cmd = dbConnection.CreateCommand())
                            {
                                cmd.CommandText =
                                    "INSERT OR IGNORE INTO pixel(px_x, px_y, region_id, user_name) VALUES (@px_x, @px_y, @region_id, @user_name)";
                                cmd.Parameters.AddWithValue("@px_x", x);
                                cmd.Parameters.AddWithValue("@px_y", y);
                                cmd.Parameters.AddWithValue("@region_id", info.Region.Id);
                                cmd.Parameters.AddWithValue("@user_name", info.PaintedBy.Name);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        Thread.Sleep(500); // Sleep to avoid overwhelming the server
                    }
                }
            }
        }
    }
}