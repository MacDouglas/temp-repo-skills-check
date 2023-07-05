using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace MCDis.ImageSequenceCutter.Modules.Parts;

static class FfMpegHelper
{
  public static readonly object GSync = new();
  private const int BufferSize = 1024;
  public static unsafe string Av_strerror(int _error)
  {
    var buffer = stackalloc byte[BufferSize];
    
    ffmpeg.av_strerror(_error, buffer, (ulong)BufferSize);
    var message = Marshal.PtrToStringAnsi((IntPtr)buffer);
    
    return message;
  }

  public static int ThrowExceptionIfError(this int _error)
  {
    if (_error < 0)
      throw new ApplicationException($"{Av_strerror(_error)}, error = {_error}");
    return _error;
  }
}