namespace Sr5000Optics
{
  public class AreaResult
  {
    public Config.AreaConfig Area { get; set; }

    public Input.Component Component { get; set; }

    public string ReadCode { get; set; } = string.Empty;

    public AreaResult(Config.AreaConfig area, Input.Component component)
    {
      Area = area;
      Component = component;
    }

    public bool IsFulfilled()
    {
      return !string.IsNullOrEmpty(ReadCode);
    }

    public bool IsValid()
    {
      return !string.IsNullOrEmpty(ReadCode) && ReadCode.Contains(Component.Material);
    }
  }
}
