using MCDis.Design.IO;
using MCDis.Design.IO.Extensions;

namespace MCDis.ImageSequenceCutter.Toolkit;

public static class DirectoryTool
{
  public static void RecursiveDeleteFilesAndDirectories(DirectoryPath _baseDir)
  {
    if (!_baseDir.Exists)
      return;

    foreach (var dir in _baseDir.EnumerateDirectories())
    {
      RecursiveDeleteFilesAndDirectories(dir);
    }
    _baseDir.Delete(true);
  }

  public static void CleanOrCreateDirectory(DirectoryPath _root, string _directoryName)
  {
    if (_root[$"tmp-{_directoryName}"].Exists)
    {
      foreach (var existingFile in _root[$"tmp-{_directoryName}"].EnumerateFiles())
      {
        existingFile.TryDelete();
      }
    }
    _root[$"tmp-{_directoryName}"].Create();
  }

  public static void CleanAndDeleteDirectory(DirectoryPath _root, string _directoryName)
  {
    if (_root[$"tmp-{_directoryName}"].Exists)
    {
      foreach (var existingFile in _root[$"tmp-{_directoryName}"].EnumerateFiles())
      {
        existingFile.TryDelete();
      }
    }
    _root[$"tmp-{_directoryName}"].Delete();
  }
}