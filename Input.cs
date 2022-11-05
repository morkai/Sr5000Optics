using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sr5000Optics
{
  public class Input
  {
    public string Operation { get; set; } = "trigger";

    public int Bank { get; set; } = -1;

    public string Luminary { get; set; } = "";

    public Dictionary<string, Component> Components { get; set; } = new Dictionary<string, Component>();

    class InputJson
    {
      public int Bank { get; set; } = -1;

      public string Luminary { get; set; } = "";

      public IList<Component> Components { get; set; } = new List<Component>();
    }

    public class Component
    {
      public string Item { get; set; }
      public string Material { get; set; }
      public int Quantity { get; set; } = 1;

      public override string ToString()
      {
        return $"{Item}/{Material}/{Quantity}";
      }
    }

    public static Input CreateFromArgs(string[] args)
    {
      var input = new Input();

      for (var i = 0; i < args.Length - 1; i += 2)
      {
        var k = args[i];
        var v = i + 1 < args.Length ? args[i + 1] : string.Empty;

        switch (k)
        {
          case "--config":
          case "--f":
            input.ReadFromFile(v);
            break;

          case "--operation":
          case "--op":
            input.Operation = v;
            break;

          case "--bank":
          case "--b":
            input.Bank = int.Parse(v);
            break;

          case "--luminary":
          case "--l":
            input.Luminary = v;
            break;

          case "--component":
          case "--bom":
            input.AddComponent(v);
            break;

          default:
            throw new Exception($"Unknown argument: [{k}].");
        }
      }

      return input;
    }

    public void ReadFromFile(string filePath)
    {
      var json = File.ReadAllText(filePath, Encoding.UTF8);
      var options = new JsonSerializerOptions()
      {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true
      };
      
      var source = JsonSerializer.Deserialize<InputJson>(json, options);

      if (source.Bank >= 0)
      {
        Bank = source.Bank;
      }

      if (!string.IsNullOrEmpty(source.Luminary))
      {
        Luminary = source.Luminary;
      }

      foreach (var component in source.Components)
      {
        AddComponent(component);
      }
    }

    private void AddComponent(string componentArg)
    {
      var parts = componentArg.Split('/');
      var component = new Component()
      {
        Item = parts[0],
        Material = parts.Length > 1 ? parts[1] : "",
        Quantity = parts.Length > 2 ? int.Parse(parts[2]) : 1
      };

      AddComponent(component);
    }

    private void AddComponent(Component component)
    {
      if (!Regex.IsMatch(component.Item, "^[0-9]{1,4}$"))
      {
        throw new Exception($"Invalid component item [{component}].");
      }

      if (!Regex.IsMatch(component.Material, "^[0-9]{1,12}$"))
      {
        throw new Exception($"Invalid component material [{component}].");
      }

      if (component.Quantity <= 0)
      {
        throw new Exception($"Invalid component quantity [{component}].");
      }

      Components[component.Item] = component;
    }
  }
}
