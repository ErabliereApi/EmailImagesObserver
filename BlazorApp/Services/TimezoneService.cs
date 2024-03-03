namespace BlazorApp.Services;

public class TimezoneService
{
    public TimeZoneInfo TimeZoneInfo { get; set; } = TimeZoneInfo.Local;

    public string ToPrettyLocalDate(DateTimeOffset? date)
    {
        if (date is null)
        {
            return "Aucune date disponible";
        }

        var now = DateTimeOffset.Now;

        DateTimeOffset nowLocal = TimeZoneInfo.ConvertTime(now, TimeZoneInfo);
        DateTimeOffset dateLocal = TimeZoneInfo.ConvertTime(date.Value, TimeZoneInfo);

        if (dateLocal.Day == nowLocal.Day)
        {
            return dateLocal.ToString("HH:mm:ss");
        }

        return dateLocal.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
