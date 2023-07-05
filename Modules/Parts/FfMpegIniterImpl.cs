#nullable enable

using System.Collections.Immutable;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using FFmpeg.AutoGen.Native;
using MCDis.Design.Extensions;
using MCDis.Design.IO;
using MCDis.ImageSequenceCutter.Modules.Data;
using MimeLut;

namespace MCDis.ImageSequenceCutter.Modules.Parts;

public class FfMpegIniterImpl
{
  private readonly ImmutableHashSet<string> p_extensions;
  private readonly ImmutableHashSet<string> p_mimes;

  public FfMpegIniterImpl()
  {
    p_extensions =
      new[] { "mov", "mp4", "mp4v", "avi", "mpg", "mp2", "mpeg", "mkv", "3gp", "flv", "wmv", "webm" }
        .ToImmutableHashSet(StringComparer.InvariantCultureIgnoreCase);

    p_mimes = (from ext in p_extensions
               let mime = MimeLutTable.FindMimeTypeByExtension(ext)
               where !mime.IsNullOrEmpty()
               select mime)
      .ToImmutableHashSet(StringComparer.InvariantCultureIgnoreCase);
  }

  public DirectoryPath? Location { get; private set; }
  public ImmutableHashSet<string> Extensions => p_extensions;
  public ImmutableHashSet<string> Mimes => p_mimes;
  
  public void InitInternal()
  {
    FilePath.FromRelativeOrAbsolute("Data");
    var location = DirectoryPath.FromAppDomain()["Data"];
    if (!location.Exists || location.Name !="Data")
      throw new ArgumentNullException(nameof(location), $"Путь не существует");

    InitFfmpegLoader(location);
    ffmpeg.avformat_network_init();
    Location = location;
  }

  public bool CanProcess(string _fileType, string _originFilename)
  {
    if (!_fileType.IsNullOrEmpty() && p_mimes.Contains(_fileType))
      return true;

    if (_originFilename.IsNullOrEmpty())
      return false;

    var ext = System.IO.Path.GetExtension(_originFilename)?.TrimStart('.');
    return ext != null && p_extensions.Contains(ext);
  }

  public MediaLibraryMediaInfoSection Process(FilePath _file)
  {
    try
    {
      var res = GetMeta(_file);
      
      return res ?? new MediaLibraryMediaInfoSection();
    }
    catch (Exception e)
    {
      throw new Exception($"Ошибка процесса извлечения мета данных: {e}");
    }
  }

  private static unsafe MediaLibraryMediaInfoSection? GetMeta(FilePath _file)
  {
    AVFormatContext* inputCtx = null;
    AVCodec* decoder = null;
    try
    {
      inputCtx = ffmpeg.avformat_alloc_context();
      if (inputCtx == null)
        return null;
      var res = ffmpeg.avformat_open_input(&inputCtx, _file.Path, null, null);

      if (res < 0)
        return null;

      lock (FfMpegHelper.GSync)
      {
        if (ffmpeg.avformat_find_stream_info(inputCtx, null) < 0)
          return null;
      }

      AVDictionaryEntry* tag = null;
      while ((tag = ffmpeg.av_dict_get(inputCtx->metadata, "", tag, ffmpeg.AV_DICT_IGNORE_SUFFIX)) != null)
      {
        var key = Marshal.PtrToStringAnsi((IntPtr)tag->key);
        var value = Marshal.PtrToStringAnsi((IntPtr)tag->value);
      }

      var streamIndex = ffmpeg.av_find_best_stream(inputCtx, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &decoder, 0);
      if (streamIndex < 0)
        return null;
      var stream = inputCtx->streams[streamIndex];

      tag = null;
      var width = stream->codec->width;
      var height = stream->codec->height;
      int? rotation = null;

      var codecId = stream->codec->codec_id.ToString("G");
      var codecName = ffmpeg.avcodec_get_name(stream->codec->codec_id);
      
      while ((tag = ffmpeg.av_dict_get(stream->metadata, "", tag, ffmpeg.AV_DICT_IGNORE_SUFFIX)) != null)
      {
        var key = Marshal.PtrToStringAnsi((IntPtr)tag->key);
        var value = Marshal.PtrToStringAnsi((IntPtr)tag->value);
       
        if (key == "rotate")
        {
          if (int.TryParse(value, out var degree))
            rotation = degree;
        }
      }

      TimeSpan? duration;
      if (stream->duration < 0)
        duration = TimeSpan.FromSeconds(inputCtx->duration / 1000000.0);
      else
        duration = TimeSpan.FromSeconds(stream->duration * stream->time_base.num /
                                        (double)stream->time_base.den);
      var frames = stream->nb_frames;
      if (frames == 0)
        frames = (long)Math.Round(duration.Value.TotalSeconds * stream->avg_frame_rate.num /
                                  stream->avg_frame_rate.den);
      return new MediaLibraryMediaInfoSection
      {
        Width = width,
        Height = height,
        Codec = codecName,
        Fps = new MediaRate { Num = stream->avg_frame_rate.num, Den = stream->avg_frame_rate.den },
        Duration = duration,
        Frames = frames,
        Rotation = rotation,
        Extra = new Dictionary<string, string>
        {
          ["ffmpeg.codecId"] = codecId
        }
      };
    }
    catch (Exception e)
    {
      throw new Exception($"Ошибка получения меты для видео '{_file}': {e}");
    }
    finally
    {
      if (inputCtx != null)
      {
        lock (FfMpegHelper.GSync)
          ffmpeg.avformat_close_input(&inputCtx);
      }
    }
  }

  private static void InitFfmpegLoader(DirectoryPath? _location)
  {
    var sync = new object();
    var loadedLibraries = new Dictionary<string, IntPtr>();
    var ffmpegRoot = _location;
    ffmpeg.GetOrLoadLibrary = (_name) =>
    {
      var version = ffmpeg.LibraryVersionMap[_name];
      var key = $"{_name}{version}";
      if (loadedLibraries.TryGetValue(key, out var num))
        return num;
      lock (sync)
      {
        if (loadedLibraries.TryGetValue(key, out num))
          return num;
        SetDllDirectory(ffmpegRoot.Path);
        num = LibraryLoader.LoadNativeLibrary(ffmpegRoot.F($"{_name}-{version}.dll").Path);
        SetDllDirectory(DirectoryPath.FromEntryAssembly().Path);
        if (num == IntPtr.Zero)
          throw new DllNotFoundException(
            $"Unable to load DLL '{_name}.{version}': The specified module could not be found.");
        loadedLibraries.Add(key, num);
      }

      return num;
    };
  }

  [DllImport("kernel32", SetLastError = true)]
  public static extern bool SetDllDirectory(string _lpPathName);
}
