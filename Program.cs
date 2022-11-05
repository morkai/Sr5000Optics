using Keyence.AutoID.SDK;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;

namespace Sr5000Optics
{
  internal class Program
  {
    static string TMP_IMAGE_FILE_PATH = Path.Combine(Path.GetTempPath(), "sr5000.jpg");

    static string ROOT_PATH = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

    static Input input;

    static Config config;

    static ReaderAccessor reader;

    static Config.LuminaryConfig luminary;

    static Dictionary<string, AreaResult> areaResults;

    static bool ftpOpen = false;

    static void Main(string[] args)
    {
      Log("Hello!");

      ReadInput(args);
      ReadConfig();

      switch (input.Operation)
      {
        case "trigger":
          ValidateInputConfig();
          Connect();
          OpenFtp();
          DeleteOldImages();
          Configure();
          Trigger();
          DownloadNewImage();
          CloseFtp();
          Disconnect();
          PrepareResultImage();
          CheckResults();
          break;

        case "areas":
          Connect();
          ReadAreas();
          Disconnect();
          break;

        default:
          Err($"Unknown operation [{input.Operation}].");
          break;
      }
    }

    static void Err(string message)
    {
      if (reader != null && reader.LastErrorInfo != ErrorCode.None)
      {
        message = $"{message} Last SR-5000 error: {reader.LastErrorInfo}";
      }

      CloseFtp();
      Disconnect();

      Log(message);

      Environment.Exit(1);
    }

    static void Log(string message)
    {
      Console.Error.WriteLine(message);
    }

    static string Cmd(string cmd)
    {
      if (reader == null)
      {
        Err($"Cannot execute command [{cmd}]: not connected.");
      }

      var result = reader.ExecCommand(cmd);

      if (reader.LastErrorInfo != ErrorCode.None)
      {
        Err($"Failed to execute command [{cmd}].");
      }

      return result;
    }

    static void ReadInput(string[] args)
    {
      Log("Reading the input args...");

      try
      {
        input = Input.CreateFromArgs(args);

        var devFilePath = Path.Combine(ROOT_PATH, "input.dev.json");

        if (File.Exists(devFilePath))
        {
          Log("Reading the dev input file...");

          input.ReadFromFile(devFilePath);
        }
      }
      catch (Exception x)
      {
        Err($"Failed to read the input args: {x.Message}");
      }
    }

    static void ReadConfig()
    {
      Log("Reading the config file...");

      try
      {
        config = Config.CreateFromFile();
      }
      catch (Exception x)
      {
        Err($"Failed to read the config file: {x.Message}");
      }
    }

    static void ValidateInputConfig()
    {
      if (!config.Luminaries.ContainsKey(input.Luminary))
      {
        Err($"Unknown luminary [{input.Luminary}].");
      }

      var ledsCount = 0;
      var opticsCount = 0;

      foreach (var component in input.Components.Values)
      {
        if (component.Item.StartsWith(config.LedsPrefix))
        {
          ledsCount += component.Quantity;
        }
        else if (component.Item.StartsWith(config.OpticsPrefix))
        {
          opticsCount += component.Quantity;
        }
      }

      luminary = config.Luminaries[input.Luminary].FirstOrDefault(
        l => l.CountLeds() == ledsCount && l.CountOptics() == opticsCount
      );

      if (luminary == null)
      {
        Err($"No config for [{ledsCount}] LEDs and [{opticsCount}] optics in [{input.Luminary}].");
      }

      foreach (var kvp in luminary.Leds)
      {
        var item = kvp.Key;
        var areas = kvp.Value;

        if (!input.Components.ContainsKey(item))
        {
          Err($"No LED component with item [{item}].");
        }

        if (input.Components[item].Quantity != areas.Count)
        {
          Err($"Areas and quantity mismatch for LED item [{item}].");
        }
      }

      foreach (var kvp in luminary.Optics)
      {
        var item = kvp.Key;
        var areas = kvp.Value;

        if (!input.Components.ContainsKey(item))
        {
          Err($"No optics component with item [{item}].");
        }

        if (input.Components[item].Quantity != areas.Count)
        {
          Err($"Areas and quantity mismatch for optics item [{item}].");
        }
      }
    }

    static void Connect()
    {
      var tryLimit = 1 + Math.Max(0, config.ConnectRetryCount);

      for (var tryNo = 1; tryNo <= tryLimit; ++tryNo)
      {
        Log($"Connecting to [{config.IpAddress}] ({tryNo}/{tryLimit})...");

        reader = new ReaderAccessor(config.IpAddress);

        if (reader.Connect())
        {
          return;
        }

        if (config.ConnectRetryDelay > 0)
        {
          Thread.Sleep(config.ConnectRetryDelay);
        }
      }

      Err("Failed to connect to the reader.");
    }

    private static void ReadAreas()
    {
      int x1, y1, x2, y2;

      for (var i = 1; i <= 250; ++i)
      {
        var areaNo = i.ToString().PadLeft(3, '0');
        var result = Cmd($"RD,{areaNo}").Trim();

        if (result.StartsWith("ER,"))
        {
          Err($"Failed to read areas: {result}");
        }

        var parts = result.Split(',');
        var pos = parts.Length == 3 ? parts[2] : "";
        
        if (!int.TryParse(pos.Substring(0, 4), out x1))
        {
          break;
        }

        if (!int.TryParse(pos.Substring(4, 4), out y1))
        {
          break;
        }

        if (!int.TryParse(pos.Substring(8, 4), out x2))
        {
          break;
        }

        if (!int.TryParse(pos.Substring(12, 4), out y2))
        {
          break;
        }

        if (x1 == 0 && y1 == 0 && x2 == 0 && y2 == 0)
        {
          break;
        }

        Console.WriteLine($"{areaNo}: {{\"x1\": {x1}, \"y1\": {y1}, \"x2\": {x2}, \"y2\": {y2}}}");
      }
    }

    static void OpenFtp()
    {
      Log("Opening the FTP...");

      if (!reader.OpenFtp())
      {
        Err("Failed to open the FTP.");
      }

      ftpOpen = true;
    }

    static void DeleteOldImages()
    {
      Log("Deleting old images...");

      try
      {
        Log($"Deleting the local temporary image file...");

        File.Delete(TMP_IMAGE_FILE_PATH);
      }
      catch (DirectoryNotFoundException) { }
      catch (Exception x)
      {
        Err($"Failed to delete the local temporary image file [{TMP_IMAGE_FILE_PATH}]: {x.Message}");
      }

      foreach (var file in reader.GetFileList("IMAGE"))
      {
        Log($"Deleting SR-5000 file: [IMAGE/{file}]...");

        if (!reader.DeleteFile($"IMAGE/{file}"))
        {
          Err($"Failed to delete a file on the SR-5000: [IMAGE/{file}].");
        }
      }
    }

    static void Configure()
    {
      Log("Configuring the reader...");

      Log("Resetting all areas...");
      Cmd("DEFAULTDAREA,0");

      Log("Setting new areas...");
      ConfigureAreas();

      Log($"Setting codes to read to [{areaResults.Count}]...");
      Cmd($"WP,250,{areaResults.Count}");

      Log($"Setting max DataMatrix codes to [{areaResults.Count}]...");
      Cmd($"WP,253,{areaResults.Count}");

      Log($"Allowing reduced detection count...");
      Cmd($"WP,251,1");
    }

    static void ConfigureAreas()
    {
      areaResults = new Dictionary<string, AreaResult>();

      var nextAreaNo = 1;

      foreach (var kvp in luminary.Leds)
      {
        var item = kvp.Key;
        var areas = kvp.Value;

        foreach (var area in areas)
        {
          var areaNo = nextAreaNo.ToString().PadLeft(3, '0');

          Cmd($"WD,{areaNo},{area.ToCommand()}");

          areaResults[areaNo] = new AreaResult(area, input.Components[item]);

          nextAreaNo += 1;
        }
      }

      foreach (var kvp in luminary.Optics)
      {
        var item = kvp.Key;
        var areas = kvp.Value;

        foreach (var area in areas)
        {
          var areaNo = nextAreaNo.ToString().PadLeft(3, '0');

          Cmd($"WD,{areaNo},{area.ToCommand()}");

          areaResults[areaNo] = new AreaResult(area, input.Components[item]);

          nextAreaNo += 1;
        }
      }
    }

    static void Trigger()
    {
      var tryLimit = 1 + Math.Max(0, config.TriggerRetryCount);
      var bank = input.Bank >= 0 ? input.Bank : config.Bank;
      var lon = bank > 0 ? $"LON,{bank.ToString().PadLeft(2, '0')}" : "LON";

      for (var tryNo = 1; tryNo <= tryLimit; ++tryNo)
      {
        Log($"Triggering ({tryNo}/{tryLimit})...");

        var results = reader.ExecCommand(lon).Trim().Split('|');
        
        foreach (var result in results)
        {
          var parts = result.Trim().Split(':');
          var code = parts[0];
          var areaNo = parts.Length > 1 ? parts[1] : "000";

          if (areaResults.ContainsKey(areaNo))
          {
            areaResults[areaNo].ReadCode = code;
          }
        }

        if (areaResults.Values.All(r => r.IsFulfilled()))
        {
          break;
        }

        if (config.TriggerRetryDelay > 0)
        {
          Thread.Sleep(config.TriggerRetryDelay);
        }
      }
    }

    static void DownloadNewImage()
    {
      Log("Downloading the new image...");

      for (var i = 0; i < 10; ++i)
      {
        var lastFile = reader.GetFileList("IMAGE").FindLast(file => file.EndsWith(".JPG"));

        if (string.IsNullOrEmpty(lastFile))
        {
          Thread.Sleep(100);

          continue;
        }

        Log($"Downloading file: [IMAGE/{lastFile}]...");

        if (!reader.GetFile($"IMAGE/{lastFile}", TMP_IMAGE_FILE_PATH))
        {
          Err($"Failed to download [IMAGE/{lastFile}] to [{TMP_IMAGE_FILE_PATH}].");
        }

        if (!File.Exists(TMP_IMAGE_FILE_PATH))
        {
          Err($"Downloaded file does not exist [{TMP_IMAGE_FILE_PATH}].");
        }

        return;
      }

      Err("No images found.");
    }

    static void CloseFtp()
    {
      if (reader == null)
      {
        return;
      }

      if (ftpOpen)
      {
        Log("Closing the FTP...");

        reader.CloseFtp();

        ftpOpen = false;
      }
    }

    static void Disconnect()
    {
      if (reader == null)
      {
        return;
      }

      Log("Disconnecting...");

      reader.Disconnect();
      reader = null;
    }

    static void PrepareResultImage()
    {
      Log("Preparing the result image...");

      var img = (Bitmap)Image.FromFile(TMP_IMAGE_FILE_PATH);
      var tmp = new Bitmap(img.Width, img.Height);
      var passPen = new Pen(Color.GreenYellow, 8);
      var failPen = new Pen(Color.Red, 8);

      using (var g = Graphics.FromImage(tmp))
      {
        g.DrawImage(img, 0, 0, img.Width, img.Height);

        foreach (var areaResult in areaResults.Values)
        {
          g.DrawRectangle(
            areaResult.IsValid() ? passPen : failPen,
            areaResult.Area.X1,
            areaResult.Area.Y1,
            areaResult.Area.X2 - areaResult.Area.X1,
            areaResult.Area.Y2 - areaResult.Area.Y1
          );
        }
      }

      var tmpFilePath = TMP_IMAGE_FILE_PATH + ".tmp.jpg";

      tmp.Save(tmpFilePath, ImageFormat.Jpeg);
      tmp.Dispose();
      img.Dispose();

      try
      {
        File.Delete(TMP_IMAGE_FILE_PATH);

        var prevPath = Path.Combine(ROOT_PATH, "previous.jpg");
        var latestPath = Path.Combine(ROOT_PATH, "latest.jpg");

        if (File.Exists(prevPath))
        {
          File.Delete(prevPath);
        }

        if (File.Exists(latestPath))
        {
          File.Move(latestPath, prevPath);
        }

        File.Move(tmpFilePath, latestPath);
      }
      catch (Exception x)
      {
        Err($"Failed to save the result image: {x.Message}");
      }
    }

    static void CheckResults()
    {
      Log("Checking the results...");

      var invalidAreaResults = areaResults.Values.Where(r => !r.IsValid()).ToList();

      if (invalidAreaResults.Count > 0)
      {
        foreach (var result in invalidAreaResults)
        {
          if (result.IsFulfilled())
          {
            Log($"Failed check for component [{result.Component}] in area [{result.Area}]: invalid code found [{result.ReadCode}].");
          }
          else
          {
            Log($"Failed check for component [{result.Component}] in area [{result.Area}]: code not found.");
          }
        }

        Console.WriteLine("NOK");
      }
      else
      {
        Console.WriteLine("OK");
      }
    }
  }
}
