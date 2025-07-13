using System.CommandLine;

namespace Meet2Docs;

public class Program
{
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
                return Parser.Run(parsedUrls, parsedNamesToSelectOnly).Result;
            }
            return 0;
        });

        var statusCode = await parseResult.InvokeAsync();
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey();
        return statusCode;
    }
}
