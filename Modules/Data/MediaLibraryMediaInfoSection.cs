namespace MCDis.ImageSequenceCutter.Modules.Data;

public class MediaLibraryMediaInfoSection
{
  public int? Width { get; init; }
  public int? Height { get; init; }
  public string Codec { get; init; }
  public long? Frames { get; init; }
  public TimeSpan? Duration { get; init; }
  public int? Rotation { get; init; }
  public MediaRate Fps { get; init; }
  public Dictionary<string, string>? Extra { get; init; }
}