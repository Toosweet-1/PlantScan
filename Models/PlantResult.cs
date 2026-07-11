namespace PlantScan.Models;

public class PlantResult
{
    public string PlantName { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Description { get; set; } = string.Empty;
    public string CommonUses { get; set; } = string.Empty;
    public string Warnings { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
}
