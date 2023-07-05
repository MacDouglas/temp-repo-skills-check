using MCDis.ImageSequenceCutter.Toolkit;

namespace MCDis.ImageSequenceCutter
{
  public static class Program
  {

    [STAThread]
    static void Main()
    {
      var cancellationSource = new CancellationTokenSource();
      var token = cancellationSource.Token;

      //ImageTool.CutVideoByScheme();

      var videoCutter = new VideoTool(token);
      videoCutter.CutVideoByScheme();

      //ApplicationConfiguration.Initialize();
      //Application.Run(new Form1());
    }
  }
}