using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing.Imaging;
using MCDis.Design.IO;
using MCDis.Design.IO.Extensions;
using MCDis.Design.IO.Json;
using MCDis.ImageSequenceCutter.Modules.Data;
using static System.String;

namespace MCDis.ImageSequenceCutter.Toolkit;

public static class ImageTool
{
  public static void CutSequence()
  {
    var dialog = new FolderBrowserDialog();
    if (dialog.ShowDialog() != DialogResult.OK)
      return;
    
    var root = DirectoryPath.FromString(dialog.SelectedPath);

    if (!root.Exists)
      throw new ArgumentNullException(nameof(root), $"Путь не существует");

    var frames = root.EnumerateFiles("*.jpg", SearchOption.TopDirectoryOnly)
      .Concat(root.EnumerateFiles("*.jpeg", SearchOption.TopDirectoryOnly))
      .Concat(root.EnumerateFiles("*.png", SearchOption.TopDirectoryOnly))
      .ToList();
    
    frames.Sort((_a, _b) => Compare(_a.Path, _b.Path, StringComparison.Ordinal));

    var configPath = FilePath.FromRelativeOrAbsolute("Config/image.json");

    var conf = configPath.ReadObjectAsJson<AreasScheme>();
    
    var width = conf.Width;
    var height = conf.Height;
    var areas = conf.Areas;
    var isCreateVideo = conf.CreateVideo;
    var isClean = conf.Clean;
    var ffmpegArgs = conf.FfmpegArgs;
    
    if (root["image"].Exists)
    {
      foreach (var file in root["image"].EnumerateFiles())
      {
        file.TryDelete();
      }
    }
    else
      root["image"].Create();

    foreach (var area in areas)
      root[area.Name].Create();

    var files = new ConcurrentBag<FilePath>();
    
    foreach (var _job in frames.Select((_path, _i) => new {LocalPath = _path, Index = _i}))
    {
      using var frameImg = Image.FromFile(_job.LocalPath.Path) as Bitmap;
      using var frameImgDst = new Bitmap(width, height, PixelFormat.Format24bppRgb);

      using (var gfx = Graphics.FromImage(frameImgDst))
      {
        if (frameImg != null)
          gfx.DrawImage(frameImg, new Rectangle(0, 0, frameImgDst.Width, frameImgDst.Height),
            new Rectangle(0, 0, frameImg.Width, frameImg.Height), GraphicsUnit.Pixel);
      }

      var jpgEncoder = GetEncoder(ImageFormat.Jpeg);
      var myEncoder = Encoder.Quality;
      var encoderParameters = new EncoderParameters(1);
      var myEncoderParameter = new EncoderParameter(myEncoder, 100L);
      encoderParameters.Param[0] = myEncoderParameter;

      foreach (var area in areas)
      {
        using var canvas = new Bitmap(area.Region.Width, area.Region.Height, PixelFormat.Format24bppRgb);
        using var gfx = Graphics.FromImage(canvas);

        gfx.DrawImage(frameImg, new Rectangle(0, 0, canvas.Width, canvas.Height),
          new Rectangle(area.Region.X, area.Region.Y, area.Region.Width, area.Region.Height),
          GraphicsUnit.Pixel);
        var filename = root[area.Name].F($"{area.Name}-{_job.Index}.jpg");

        if (filename.Exists)
          filename.TryDelete();

        canvas.Save(filename.Path, jpgEncoder, encoderParameters);
        files.Add(filename);
      }

      Console.WriteLine($@"Processed {_job.LocalPath.Path}");
    }

    var pad = $"pad=width={width/2}:height={height*2}:color=black";
    //var pad = $"";

    for (var i = 0; i < frames.Count; i++)
    {
      var leftPart = root[areas[0].Name].F($"{areas[0].Name}-{i}.jpg");
      var rightPart = root[areas[1].Name].F($"{areas[1].Name}-{i}.jpg");

      var fileName = DirectoryPath.FromEntryAssembly()?.F("ffmpeg.exe").Path;
      var workingDirectory = root["output"].Path;
      //var arguments = $"-i {leftPart} -i {rightPart} -filter_complex {pad} vstack=inputs=2 output-{i}.jpg";
      var arguments = $"-i {leftPart} -i {rightPart} -filter_complex vstack=inputs=2  output-{i}.jpg";
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

    if (isCreateVideo)
    {
      foreach (var area in areas)
      {
        Console.WriteLine($@"-y -f image2 -i frame%d.jpg {ffmpegArgs} {area.Name}.mp4");
        
        var fileName = DirectoryPath.FromEntryAssembly()?.F("ffmpeg.exe").Path;
        var workingDirectory = root[area.Name].Path;
        var arguments = $"-y -f image2 -i frame%d.jpg {ffmpegArgs} {area.Name}.mp4";
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

      if (isClean)
      {
        foreach (var file in files)
          file.TryDelete();
      }
    }
  }

  private static ImageCodecInfo GetEncoder(ImageFormat _format)
    => ImageCodecInfo
      .GetImageDecoders()
      .First(_codec 
        => _codec.FormatID == _format.Guid);

}