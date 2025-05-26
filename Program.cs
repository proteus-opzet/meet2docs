using Meet2Docs.Util;
using System.Text;
using System.Text.RegularExpressions;

namespace Meet2Docs;

public class Program
{
    private static async Task Main()
    {
        //var inputHtml = await File.ReadAllTextAsync("index.html");
        var inputHtml = await ReadMainResponse("https://www.when2meet.com/?ExamplePath");
        var requestTimestamp = DateTime.Now;

        var eventName = FindEventName(inputHtml);

        var timeslots = RetrieveTimeslots(inputHtml);
        var mappedTimeslots = ParseAvailability(inputHtml, timeslots);
        mappedTimeslots.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

        var outputFile = $"{eventName}_{requestTimestamp.ToLongTimeString()}.csv";
        WriteCsv(mappedTimeslots, outputFile);
        Console.WriteLine($"CSV export completed: {outputFile}");
    }

    private static string FindEventName(string inputHtml)
    {
        const string identifier = "<div id=\"NewEventNameDiv\"";
        var pos = inputHtml.IndexOf(identifier, StringComparison.Ordinal) + 83;
        var endPos = inputHtml.IndexOf("<br>", pos, StringComparison.Ordinal);
        return inputHtml.Substring(pos, endPos - pos);
    }

    private static async Task<string> ReadMainResponse(string url)
    {
        using var client = new HttpClient();
        return await client.GetStringAsync(url);
    }

    private static List<Timeslot> RetrieveTimeslots(string inputStr)
    {
        var slots = new List<Timeslot>();
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
                slots.Add(new Timeslot(idx, time));
            }

            idx++;
            identifier = $"TimeOfSlot[{idx}]=";
        }

        return slots;
    }

    private static List<Timeslot> ParseAvailability(string inputStr, List<Timeslot> timeslots)
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
            if (availability.TryGetValue(slot.Index, out var userIds))
            {
                slot.AvailableUsers = userIds.Where(people.ContainsKey).Select(id => people[id]).ToList();
            }
        }

        return timeslots;
    }

    private static void WriteCsv(List<Timeslot> slots, string outputFile)
    {
        var uniqueUsers = slots.SelectMany(s => s.AvailableUsers).Distinct().OrderBy(u => u).ToList();
        var timeslots = slots.Select(s => s.DateTime.ToString("yyyy-MM-ddTHH:mm:sszzz")).ToList();

        var userAvailability = uniqueUsers.ToDictionary(user =>
                user,
                user => slots.Select(s => s.AvailableUsers.Contains(user) ? "true" : "false").ToList());


        using var writer = new StreamWriter(outputFile, false, Encoding.UTF8);
        var header = Enumerable.Concat(["Timeslot"], uniqueUsers).Select(CsvUtil.EscapeCsv);
        writer.WriteLine(string.Join(",", header));

        for (var row = 0; row < timeslots.Count; row++)
        {
            var fields = new List<string> { timeslots[row] };
            foreach (var user in uniqueUsers)
            {
                fields.Add(userAvailability[user][row]);
            }
            writer.WriteLine(string.Join(",", fields.Select(CsvUtil.EscapeCsv)));
        }
    }

}
