namespace desktop_cshar.Models;

public class GenerationPreset
{
    public string Name { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public int Steps { get; set; }
    public double GuidanceScale { get; set; }

    public string DisplayName => Name;
}