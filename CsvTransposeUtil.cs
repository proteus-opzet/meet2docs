using System.Globalization;

namespace Meet2Docs;

internal class CsvTransposeUtil
{
    internal static void Run(string inputPath, string outputPath)
    {
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

            var combinedRanges = CombineSlots(sortedGroup, userCols);
            foreach (var (start, end, availableUsers) in combinedRanges)
            {
                string dayName = start.ToString("ddd", CultureInfo.InvariantCulture);
                string rangeStr = $"{dayName} {start:HH:mm}-{end.AddMinutes(15):HH:mm}";
                timeRanges.Add(rangeStr);

                // Create a list of users sorted (e.g., alphabetically or however you want)
                var sortedUsers = availableUsers.OrderBy(u => u).ToList();
                slotsUsers.Add(sortedUsers);
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

    static List<(DateTime Start, DateTime End, HashSet<string> AvailableUsers)> CombineSlots(List<AvailabilityRow> sortedRows, List<string> userCols)
    {
        var ranges = new List<(DateTime Start, DateTime End, HashSet<string> AvailableUsers)>();
        if (sortedRows.Count == 0) return ranges;

        var rangeStart = sortedRows[0].BeginTime;
        var prevTime = rangeStart;
        var currentUsers = GetAvailableUsers(sortedRows[0], userCols);

        for (var i = 1; i < sortedRows.Count; i++)
        {
            var row = sortedRows[i];
            var rowUsers = GetAvailableUsers(row, userCols);

            // Check: 15-minute gap and same user set
            if (Math.Abs((row.BeginTime - prevTime).TotalMinutes - 15) < double.Epsilon && currentUsers.SetEquals(rowUsers))
            {
                prevTime = row.BeginTime;
            }
            else
            {
                // Close current range
                ranges.Add((rangeStart, prevTime, currentUsers));

                // Start new range
                rangeStart = row.BeginTime;
                prevTime = row.BeginTime;
                currentUsers = rowUsers;
            }
        }

        // Add final range
        ranges.Add((rangeStart, prevTime, currentUsers));
        return ranges;
    }

    private static HashSet<string> GetAvailableUsers(AvailabilityRow row, List<string> userCols)
    {
        return userCols.Where(user => row.UserAvailability.ContainsKey(user) && row.UserAvailability[user] > 0).ToHashSet();
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