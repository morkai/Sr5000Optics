using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sr5000Optics
{
  public class Config
  {
    public int Bank { get; set; } = 0;

    public string LedsPrefix { get; set; } = "7";

    public string OpticsPrefix { get; set; } = "6";

    public string IpAddress { get; set; }

    public int ConnectRetryCount { get; set; } = 0;

    public int ConnectRetryDelay { get; set; } = 2000;

    public int TriggerRetryCount { get; set; } = 0;

    public int TriggerRetryDelay { get; set; } = 1000;

    public Dictionary<string, IList<LuminaryConfig>> Luminaries { get; set; }

    public class KeyenceConfig
    {
      public string Host { get; set; }

      public int ConnectRetryCount { get; set; } = 0;

      public int ConnectRetryDelay { get; set; } = 2000;
    }

    public class LuminaryConfig
    {
      public Dictionary<string, IList<AreaConfig>> Leds { get; set; }

      public Dictionary<string, IList<AreaConfig>> Optics { get; set; }

      public int CountLeds()
      {
        var count = 0;

        foreach (var areas in Leds.Values)
        {
          count += areas.Count;
        }

        return count;
      }

      public int CountOptics()
      {
        var count = 0;

        foreach (var areas in Optics.Values)
        {
          count += areas.Count;
        }

        return count;
      }
    }

    public class AreaConfig
    {
      public int X1 { get; set; }
      public int Y1 { get; set; }
      public int X2 { get; set; }
      public int Y2 { get; set; }

      public override string ToString()
      {
        return $"{X1}x{Y1} {X2}x{Y2}";
      }

      public string ToCommand()
      {
        var x1 = X1.ToString().PadLeft(4, '0');
        var y1 = Y1.ToString().PadLeft(4, '0');
        var x2 = X2.ToString().PadLeft(4, '0');
        var y2 = Y2.ToString().PadLeft(4, '0');

        return $"{x1}{y1}{x2}{y2}";
      }
    }

    public static Config CreateFromFile()
    {
      var rootDirPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
      var prodFilePath = Path.Combine(rootDirPath, "config.production.json");
      var filePath = File.Exists(prodFilePath) ? prodFilePath : Path.Combine(rootDirPath, "config.json");
      var json = File.ReadAllText(filePath, Encoding.UTF8);
      var options = new JsonSerializerOptions()
      {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true
      };
      
      return JsonSerializer.Deserialize<Config>(json, options);
    }
  }
}
