using System.Text.RegularExpressions;
using File = System.IO.File;

namespace Meet2Docs;

public class Program
{
    // --- BEGIN Section: change these parameters ---
    private static string[] When2MeetUrls = ["https://www.when2meet.com/A", "https://www.when2meet.com/B"];

    private static readonly DateTimeOffset StartOfWeek = DateTimeOffset.Parse("2025-07-20T00:00:00+02");
    private static readonly DateTimeOffset EndOfWeek = DateTimeOffset.Parse("2025-07-27T00:00:00+02");


    // Names must match exactly! Case-sensitive
    private static readonly List<string> SelectOnlyThese = []; // Specify names to filter by, or leave empty to include all
    // --- END Section ---

    private static readonly TimeSpan BeginningOfDay = new(6, 0, 0);
    private static readonly TimeSpan EndOfDay = new(22, 0, 0);

    public static readonly TimeZoneInfo Timezone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");


    /// <summary>
    /// Main program logic
    /// </summary>
    public static async Task Main()
    {
        //1. Using data from the first URL, retrieve timeslot times
        var firstUrl = When2MeetUrls.FirstOrDefault();
        var inputHtmls = new string[When2MeetUrls.Length];

        var requestTimestamp = DateTime.Now;
        using var client = new HttpClient();
        inputHtmls[0] = await client.GetStringAsync(firstUrl);
        var timeslots = RetrieveTimeslots(inputHtmls[0]);
        timeslots = AddAvailabilityToTimeslots(inputHtmls[0], timeslots);


        for (var i = 1; i < When2MeetUrls.Length; i++)
        {
            var url = When2MeetUrls[i];
            inputHtmls[i] = await client.GetStringAsync(url);
            timeslots = AddAvailabilityToTimeslots(inputHtmls[i], timeslots);
        }

        timeslots = MarkBlockMembership(timeslots);

        var eventNames = new string[When2MeetUrls.Length];
        for (var i = 0; i < When2MeetUrls.Length; i++)
        {
            eventNames[i] = string.Join("_", FindEventName(inputHtmls[i]).Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
        }

        // 3. Write output CSV
        var filenameBase = $"{string.Join("_", eventNames)}_{requestTimestamp:yyyyMMdd_HHmmss}";
        var lines = CreateCsv(timeslots);

        await File.WriteAllLinesAsync(filenameBase + ".csv", lines);
        Console.WriteLine($"CSV export completed: {filenameBase}.csv");
        CsvToXlsxConverter.Run(filenameBase + ".csv", filenameBase + ".xlsx");
    }

    /// <summary>
    /// Finds the When2Meet event name.
    /// </summary>
    private static string FindEventName(string inputHtml)
    {
        const string identifier = "<div id=\"NewEventNameDiv\"";
        var pos = inputHtml.IndexOf(identifier, StringComparison.Ordinal) + 80;
        var endPos = inputHtml.IndexOf("<br>", pos, StringComparison.Ordinal);
        return inputHtml.Substring(pos, endPos - pos);
    }

    /// <summary>
    /// Creates timeslots that are covered in the given When2Meet.
    /// </summary>
    private static List<Timeslot> RetrieveTimeslots(string inputStr)
    {
        var timeslots = new List<Timeslot>();
        var index = 0;
        var identifier = $"TimeOfSlot[{index}]=";
        var posBegin = 0;
        var posEnd = inputStr.IndexOf("PeopleNames[0] =", StringComparison.Ordinal);

        while ((posBegin = inputStr.IndexOf(identifier, posBegin, StringComparison.Ordinal)) != -1 && posBegin < posEnd)
        {
            posBegin += identifier.Length;
            var posSemicolon = inputStr.IndexOf(';', posBegin);
            var substr = inputStr.Substring(posBegin, posSemicolon - posBegin);

            if (long.TryParse(substr, out var time))
            {
                timeslots.Add(new Timeslot(index, time));
            }

            index++;
            identifier = $"TimeOfSlot[{index}]=";
        }

        return timeslots;
    }

    /// <summary>
    /// Adds the list of available people for each timeslot.
    /// </summary>
    private static List<Timeslot> AddAvailabilityToTimeslots(string inputStr, List<Timeslot> timeslots)
    {
        Dictionary<int, string> people = new();
        Dictionary<int, List<int>> availability = new();
        List<int> ignoreIDs = [];
        GeneratedRegexAttribute genRegex = new(@"PeopleNames\[(\d+)] = '([^']+)';PeopleIDs\[\1] = (\d+);");
        foreach (Match match in Regex.Matches(inputStr, genRegex.Pattern))
        {
            var userId = int.Parse(match.Groups[3].Value);
            var name = match.Groups[2].Value.Trim();
            if (SelectOnlyThese.Count > 0 && !SelectOnlyThese.Contains(name))
            {
                ignoreIDs.Add(userId); // Collect IDs to ignore
                continue; // Skip this user if not in the selection list
            }

            people[userId] = name;
        }

        genRegex = new GeneratedRegexAttribute(@"AvailableAtSlot\[(\d+)]\.push\((\d+)\);");
        foreach (Match match in Regex.Matches(inputStr, genRegex.Pattern))
        {
            var slotIdx = int.Parse(match.Groups[1].Value);
            var userId = int.Parse(match.Groups[2].Value);
            if (!availability.ContainsKey(slotIdx))
                availability[slotIdx] = [];
            if (ignoreIDs.Contains(userId)) continue; // Skip this user if their ID is in the ignore list
            availability[slotIdx].Add(userId);
        }

        foreach (var slot in timeslots)
        {
            if (availability.TryGetValue(slot.Index, out var userIds))
            {
                slot.AvailableUsers ??= [];
                slot.AvailableUsers.AddRange(userIds.Where(people.ContainsKey).Select(id => people[id]).ToList());
            }
        }

        return timeslots;
    }

    /// <summary>
    /// Finds blocks of at least 90 minutes each where the same 3 people are available (or more).
    /// </summary>
    private static List<Timeslot> MarkBlockMembership(List<Timeslot> timeslots)
    {
        var filtered = timeslots.Where(slot =>
        {
            var dt = slot.DateTimeBegin;
            return dt >= StartOfWeek && dt < EndOfWeek && dt.TimeOfDay >= BeginningOfDay && dt.TimeOfDay < EndOfDay;
        }).OrderBy(slot => slot.DateTimeBegin).ToList();

        if (filtered.Count < 1)
        {
            Console.WriteLine("No timeslots match the date/time filter.");
            return [];
        }

        for (var i = 0; i < filtered.Count - 5; i++)
        {
            var slot0 = filtered[i];
            var slot1 = filtered[i + 1];
            var slot2 = filtered[i + 2];
            var slot3 = filtered[i + 3];
            var slot4 = filtered[i + 4];
            var slot5 = filtered[i + 5];


            if (slot5.DateTimeBegin - slot0.DateTimeBegin > TimeSpan.FromMinutes(75))
            {
                //We are overflowing into the next day
                continue;
            }

            var intersection = slot0.AvailableUsers.Intersect(slot1.AvailableUsers).Intersect(slot2.AvailableUsers).Intersect(slot3.AvailableUsers).Intersect(slot4.AvailableUsers).Intersect(slot5.AvailableUsers);

            if (intersection.Count() >= 3)
            {
                slot0.IsPartOfBlock = true;
                slot1.IsPartOfBlock = true;
                slot2.IsPartOfBlock = true;
                slot3.IsPartOfBlock = true;
                slot4.IsPartOfBlock = true;
                slot5.IsPartOfBlock = true;
            }
        }

        return filtered;
    }

    private static List<string> CreateCsv(List<Timeslot> timeslots)
    {
        var allNames = timeslots.SelectMany(ts => ts.AvailableUsers).Distinct().OrderBy(n => n).ToList();
        var header = new List<string>();
        header.AddRange(["DayOfTheWeek", "DateString", "BeginTimeString"]);
        header.AddRange(allNames);
        header.AddRange(["CountAvailable", "AtLeast3Users", "IsPartOfBlock"]);

        var lines = new List<string> { string.Join(",", header) };

        // Write rows
        foreach (var ts in timeslots)
        {
            var fields = new List<string>
            {
                ts.DayOfTheWeek.ToString(),
                EscapeCsv(ts.DateString),
                EscapeCsv(ts.BeginTimeString)
            };

            foreach (var name in allNames)
            {
                fields.Add(ts.AvailableUsers.Contains(name) ? "1" : "0");
            }

            fields.Add(ts.CountAvailable.ToString());
            fields.Add(ts.AtLeast3Users ? "1" : "0");
            fields.Add(ts.IsPartOfBlock ? "1" : "0");

            lines.Add(string.Join(",", fields));
        }

        return lines;
    }

    private static string EscapeCsv(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }
        return field;
    }

}