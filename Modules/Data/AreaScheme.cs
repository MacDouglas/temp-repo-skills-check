namespace MCDis.ImageSequenceCutter.Modules.Data;

public record AreaScheme
{
  public string Name { get; init; }
  public RectangleScheme Region { get; init; }
}