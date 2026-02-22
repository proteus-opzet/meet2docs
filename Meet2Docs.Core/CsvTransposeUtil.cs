using System.Globalization;

namespace Meet2Docs.Core;

internal class CsvTransposeUtil
{
    const int SlotIntervalMinutes = 15;

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

        // Precompute user sets for each slot
        var slotUsers = sortedRows.Select(r => GetAvailableUsers(r, userCols)).ToList();

        // Find peaks and expand from each one
        var peaks = FindPeaks(slotUsers, minPeaks: 3);
        foreach (var peakIdx in peaks)
        {
            var (lo, hi, users) = ExpandFromPeak(peakIdx, sortedRows, slotUsers);
            ranges.Add((sortedRows[lo].BeginTime, sortedRows[hi].BeginTime, users));
        }

        // Deduplicate identical ranges, sort by start time
        var seen = new HashSet<string>();
        var deduplicated = new List<(DateTime Start, DateTime End, HashSet<string> AvailableUsers)>();
        foreach (var range in ranges.OrderBy(r => r.Start).ThenByDescending(r => r.AvailableUsers.Count))
        {
            var key = $"{range.Start:o}|{range.End:o}|{string.Join(",", range.AvailableUsers.OrderBy(u => u))}";
            if (seen.Add(key))
                deduplicated.Add(range);
        }

        return deduplicated;
    }

    /// <summary>
    /// Finds local maxima (slots where user count >= both neighbors).
    /// Supplements with highest-count slots if fewer than minPeaks are found.
    /// </summary>
    private static List<int> FindPeaks(List<HashSet<string>> slotUsers, int minPeaks)
    {
        var peaks = new List<int>();
        for (var i = 0; i < slotUsers.Count; i++)
        {
            var count = slotUsers[i].Count;
            var prevCount = i > 0 ? slotUsers[i - 1].Count : -1;
            var nextCount = i < slotUsers.Count - 1 ? slotUsers[i + 1].Count : -1;
            if (count >= prevCount && count >= nextCount)
                peaks.Add(i);
        }

        // Ensure at least minPeaks starting points
        if (peaks.Count < minPeaks)
        {
            var peakSet = new HashSet<int>(peaks);
            var extras = Enumerable.Range(0, slotUsers.Count)
                .Where(i => !peakSet.Contains(i))
                .OrderByDescending(i => slotUsers[i].Count);
            foreach (var idx in extras)
            {
                peaks.Add(idx);
                if (peaks.Count >= minPeaks) break;
            }
        }

        return peaks;
    }

    /// <summary>
    /// From a peak slot, greedily expands bidirectionally.
    /// At each step, tries both neighbors and picks the direction that preserves more users.
    /// </summary>
    private static (int Lo, int Hi, HashSet<string> Users) ExpandFromPeak(
        int peakIdx, List<AvailabilityRow> sortedRows, List<HashSet<string>> slotUsers)
    {
        var currentUsers = new HashSet<string>(slotUsers[peakIdx]);
        int lo = peakIdx, hi = peakIdx;

        while (true)
        {
            HashSet<string> backIntersection = null;
            HashSet<string> fwdIntersection = null;

            // Try backward
            if (lo > 0 && IsConsecutive(sortedRows[lo - 1], sortedRows[lo]))
            {
                backIntersection = new HashSet<string>(currentUsers);
                backIntersection.IntersectWith(slotUsers[lo - 1]);
                if (backIntersection.Count == 0) backIntersection = null;
            }

            // Try forward
            if (hi < sortedRows.Count - 1 && IsConsecutive(sortedRows[hi], sortedRows[hi + 1]))
            {
                fwdIntersection = new HashSet<string>(currentUsers);
                fwdIntersection.IntersectWith(slotUsers[hi + 1]);
                if (fwdIntersection.Count == 0) fwdIntersection = null;
            }

            if (backIntersection == null && fwdIntersection == null) break;

            // Pick the direction that preserves the most users
            if (backIntersection != null && (fwdIntersection == null || backIntersection.Count >= fwdIntersection.Count))
            {
                lo--;
                currentUsers = backIntersection;
            }
            else
            {
                hi++;
                currentUsers = fwdIntersection!;
            }
        }

        return (lo, hi, currentUsers);
    }

    private static bool IsConsecutive(AvailabilityRow earlier, AvailabilityRow later)
    {
        return Math.Abs((later.BeginTime - earlier.BeginTime).TotalMinutes - SlotIntervalMinutes) < double.Epsilon;
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