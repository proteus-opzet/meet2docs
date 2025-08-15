using System.CommandLine;
using System.Globalization;
using Meet2Docs.Core;

namespace Meet2Docs.Cli;

public class Program
{
    private const int NDaysAfterWhichTheNextMondayIsSelected = 4;

    private static DateTimeOffset StartOfWeek
    {
        get
        {
            var start = DateTimeOffset.Now.AddDays(NDaysAfterWhichTheNextMondayIsSelected);
            var daysUntilMonday = ((int)DayOfWeek.Monday - (int)start.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0) daysUntilMonday = 7;
            return start.Date.AddDays(daysUntilMonday);
        }
    }

    public static async Task<int> Main(string[] args)
    {
        var urlsOption = new Option<string[]>(
            name: "--urls",
            aliases: ["-u"])
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
            name: "--select-only",
            aliases: ["-s"])
        {
            Description = "A comma-separated list of names to select",
            Arity = ArgumentArity.ZeroOrOne,
            CustomParser = result =>
            {
                if (result.Tokens.Count == 0) return [];
                var token = result.Tokens[0].Value;
                return token.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            },
            DefaultValueFactory = _ => []
        };

        // Make this nullable so we can pass null to Parser.Run when date filtering is disabled.
        var beginTimeOption = new Option<DateTimeOffset?>(
            name: "--beginTime",
            aliases: ["-b"]
        )
        {
            Description = "ISO 8601 time: filter dates after the specified value. Default: the next Monday at least 4 few days from now. This means: Fri(today),Sat,Sun,Mon,Tue,Wed,Thu,Fri,Sat,Sun,*Mon*.",
            DefaultValueFactory = _ => StartOfWeek
        };

        var endTimeOption = new Option<DateTimeOffset?>(
            name: "--endTime",
            aliases: ["-e"]
        )
        {
            Description = "ISO 8601 time: filter dates before the specified value. Default: beginTime + 7 days."
        };

        var noDateFilterOption = new Option<bool>(
            name: "--no-date-filter",
            aliases: ["-N"]
        )
        {
            Description = "Disable filtering by date: select all timeslots. BeginHour and ToHour arguments or their defaults are still being used."
        };

        var fromHour = new Option<int>(
            name: "--beginHour",
            aliases: ["-f"]
        )
        {
            Description = "Filter timeslots after the specified hour of the day. Default: 6.",
            DefaultValueFactory = _ => 6
        };
        fromHour.Validators.Add(r =>
        {
            var v = r.GetValueOrDefault<int>();
            if (v is < 0 or > 23)
            {
                r.AddError("--beginHour must be between 0 and 23.");
            }
        });

        var toHour = new Option<int>(
            name: "--toHour",
            aliases: ["-t"]
        )
        {
            Description = "Filter timeslots before the specified hour of the day. Default: 22.",
            DefaultValueFactory = _ => 22
        };
        // Fix: apply validator to toHour (was mistakenly added to fromHour before)
        toHour.Validators.Add(r =>
        {
            var v = r.GetValueOrDefault<int>();
            if (v < 0 || v > 23)
            {
                r.AddError("--toHour must be between 0 and 23.");
            }
        });

        var root = new RootCommand("Extracts information about a When2Meet event")
        {
            urlsOption, selectOnlyOption, beginTimeOption, endTimeOption, noDateFilterOption, fromHour, toHour
        };

        var parseResult = root.Parse(args);

        root.SetAction(res =>
        {
            // values come out already parsed + defaults applied
            var urls = res.GetRequiredValue(urlsOption);
            var selectOnly = res.GetValue(selectOnlyOption);

            // pull values as nullable so we can pass null through
            DateTimeOffset? begin = res.GetValue(beginTimeOption);
            DateTimeOffset? end = res.GetValue(endTimeOption) ?? begin?.AddDays(7); // dependent default here

            var disableDateFilter = res.GetValue(noDateFilterOption);
            if (disableDateFilter)
            {
                begin = null;
                end = null;
            }

            var beginHour = res.GetValue(fromHour);
            var endHour = res.GetValue(toHour);

            return Parser.Run(urls, selectOnly, begin, end, beginHour, endHour).Result;
        });

        var status = await parseResult.InvokeAsync();
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey();
        return status;
    }
}
