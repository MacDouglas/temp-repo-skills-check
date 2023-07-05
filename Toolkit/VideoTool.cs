using System.Collections.Concurrent;
using System.Diagnostics;
using MCDis.Design.IO;
using MCDis.Design.IO.Extensions;
using MCDis.Design.IO.Json;
using MCDis.ImageSequenceCutter.Modules.Data;
using MCDis.ImageSequenceCutter.Modules.Parts;

namespace MCDis.ImageSequenceCutter.Toolkit;

public class VideoTool
{
  private readonly FfMpegIniterImpl p_ffmpegExtractor;

  public VideoTool(CancellationToken _cancellation)
  {
    p_ffmpegExtractor = new FfMpegIniterImpl();
    p_ffmpegExtractor.InitInternal();
  }

  public void CutVideoByScheme()
  {
    var extensions = p_ffmpegExtractor.Extensions;
    var mimes = p_ffmpegExtractor.Mimes;

    var dialog = new FolderBrowserDialog();
    if (dialog.ShowDialog() != DialogResult.OK)
      return;

    var root = DirectoryPath.FromString(dialog.SelectedPath);
    if (!root.Exists)
      throw new ArgumentNullException(nameof(root), $"Путь не существует");

    var configPath = FilePath.FromRelativeOrAbsolute("Config/video.json");
    var schemeConf = configPath.ReadObjectAsJson<AreasScheme>();

    //foreach (var area in conf.Areas)
    //  root[$"video-{area.Name}"].Create();

    var files = new List<ResourceInfo>();
    foreach (var extension in extensions)
    {
      files.AddRange(
        root
          .EnumerateFiles($"*.{extension}", SearchOption.TopDirectoryOnly)
          .Select(_filePath =>
            new ResourceInfo(_filePath, _filePath.Name, _filePath.Extension)));
    }

    var directoriesInfo = new ConcurrentBag<WorkingDirectoryInfo>();

    var options = new ParallelOptions { MaxDegreeOfParallelism = files.Count > 6 ? files.Count : 6 };
    Parallel.ForEach(files, options, (_fileInfo, _token) =>
    {
      if (!p_ffmpegExtractor.CanProcess(_fileInfo.FileExtension, _fileInfo.FileName))
        return;

      var res = p_ffmpegExtractor.Process(_fileInfo.FilePath);

      if (res.Duration > TimeSpan.FromSeconds(0))
        Console.WriteLine();

      var directoryName = _fileInfo.FileName.TrimEnd(_fileInfo.FileExtension.ToCharArray());

      DirectoryTool.CleanOrCreateDirectory(root, directoryName);

      var workingDirectory = root[$"tmp-{directoryName}"];
      ConvertVideoToImages(workingDirectory, directoryName, _fileInfo.FilePath);

      var info = new WorkingDirectoryInfo(res, root, workingDirectory, _fileInfo, directoryName);
      directoriesInfo.Add(info);
    });

    foreach (var info in directoriesInfo)
    {
      ImageCutter.CutImageByScheme(info.WorkingDirectory, schemeConf);
    }

    Parallel.ForEach(directoriesInfo, options, (_directoriesInfo, _token) =>
    {
      CreateVideoFromImages(_directoriesInfo);

      if (true) 
      { 
        DirectoryTool.RecursiveDeleteFilesAndDirectories(_directoriesInfo.WorkingDirectory);
      }
    });
  }
  private static void CreateVideoFromImages(WorkingDirectoryInfo _directoryInfo)
  {
    var framerate = _directoryInfo.ResourceFFmpegInfo.Fps.Num;
    var filename = $"final_{_directoryInfo.ResourceInfo.FileName}";

    var fileName = FilePath.FromRelativeOrAbsolute("ffmpeg.exe").Path;
    var workingDirectory = _directoryInfo.WorkingDirectory["tmp-output"].Path;
    var arguments = $" -framerate {framerate} -i %d.jpg -pix_fmt yuv420p {_directoryInfo.Root.Path}/{filename}";
    var shellExecute = false;

    var proc = new Process
    {
      StartInfo =
      {
        FileName = fileName,
        WorkingDirectory = workingDirectory,
        Arguments = arguments,
        UseShellExecute = shellExecute
      }
    };
    proc.Start();
    proc.WaitForExit();
  }

  private static void ConvertVideoToImages(DirectoryPath _workingDirectory, string _directoryName, FilePath _resourcePath)
  {
    var fileName = FilePath.FromRelativeOrAbsolute("ffmpeg.exe").Path;
    var workingDirectory = _workingDirectory.Path;
    var arguments = $"-i {_resourcePath} image%d.jpg";
    var shellExecute = false;

    var proc = new Process
    {
      StartInfo =
      {
        FileName = fileName,
        WorkingDirectory = workingDirectory,
        Arguments = arguments,
        UseShellExecute = shellExecute,
      }
    };
    proc.Start();
    proc.WaitForExit();
  }

}

