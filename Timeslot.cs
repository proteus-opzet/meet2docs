namespace Meet2Docs;

internal class Timeslot
{
    public int Index { get; }
    public long BeginTimestamp { get; }
    public List<string> AvailableUsers { get; set; } = [];

    public DateTimeOffset DateTimeBegin => TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(BeginTimestamp), Program.Timezone);
    public int DayOfTheWeek => (int)DateTimeBegin.DayOfWeek;
    public string DateString => DateTimeBegin.ToString("yyyy-MM-dd");

    public string BeginTimeString => DateTimeBegin.ToString("HH:mm");

    public int CountAvailable => AvailableUsers.Count;
    public bool AtLeast3Users => CountAvailable >= 3;

    public bool IsPartOfBlock { get; set; }

    public Timeslot(int index, long beginTimestamp)
    {
        Index = index;
        BeginTimestamp = beginTimestamp;
    }

    public override string ToString()
    {
        return $"Timeslot(index={Index}, time={DateTimeBegin}, availableUsers={string.Join(", ", AvailableUsers)}, isViable={AtLeast3Users})";
    }
}