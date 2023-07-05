using MCDis.Design.IO;

namespace MCDis.ImageSequenceCutter.Modules.Data;

public class WorkingDirectoryInfo
{
  public WorkingDirectoryInfo(MediaLibraryMediaInfoSection _resourceFFmpegInfo,
    DirectoryPath _root,
    DirectoryPath _workingDirectory,
    ResourceInfo _resourceInfo,
    string _directoryName)
  {
    ResourceFFmpegInfo = _resourceFFmpegInfo;
    Root = _root;
    WorkingDirectory = _workingDirectory;
    ResourceInfo = _resourceInfo;
    DirectoryName = _directoryName;
  }
  public MediaLibraryMediaInfoSection ResourceFFmpegInfo { get; init; }
  public DirectoryPath Root { get; init; }
  public DirectoryPath WorkingDirectory { get; init; }
  public ResourceInfo ResourceInfo { get; init; }
  public string DirectoryName { get; init; }
}