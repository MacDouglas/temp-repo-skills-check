using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using MCDis.Design.IO;
using MCDis.Design.IO.Extensions;
using MCDis.ImageSequenceCutter.Modules.Data;
using MCDis.ImageSequenceCutter.Toolkit;

namespace MCDis.ImageSequenceCutter.Modules.Parts;

public static class ImageCutter
{
  public static void CutImageByScheme(DirectoryPath _root, AreasScheme _areasScheme)
  {
    var frames = _root.EnumerateFiles("*.jpg", SearchOption.TopDirectoryOnly)
      .Concat(_root.EnumerateFiles("*.jpeg", SearchOption.TopDirectoryOnly))
      .Concat(_root.EnumerateFiles("*.png", SearchOption.TopDirectoryOnly))
      .ToList();
    var sortedFrames = frames.OrderBy(_x => PadNumbers(_x.Path));

    var width = _areasScheme.Width;
    var height = _areasScheme.Height;
    var areas = _areasScheme.Areas;
    
    foreach (var area in areas)
      DirectoryTool.CleanOrCreateDirectory(_root, area.Name);
    DirectoryTool.CleanOrCreateDirectory(_root, "output");

    var files = new ConcurrentBag<FilePath>();
    
    Parallel.ForEach(
      sortedFrames.Select((_, _i) => new { Path = _, Index = _i }),
      new ParallelOptions { MaxDegreeOfParallelism = 16 },
      _job =>
    {
      using var frameImg = Image.FromFile(_job.Path.Path) as Bitmap;
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
        var filename = _root[$"tmp-{area.Name}"].F($"{area.Name}-{_job.Index}.jpg");

        if (filename.Exists)
          filename.TryDelete();

        canvas.Save(filename.Path, jpgEncoder, encoderParameters);
      }

      var leftPart = _root[$"tmp-{areas[0].Name}"].F($"{areas[0].Name}-{_job.Index}.jpg");
      var rightPart = _root[$"tmp-{areas[1].Name}"].F($"{areas[1].Name}-{_job.Index}.jpg");

      CutImageByScheme(_root, leftPart, rightPart, _job.Index);
    });
  }

  private static void CutImageByScheme(DirectoryPath _root, FilePath _leftPart, FilePath _rightPart, int _index)
  {
    var fileName = DirectoryPath.FromEntryAssembly()?.F("ffmpeg.exe").Path;
    var workingDirectory = _root["tmp-output"].Path;
    var arguments = $"-i {_leftPart} -i {_rightPart} -filter_complex vstack=inputs=2  {_index}.jpg";
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

  private static string PadNumbers(string _input)
  {
    return Regex.Replace(_input, "[0-9]+", _match => _match.Value.PadLeft(10, '0'));
  }
  private static ImageCodecInfo GetEncoder(ImageFormat _format)
    => ImageCodecInfo
      .GetImageDecoders()
      .First(_codec
        => _codec.FormatID == _format.Guid);

}