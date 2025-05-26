using System.Text;
using System.Text.RegularExpressions;

namespace Meet2Docs;

internal class Slot
{
    public int Idx { get; }
    public long Timestamp { get; }
    public List<string> AvailableUsers { get; set; }
    public DateTimeOffset DateTimeOffset { get; }
    public int DayOfWeek { get; }

    public Slot(int idx, long timestamp)
    {
        Idx = idx;
        Timestamp = timestamp;
        AvailableUsers = [];

        var amsterdamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");
        DateTimeOffset = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(timestamp), amsterdamTimeZone);

        DayOfWeek = (int)DateTimeOffset.DayOfWeek;
    }

    public override string ToString()
    {
        return $"Slot(idx={Idx}, time={DateTimeOffset}, available_users={string.Join(", ", AvailableUsers)})";
    }
}

internal class TimeWindow
{
    public DateTimeOffset Start { get; }
    public DateTimeOffset End { get; }

    public TimeWindow(DateTimeOffset start, DateTimeOffset end)
    {
        Start = start;
        End = end;
    }

    public override string ToString() => $"{Start:yyyy-MM-dd HH:mm} → {End:HH:mm}";
}

internal class Program
{
    private static async Task<string> ReadMainResponse(string url)
    {
        using var client = new HttpClient();
        return await client.GetStringAsync(url);
    }

    private static List<Slot> OneRetrieveTimeslots(string inputStr)
    {
        var slots = new List<Slot>();
        var idx = 0;
        var identifier = $"TimeOfSlot[{idx}]=";
        var posBegin = 0;
        var posEnd = inputStr.IndexOf("PeopleNames[0] =", StringComparison.Ordinal);

        while ((posBegin = inputStr.IndexOf(identifier, posBegin, StringComparison.Ordinal)) != -1 && posBegin < posEnd)
        {
            posBegin += identifier.Length;
            var posSemicolon = inputStr.IndexOf(';', posBegin);
            var substr = inputStr.Substring(posBegin, posSemicolon - posBegin);

            if (long.TryParse(substr, out var time))
            {
                slots.Add(new Slot(idx, time));
            }

            idx++;
            identifier = $"TimeOfSlot[{idx}]=";
        }

        return slots;
    }

    private static List<Slot> TwoParseAvailability(string inputStr, List<Slot> timeslots)
    {
        Dictionary<int, string> people = new();
        Dictionary<int, List<int>> availability = new();

        GeneratedRegexAttribute genRegex = new(@"PeopleNames\[(\d+)] = '([^']+)';PeopleIDs\[\1] = (\d+);");
        foreach (Match match in Regex.Matches(inputStr, genRegex.Pattern))
        {
            var userId = int.Parse(match.Groups[3].Value);
            var name = match.Groups[2].Value.Trim();
            people[userId] = name;
        }

        genRegex = new GeneratedRegexAttribute(@"AvailableAtSlot\[(\d+)]\.push\((\d+)\);");
        foreach (Match match in Regex.Matches(inputStr, genRegex.Pattern))
        {
            var slotIdx = int.Parse(match.Groups[1].Value);
            var userId = int.Parse(match.Groups[2].Value);
            if (!availability.ContainsKey(slotIdx))
                availability[slotIdx] = [];
            availability[slotIdx].Add(userId);
        }

        foreach (var slot in timeslots)
        {
            if (availability.TryGetValue(slot.Idx, out var userIds))
            {
                slot.AvailableUsers = userIds.Where(people.ContainsKey).Select(id => people[id]).ToList();
            }
        }

        return timeslots;
    }

    private static void WriteCsv(List<Slot> slots, string outputFile)
    {
        // 1) Determine unique users (sorted alphabetically)
        var uniqueUsers = slots.SelectMany(s => s.AvailableUsers).Distinct().OrderBy(u => u).ToList();

        // 2) Build a parallel list of ISO8601 timestamps (with offset)
        var timeSlots = slots.Select(s => s.DateTimeOffset.ToString("yyyy-MM-ddTHH:mm:sszzz")).ToList();

        // 3) Precompute availability matrix: user → list of "true"/"false"
        var userAvailability = uniqueUsers.ToDictionary(user => user, user => slots.Select(s => s.AvailableUsers.Contains(user) ? "true" : "false").ToList());

        // 4) CSV‐field escaper (quotes fields containing comma, quote, or newline)
        static string EscapeCsv(string field)
        {
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }

            return field;
        }

        // 5) Write out
        using var writer = new StreamWriter(outputFile, false, Encoding.UTF8);
        // Header
        var header = Enumerable.Concat(["Slot"], uniqueUsers).Select(EscapeCsv);
        writer.WriteLine(string.Join(",", header));

        // Rows
        for (int row = 0; row < timeSlots.Count; row++)
        {
            // start with the slot timestamp
            var fields = new List<string> { timeSlots[row] };
            // append each user's availability at this slot
            foreach (var user in uniqueUsers)
            {
                fields.Add(userAvailability[user][row]);
            }

            writer.WriteLine(string.Join(",", fields.Select(EscapeCsv)));
        }
    }


    /// <summary>
    /// Finds, for each calendar day, all time‐ranges where at least `minUsers` are
    /// continuously available for at least `minConsecutiveSlots` slots (15 min each).
    /// </summary>
    private static Dictionary<DateTime, List<TimeWindow>> FindAvailablePeriods(List<Slot> slots, int minConsecutiveSlots = 6, int minUsers = 3)
    {
        var result = new Dictionary<DateTime, List<TimeWindow>>();

        // 1) All distinct users
        var users = slots.SelectMany(s => s.AvailableUsers).Distinct().ToList();

        // 2) All dates present
        var dates = slots.Select(s => s.DateTimeOffset.Date).Distinct().OrderBy(d => d);

        foreach (var date in dates)
        {
            // 3) Slots for this date, in timestamp order
            var daySlots = slots.Where(s => s.DateTimeOffset.Date == date).OrderBy(s => s.Timestamp).ToList();
            int m = daySlots.Count;
            if (m == 0) continue;

            // 4) Build per‐user availability arrays
            var availability = users.ToDictionary(user => user, user => daySlots.Select(s => s.AvailableUsers.Contains(user)).ToArray());

            // 5) For each user, mark only those slots that lie inside a run ≥ minConsecutiveSlots
            var longAvailability = availability.ToDictionary(kvp => kvp.Key, kvp => ComputeLongAvailability(kvp.Value, minConsecutiveSlots));

            // 6) Count how many users are “long‐available” at each slot
            var counts = new int[m];
            for (int i = 0; i < m; i++)
                counts[i] = longAvailability.Values.Count(arr => arr[i]);

            // 7) Sweep for contiguous runs where counts[i] >= minUsers
            var windows = new List<TimeWindow>();
            for (int i = 0; i < m;)
            {
                if (counts[i] >= minUsers)
                {
                    int startIdx = i;
                    while (i < m && counts[i] >= minUsers) i++;
                    int endIdx = i - 1;

                    var start = daySlots[startIdx].DateTimeOffset;
                    // end = end of last slot → add 15 minutes
                    var end = daySlots[endIdx].DateTimeOffset.AddMinutes(15);

                    windows.Add(new TimeWindow(start, end));
                }
                else
                {
                    i++;
                }
            }

            if (windows.Count > 0)
                result[date] = windows;
        }

        return result;
    }

    /// <summary>
    /// Given a raw availability array (true/false per slot), returns a new array
    /// where only positions inside a run of length >= minConsecutiveSlots are true.
    /// </summary>
    private static bool[] ComputeLongAvailability(bool[] raw, int minConsecutiveSlots)
    {
        int n = raw.Length;
        var outArr = new bool[n];
        int runLen = 0;

        for (int i = 0; i < n; i++)
        {
            if (raw[i])
                runLen++;
            else
                runLen = 0;

            if (runLen >= minConsecutiveSlots)
            {
                // mark the last runLen slots as true
                for (int j = i; j > i - runLen; j--)
                    outArr[j] = true;
            }
        }

        return outArr;
    }


    private static async Task Main()
    {
        var inputData = await File.ReadAllTextAsync("hippo");
        var timeslots = OneRetrieveTimeslots(inputData);
        var mappedTimeslots = TwoParseAvailability(inputData, timeslots);
        mappedTimeslots.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

        var firstDay = mappedTimeslots.First().DateTimeOffset.Date;
        var firstDaySlots = mappedTimeslots.Where(s => s.DateTimeOffset.Date == firstDay).ToList();
        var firstHour = firstDaySlots.First().DateTimeOffset.Hour;
        var lastHour = firstDaySlots.Last().DateTimeOffset.Hour;

        const string outputFile = "hippo1.csv";
        WriteCsv(mappedTimeslots, outputFile);
        Console.WriteLine($"CSV export completed: {outputFile}");

        var windows = FindAvailablePeriods(mappedTimeslots);
        foreach (var kvp in windows)
        {
            Console.WriteLine(kvp.Key.ToString("yyyy-MM-dd"));
            foreach (var w in kvp.Value)
                Console.WriteLine($"  {w}");
        }
    }
}
