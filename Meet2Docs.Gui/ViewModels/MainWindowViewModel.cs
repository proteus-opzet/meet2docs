using System;
using System.Linq;
using System.Windows.Input;
using Meet2Docs.Core;

namespace Meet2Docs.Gui.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public string Title => "Meet2Docs — GUI";

    private string _urlsText = "";
    public string UrlsText { get => _urlsText; set => SetField(ref _urlsText, value); }

    private string _selectOnlyText = "";
    public string SelectOnlyText { get => _selectOnlyText; set => SetField(ref _selectOnlyText, value); }

    private bool _noDateFilter;
    public bool NoDateFilter { get => _noDateFilter; set => SetField(ref _noDateFilter, value); }

    private DateTimeOffset? _beginDate;
    public DateTimeOffset? BeginDate { get => _beginDate; set { SetField(ref _beginDate, value); if (EndDate is null && value is { } v) EndDate = v.AddDays(7); } }

    private DateTimeOffset? _endDate;
    public DateTimeOffset? EndDate { get => _endDate; set => SetField(ref _endDate, value); }

    private string _beginHourText = "6";
    public string BeginHourText { get => _beginHourText; set => SetField(ref _beginHourText, value); }

    private string _endHourText = "22";
    public string EndHourText { get => _endHourText; set => SetField(ref _endHourText, value); }

    private string _status = "Ready.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    public ICommand RunCommand { get; }

    public MainWindowViewModel()
    {
        BeginDate = StartOfNextMeaningfulMonday();
        EndDate = BeginDate?.AddDays(7);
        RunCommand = new RelayCommand(async _ =>
        {
            try
            {
                var urls = (UrlsText ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (urls.Length == 0) { Status = "Please enter at least one URL."; return; }

                var selectOnly = (SelectOnlyText ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (!int.TryParse(BeginHourText, out var beginHour) || beginHour is < 0 or > 23)
                { Status = "Begin hour must be 0–23."; return; }

                if (!int.TryParse(EndHourText, out var endHour) || endHour is < 0 or > 23)
                { Status = "End hour must be 0–23."; return; }

                DateTimeOffset? begin = NoDateFilter ? null : BeginDate;
                DateTimeOffset? end = NoDateFilter ? null : (EndDate ?? BeginDate?.AddDays(7));

                if (!NoDateFilter && begin.HasValue && end.HasValue && end <= begin)
                { Status = "End date must be after begin date."; return; }

                Status = "Running…";
                var exitCode = await Parser.Run(urls, selectOnly, begin, end, beginHour, endHour);
                Status = exitCode == 0 ? "Done." : $"Finished with exit code {exitCode}.";
            }
            catch (Exception ex)
            {
                Status = $"Error: {ex.Message}";
            }
        });
    }

    private static DateTimeOffset StartOfNextMeaningfulMonday()
    {
        const int N = 4;
        var start = DateTimeOffset.Now.AddDays(N);
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)start.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0) daysUntilMonday = 7;
        return start.Date.AddDays(daysUntilMonday);
    }
}
