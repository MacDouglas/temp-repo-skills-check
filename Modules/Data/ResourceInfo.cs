using MCDis.Design.IO;

namespace MCDis.ImageSequenceCutter.Modules.Data;

public class ResourceInfo
{
  public ResourceInfo(FilePath _filePath, string _fileName, string _fileExtension)
  {
    FilePath = _filePath;
    FileExtension = _fileExtension;
    FileName = _fileName;
  }
  public FilePath FilePath { get; init; }
  public string FileExtension { get; init; }
  public string FileName { get; init; }
}