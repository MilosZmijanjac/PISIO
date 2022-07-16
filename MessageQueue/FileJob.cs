namespace MessageQueue.Models;

public class FileJob
{
    public string? Id { get; set; }
    public string? Extension { get; set; }
    public List<Byte[]>? Files { get; set; }
}
