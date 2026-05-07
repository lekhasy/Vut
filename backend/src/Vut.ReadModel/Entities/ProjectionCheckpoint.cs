namespace Vut.ReadModel.Entities;

public sealed class ProjectionCheckpoint
{
    public string ProjectorName { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public int PartitionId { get; set; }
    public long LastOffset { get; set; }
    public DateTime UpdatedAt { get; set; }
}
