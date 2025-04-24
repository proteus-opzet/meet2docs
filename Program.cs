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
        DateTimeOffset = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(timestamp), amsterdamTimeZone); ;

        DayOfWeek = (int)DateTimeOffset.DayOfWeek;
    }

    public override string ToString()
    {
        return $"Slot(idx={Idx}, time={DateTimeOffset}, available_users={string.Join(", ", AvailableUsers)})";
    }
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
        var uniqueUsers = slots
            .SelectMany(s => s.AvailableUsers)
            .Distinct()
            .OrderBy(u => u)
            .ToList();

        // 2) Build a parallel list of ISO8601 timestamps (with offset)
        var timeSlots = slots
            .Select(s => s.DateTimeOffset.ToString("yyyy-MM-ddTHH:mm:sszzz"))
            .ToList();

        // 3) Precompute availability matrix: user → list of "true"/"false"
        var userAvailability = uniqueUsers.ToDictionary(
            user => user,
            user => slots
                .Select(s => s.AvailableUsers.Contains(user) ? "true" : "false")
                .ToList()
        );

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


    private static async Task Main()
    {
        var inputData = await File.ReadAllTextAsync("test_response");
        var timeslots = OneRetrieveTimeslots(inputData);
        var mappedTimeslots = TwoParseAvailability(inputData, timeslots);
        mappedTimeslots.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

        var firstDay = mappedTimeslots.First().DateTimeOffset.Date;
        var firstDaySlots = mappedTimeslots.Where(s => s.DateTimeOffset.Date == firstDay).ToList();
        var firstHour = firstDaySlots.First().DateTimeOffset.Hour;
        var lastHour = firstDaySlots.Last().DateTimeOffset.Hour;

        const string outputFile = "availability_schedule.csv";
        WriteCsv(mappedTimeslots, outputFile);

        Console.WriteLine($"CSV export completed: {outputFile}");
    }
}