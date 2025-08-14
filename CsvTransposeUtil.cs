using System.Globalization;

namespace Meet2Docs;

internal class CsvTransposeUtil
{
    internal static void Run()
    {
        const string inputPath = "Final_Capybara_Final_Seahorse_20250811_122452.csv";
        const string outputPath = "merged-experimental.csv";

        // Read all lines from CSV
        var lines = File.ReadAllLines(inputPath);
        if (lines.Length < 2)
        {
            Console.WriteLine("CSV file is empty or invalid.");
            return;
        }

        // Parse header
        var headers = lines[0].Split(',').Select(h => h.Trim()).ToList();
        var nonUserCols = new HashSet<string>
        {
            "DayOfTheWeek",
            "DateString",
            "BeginTimeString",
            "CountAvailable",
            "AtLeast3Users",
            "IsPartOfBlock"
        };

        // Identify user columns
        var userCols = headers.Where(h => !nonUserCols.Contains(h) && !string.IsNullOrWhiteSpace(h)).ToList();

        // Parse rows into objects
        var data = new List<AvailabilityRow>();
        for (var i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split(',');
            if (parts.Length < headers.Count) continue;

            var row = new AvailabilityRow
            {
                DayOfWeek = parts[0],
                DateString = parts[1],
                BeginTimeString = parts[2],
                AtLeast3Users = parts[headers.IndexOf("AtLeast3Users")] == "1",
                IsPartOfBlock = parts[headers.IndexOf("IsPartOfBlock")] == "1",
                UserAvailability = new Dictionary<string, int>()
            };

            if (DateTime.TryParseExact(row.DateString + " " + row.BeginTimeString, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTime))
            {
                row.BeginTime = parsedTime;
            }

            foreach (var user in userCols)
            {
                var colIndex = headers.IndexOf(user);
                if (colIndex >= 0 && colIndex < parts.Length)
                {
                    row.UserAvailability[user] = int.TryParse(parts[colIndex], out var value) ? value : 0;
                }
            }

            if (row.AtLeast3Users && row.IsPartOfBlock)
            {
                data.Add(row);
            }
        }

        // Group by date
        var groupedByDate = data.GroupBy(r => r.DateString);

        var timeRanges = new List<string>();
        var slotsUsers = new List<List<string>>();

        foreach (var group in groupedByDate)
        {
            var sortedGroup = group.OrderBy(r => r.BeginTime).ToList();
            var times = sortedGroup.Select(r => r.BeginTime).ToList();

            var combinedRanges = CombineSlots(sortedGroup);
            foreach (var range in combinedRanges)
            {
                string dayName = range.Start.ToString("ddd", CultureInfo.InvariantCulture);
                string rangeStr = $"{dayName} {range.Start:HH:mm}-{range.End.AddMinutes(15):HH:mm}";
                timeRanges.Add(rangeStr);

                var blockRows = sortedGroup.Where(r => r.BeginTime >= range.Start && r.BeginTime <= range.End).ToList();

                // Compute users for this block
                var userCounts = new Dictionary<string, int>();
                foreach (var row in blockRows)
                {
                    foreach (var user in userCols)
                    {
                        if (row.UserAvailability.ContainsKey(user))
                        {
                            userCounts[user] = userCounts.GetValueOrDefault(user, 0) + row.UserAvailability[user];
                        }
                    }
                }

                var allUsers = userCounts
                    .Where(kvp => kvp.Value > 0)
                    .OrderByDescending(kvp => kvp.Value)
                    .Select(kvp => kvp.Key)
                    .ToList();

                slotsUsers.Add(allUsers);
            }

        }

        // Build output
        var outputLines = new List<string> { "Training Time," + string.Join(",", timeRanges) };

        var maxRows = slotsUsers.Max(u => u.Count);
        for (var i = 0; i < maxRows; i++)
        {
            var row = new List<string> { (i + 1).ToString() };
            foreach (var users in slotsUsers)
            {
                row.Add(i < users.Count ? users[i] : "");
            }

            outputLines.Add(string.Join(",", row));
        }

        File.WriteAllLines(outputPath, outputLines);
        Console.WriteLine($"Training schedule written to {outputPath}");
    }

    static List<(DateTime Start, DateTime End, int CountAvailable)> CombineSlots(List<AvailabilityRow> sortedRows)
    {
        var ranges = new List<(DateTime Start, DateTime End, int CountAvailable)>();
        if (sortedRows.Count == 0) return ranges;

        DateTime rangeStart = sortedRows[0].BeginTime;
        DateTime prevTime = rangeStart;
        int currentCount = sortedRows[0].UserAvailability.Values.Sum(); // Will replace later with actual CountAvailable field
        currentCount = GetCountAvailable(sortedRows[0]);

        for (int i = 1; i < sortedRows.Count; i++)
        {
            var row = sortedRows[i];
            int rowCount = GetCountAvailable(row);

            // If consecutive time AND same count, extend range
            if ((row.BeginTime - prevTime).TotalMinutes == 15 && rowCount == currentCount)
            {
                prevTime = row.BeginTime;
            }
            else
            {
                // Close the current range
                ranges.Add((rangeStart, prevTime, currentCount));

                // Start new range
                rangeStart = row.BeginTime;
                prevTime = row.BeginTime;
                currentCount = rowCount;
            }
        }

        // Add final range
        ranges.Add((rangeStart, prevTime, currentCount));
        return ranges;
    }

    static int GetCountAvailable(AvailabilityRow row)
    {
        // Prefer using actual "CountAvailable" if present
        // If not, compute as sum of users marked as available (1)
        return row.UserAvailability.Values.Sum();
    }
}

internal class AvailabilityRow
{
    public string DayOfWeek { get; set; }
    public string DateString { get; set; }
    public string BeginTimeString { get; set; }
    public DateTime BeginTime { get; set; }
    public bool AtLeast3Users { get; set; }
    public bool IsPartOfBlock { get; set; }
    public Dictionary<string, int> UserAvailability { get; set; }
}