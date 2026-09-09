namespace CoolApp.Models;

public class StudySession
{
    public int Id { get; set; }

    public string Course { get; set; } = string.Empty;

    public string Topic { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public DateTime StartsAt { get; set; }

    public string HostName { get; set; } = string.Empty;

    public int SeatsAvailable { get; set; }
}
