namespace Meet2Docs.Util;

internal class Timeslot
{
    public int Index { get; }
    public long Timestamp { get; }
    public List<string> AvailableUsers { get; set; }
    public DateTimeOffset DateTime { get; }
    public int DayOfTheWeek { get; }

    public Timeslot(int index, long timestamp)
    {
        Index = index;
        Timestamp = timestamp;
        AvailableUsers = [];

        var amsterdamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");
        DateTime = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(timestamp), amsterdamTimeZone);
        DayOfTheWeek = (int)DateTime.DayOfWeek;
    }

    public override string ToString()
    {
        return $"Timeslot(idx={Index}, time={DateTime}, available_users={string.Join(", ", AvailableUsers)})";
    }
}