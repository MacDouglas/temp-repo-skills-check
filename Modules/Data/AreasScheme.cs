namespace MCDis.ImageSequenceCutter.Modules.Data;

public record AreasScheme
{
  public int Width { get; init; }
  public int Height { get; init; }
  public List<AreaScheme> Areas { get; init; }
  public bool CreateVideo { get; init; }
  public bool Clean { get; init; }
  public string FfmpegArgs { get; init; }
}