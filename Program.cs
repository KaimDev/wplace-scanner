using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using OpenQA.Selenium.Edge;

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
    private static int PxX { get; set; } = 0;
    private static int PxY { get; set; } = 0;

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

        Console.WriteLine("Initial Pixel X (Px X):");
        var pxXInput = Console.ReadLine();
        if (int.TryParse(pxXInput, out var pxX))
            PxX = pxX;
        else
            PxX = 0;

        Console.WriteLine("Initial Pixel Y (Px Y):");
        var pxYInput = Console.ReadLine();
        if (int.TryParse(pxYInput, out var pxY))
            PxY = pxY;
        else
            PxY = 0;

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
        var chromeOptions = new EdgeOptions();
        // chromeOptions.AddArgument("--headless=new"); // headless
        // chromeOptions.AddArgument("--disable-gpu");
        // chromeOptions.AddArgument("--no-sandbox");

        using var driver = new EdgeDriver(chromeOptions);

        int fromTlx = int.Parse(FromTlx!);
        int fromTly = int.Parse(FromTly!);
        int toTlx = int.Parse(ToTlx!);
        int toTly = int.Parse(ToTly!);

        int startTlx = Math.Min(fromTlx, toTlx);
        int endTlx = Math.Max(fromTlx, toTlx);
        int startTly = Math.Min(fromTly, toTly);
        int endTly = Math.Max(fromTly, toTly);

        var random = new Random();
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
                for (; PxX < 1000; PxX += 10)
                {
                    for (; PxY < 1000; PxY += 10)
                    {
                        var pixelUrl = $"{url}?x={PxX}&y={PxY}";

                        // Navegar con Selenium
                        await driver.Navigate().GoToUrlAsync(pixelUrl);

                        Thread.Sleep(random.Next(500, 800));
                        // driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);

                        // Obtener el JSON que devuelve el endpoint
                        var json = driver.PageSource;

                        // ⚠ Selenium devuelve el HTML entero (<html>...</html>).
                        // Como el endpoint devuelve JSON, hay que limpiar el <pre> u otro wrapper.
                        json = ExtractJsonFromHtml(json);

                        // is a valid json
                        if (string.IsNullOrEmpty(json) || !json.StartsWith("{") || !json.EndsWith("}"))
                        {
                            PxY -= 10; // Decrement PxY to retry the same pixel
                            Console.WriteLine($"Invalid JSON for pixel ({PxX}, {PxY}) at ({tlx}, {tly}). Retrying...");
                            Thread.Sleep(10000);
                            continue;
                        }
                        
                        using var doc = JsonDocument.Parse(json);
                        string prettyJson =
                            JsonSerializer.Serialize(doc.RootElement,
                                new JsonSerializerOptions { WriteIndented = true });

                        Console.WriteLine($"From {FromTlx}, {FromTly} to {ToTlx}, {ToTly}:");
                        Console.WriteLine($"TLX: {tlx}, TLY: {tly}, Pxx: {PxX}, Pyy: {PxY}");
                        Console.WriteLine(prettyJson);

                        var info = JsonSerializer.Deserialize<PixelInfo>(prettyJson);

                        if (info == null)
                            continue;

                        if (info.PaintedBy.Name != string.Empty)
                        {
                            await using var cmd = dbConnection.CreateCommand();
                            cmd.CommandText = "INSERT OR IGNORE INTO user(id, name) VALUES (@id, @name)";
                            cmd.Parameters.AddWithValue("@id", info.PaintedBy.Id);
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
                                "INSERT OR IGNORE INTO user_alliance(user_id, alliance_id) VALUES (@user_id, @alliance_id)";
                            cmd.Parameters.AddWithValue("@user_id", info!.PaintedBy!.Id);
                            cmd.Parameters.AddWithValue("@alliance_id", info.PaintedBy.AllianceId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        await using (var checkCmd = dbConnection.CreateCommand())
                        {
                            checkCmd.CommandText = "SELECT COUNT(*) FROM region WHERE tl_x = @tl_x AND tl_y = @tl_y";
                            checkCmd.Parameters.AddWithValue("@tl_x", tlx);
                            checkCmd.Parameters.AddWithValue("@tl_y", tly);

                            var count = (long)await checkCmd.ExecuteScalarAsync();
                            if (count == 0)
                            {
                                await using (var cmd = dbConnection.CreateCommand())
                                {
                                    cmd.CommandText =
                                        "INSERT INTO region(region_id, name, number, country_id, tl_x, tl_y) VALUES (@region_id, @name, @number, @country_id, @tl_x, @tl_y)";
                                    cmd.Parameters.AddWithValue("@region_id", info.Region.Id);
                                    cmd.Parameters.AddWithValue("@name", info.Region.Name);
                                    cmd.Parameters.AddWithValue("@number", info.Region.Number);
                                    cmd.Parameters.AddWithValue("@country_id", info.Region.CountryId);
                                    cmd.Parameters.AddWithValue("@tl_x", tlx);
                                    cmd.Parameters.AddWithValue("@tl_y", tly);
                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        if (info.PaintedBy.Name != string.Empty)
                        {
                            await using var regionIdCm = dbConnection.CreateCommand();
                            regionIdCm.CommandText = "SELECT id FROM region WHERE tl_x = @tl_x AND tl_y = @tl_y";
                            regionIdCm.Parameters.AddWithValue("@tl_x", tlx);
                            regionIdCm.Parameters.AddWithValue("@tl_y", tly);
                            var regionId = (long?)await regionIdCm.ExecuteScalarAsync();

                            if (regionId == null)
                            {
                                Console.WriteLine($"Region not found for TLX: {tlx}, TLY: {tly}");
                                break;
                            }

                            await using (var cmd = dbConnection.CreateCommand())
                            {
                                cmd.CommandText =
                                    "INSERT OR IGNORE INTO pixel(px_x, px_y, region_id, user_id) " +
                                    "VALUES (@px_x, @px_y, @region_id, @user_id);";
                                cmd.Parameters.AddWithValue("@px_x", PxX);
                                cmd.Parameters.AddWithValue("@px_y", PxY);
                                cmd.Parameters.AddWithValue("@region_id", regionId);
                                cmd.Parameters.AddWithValue("@user_id", info.PaintedBy.Id);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }
                    }

                    PxY = 0; // Reset PxY for the next PxX iteration
                }

                PxX = 0; // Reset PxX for the next TLX iteration
            }
        }
    }

    public static string ExtractJsonFromHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        // Buscar lo que esté dentro de <pre>...</pre>
        var match = Regex.Match(html, @"<pre.*?>(.*?)<\/pre>", RegexOptions.Singleline);

        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        return string.Empty;
    }
}