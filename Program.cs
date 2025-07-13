using System.CommandLine;
using System.Text.RegularExpressions;
using File = System.IO.File;
using System.CommandLine.Parsing;

namespace Meet2Docs;

public class Program
{
    private const int NDaysAfterWhichTheNextMondayIsSelected = 4;
    private static DateTimeOffset StartOfWeek
    {
        get
        {
            var start = DateTimeOffset.Now.AddDays(NDaysAfterWhichTheNextMondayIsSelected);
            var daysUntilMonday = ((int)DayOfWeek.Monday - (int)start.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0)
                daysUntilMonday = 7; // ensures we get the *next* Monday, not today if it's already Monday
            DateTimeOffset nextMonday = start.Date.AddDays(daysUntilMonday);
            return nextMonday;
        }
    }

    private static readonly DateTimeOffset EndOfWeek = StartOfWeek.AddDays(7);

    private static readonly TimeSpan BeginningOfDay = new(6, 0, 0);
    private static readonly TimeSpan EndOfDay = new(22, 0, 0);

    public static readonly TimeZoneInfo Timezone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");


    public static async Task<int> Main(string[] args)
    {
        var urlsOption = new Option<string[]>(
            name: "--urls",
            "-u"
        )
        {
            Description = "A comma-separated list of URLs",
            Arity = ArgumentArity.ExactlyOne,
            Required = true,
            CustomParser = result =>
            {
                var token = result.Tokens.Count > 0 ? result.Tokens[0].Value : "";
                return token.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
        };

        var selectOnlyOption = new Option<string[]>(
            "--select-only",
            "-s"
        )
        {
            Description = "A comma-separated list of names to select",
            Arity = ArgumentArity.ZeroOrOne,
            CustomParser = result =>
            {
                var token = result.Tokens.Count > 0 ? result.Tokens[0].Value : "";
                return token.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
        };

        var rootCommand = new RootCommand("Extracts information about a When2Meet event");
        rootCommand.Options.Add(urlsOption);
        rootCommand.Options.Add(selectOnlyOption);
        var parseResult = rootCommand.Parse(args);

        rootCommand.SetAction(res =>
        {
            if (res.GetRequiredValue(urlsOption) is { } parsedUrls
                && res.GetValue(selectOnlyOption) is { } parsedNamesToSelectOnly)
            {
                return Run(parsedUrls, parsedNamesToSelectOnly).Result;
            }
            return 0;
        });

        var statusCode = await parseResult.InvokeAsync();
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey();
        return statusCode;
    }


    /// <summary>
    /// Main program logic
    /// </summary>
    internal static async Task<int> Run(string[] urls, string[] selectOnly)
    {
        try
        {
            //1. Using data from the first URL, retrieve timeslot times
            var firstUrl = urls.FirstOrDefault();
            var inputHtmls = new string[urls.Length];

            var requestTimestamp = DateTime.Now;
            using var client = new HttpClient();
            inputHtmls[0] = await client.GetStringAsync(firstUrl);
            var timeslots = RetrieveTimeslots(inputHtmls[0]);

            //2. Add availability from all URLs
            for (var i = 0; i < urls.Length; i++)
            {
                var url = urls[i];
                inputHtmls[i] = await client.GetStringAsync(url);
                timeslots = AddAvailabilityToTimeslots(inputHtmls[i], timeslots, selectOnly);
            }

            // 3. Summarize user availability
            timeslots = MarkBlockMembership(timeslots);

            // 4. Write output CSV, XLSX
            var eventNames = new string[urls.Length];
            for (var i = 0; i < urls.Length; i++)
            {
                eventNames[i] = string.Join("_", FindEventName(inputHtmls[i]).Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
            }

            var filenameBase = $"{string.Join("_", eventNames)}_{requestTimestamp:yyyyMMdd_HHmmss}";
            var lines = CreateCsv(timeslots);

            await File.WriteAllLinesAsync(filenameBase + ".csv", lines);
            Console.WriteLine($"CSV export completed: {filenameBase}.csv");
            CsvToXlsxConverter.Run(filenameBase + ".csv", filenameBase + ".xlsx");
            Console.WriteLine($"Formatted XLSX exported: {filenameBase}.xlsx");

            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync(ex.Message);
            return 1;
        }
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