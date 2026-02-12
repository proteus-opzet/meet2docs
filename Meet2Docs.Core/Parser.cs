using System.Text.RegularExpressions;

namespace Meet2Docs.Core;

public class Parser
{
    private static TimeSpan? _fromHour;
    private static TimeSpan? _toHour;

    public static readonly TimeZoneInfo Timezone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");

    /// <summary>
    /// Main program logic
    /// </summary>
    public static async Task<int> Run(string[] urls, string[] selectOnly, DateTimeOffset? beginTime = null, DateTimeOffset? endTime = null, int fromHour = 6, int toHour = 22)
    {
        try
        {
            _fromHour = new TimeSpan(fromHour, 0, 0);
            _toHour = new TimeSpan(toHour, 0, 0);

            // Split selectOnly into partitions divided by SEPARATOR
            var partitions = SplitBySeparator(selectOnly);

            //1. Fetch HTML from all URLs (once)
            var inputHtmls = new string[urls.Length];
            var requestTimestamp = DateTime.Now;
            using var client = new HttpClient();
            for (var i = 0; i < urls.Length; i++)
            {
                inputHtmls[i] = await client.GetStringAsync(urls[i]);
            }

            // Compute event names and base filename (once)
            var eventNames = new string[urls.Length];
            for (var i = 0; i < urls.Length; i++)
            {
                eventNames[i] = string.Join("_", FindEventName(inputHtmls[i]).Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
            }
            var filenameBase = $"{string.Join("_", eventNames)}_{requestTimestamp:yyyyMMdd_HHmmss}";

            // Run processing for each partition
            for (var p = 0; p < partitions.Count; p++)
            {
                var partSelectOnly = partitions[p];
                var partFilenameBase = partitions.Count > 1 ? $"{filenameBase}_part{p + 1}" : filenameBase;

                if (partitions.Count > 1)
                    Console.WriteLine($"\n--- Partition {p + 1}: {(partSelectOnly.Length > 0 ? string.Join(", ", partSelectOnly) : "(all)")} ---");

                //2. Retrieve timeslots and add availability
                var timeslots = RetrieveTimeslots(inputHtmls[0]);
                for (var i = 0; i < urls.Length; i++)
                {
                    timeslots = AddAvailabilityToTimeslots(inputHtmls[i], timeslots, partSelectOnly);
                }

                // 3. Summarize user availability
                timeslots = MarkBlockMembership(timeslots, beginTime ?? DateTimeOffset.MinValue, endTime ?? DateTimeOffset.MaxValue);

                // 4. Write output CSV, XLSX
                var lines = CreateCsv(timeslots);
                await File.WriteAllLinesAsync(partFilenameBase + ".csv", lines);
                Console.WriteLine($"Basic CSV exported: {partFilenameBase}.csv");
                CsvToXlsxConverter.Run(partFilenameBase + ".csv", partFilenameBase + ".xlsx");
                Console.WriteLine($"Formatted XLSX exported: {partFilenameBase}.xlsx");

                //5. Make it an overview of the week
                CsvTransposeUtil.Run(partFilenameBase + ".csv", "weekview-" + partFilenameBase + ".csv");
                Console.WriteLine($"Week overview CSV exported: weekview-{partFilenameBase}.csv");
            }

            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync(ex.Message);
            return 1;
        }
    }

    /// <summary>
    /// Splits a list of names into partitions separated by the special "SEPARATOR" entry.
    /// </summary>
    private static List<string[]> SplitBySeparator(string[] items)
    {
        var partitions = new List<string[]>();
        var current = new List<string>();

        foreach (var item in items)
        {
            if (string.Equals(item, "SEPARATOR", StringComparison.OrdinalIgnoreCase))
            {
                if (current.Count > 0)
                    partitions.Add(current.ToArray());
                current = [];
            }
            else
            {
                current.Add(item);
            }
        }

        if (current.Count > 0)
            partitions.Add(current.ToArray());

        // No partitions found (empty selectOnly or only SEPARATORs) → single run with no filter
        if (partitions.Count == 0)
            partitions.Add([]);

        return partitions;
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
    private static List<Timeslot> AddAvailabilityToTimeslots(string inputStr, List<Timeslot> timeslots, string[] selectOnly)
    {
        Dictionary<int, string> people = new();
        Dictionary<int, List<int>> availability = new();
        List<int> ignoreIDs = [];
        GeneratedRegexAttribute genRegex = new(@"PeopleNames\[(\d+)] = '([^']+)';PeopleIDs\[\1] = (\d+);");
        foreach (Match match in Regex.Matches(inputStr, genRegex.Pattern))
        {
            var userId = int.Parse(match.Groups[3].Value);
            var name = match.Groups[2].Value.Trim();
            if (selectOnly.Length > 0 && !selectOnly.Contains(name))
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
    private static List<Timeslot> MarkBlockMembership(List<Timeslot> timeslots, DateTimeOffset beginTime, DateTimeOffset endTime)
    {
        var filtered = timeslots.Where(slot =>
        {
            var dt = slot.DateTimeBegin;
            return dt >= beginTime && dt < endTime && dt.TimeOfDay >= _fromHour && dt.TimeOfDay < _toHour;
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
        var allNames = timeslots.SelectMany(ts => ts.AvailableUsers).Distinct().OrderBy(n => TotalIndividualAvailability(timeslots, n)).ToList();
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

    private static int TotalIndividualAvailability(List<Timeslot> timeslots, string s)
    {
        return timeslots.Count(timeslot => timeslot.AvailableUsers.Contains(s));
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
