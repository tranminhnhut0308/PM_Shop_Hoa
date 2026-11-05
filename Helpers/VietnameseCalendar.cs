namespace ShopHoa.Helpers;

public static class VietnameseCalendar
{
    private static readonly TimeSpan UtcOffset = TimeSpan.FromHours(7);

    public static DateTimeOffset Today()
        => FromDateTime(DateTime.UtcNow);

    public static DateTimeOffset FromCalendarDate(DateTimeOffset value)
        => FromDateTime(DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified));

    public static DateTimeOffset FromDateTime(DateTime value)
    {
        var vietnamDate = value.Kind switch
        {
            DateTimeKind.Utc => value.Add(UtcOffset).Date,
            DateTimeKind.Local => value.ToUniversalTime().Add(UtcOffset).Date,
            _ => value.Date
        };

        var calendarDate = DateTime.SpecifyKind(vietnamDate, DateTimeKind.Unspecified);
        return new DateTimeOffset(calendarDate, UtcOffset);
    }

    public static DateTime ToDatabaseWallClockDate(DateTimeOffset value)
        => DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified);
}
