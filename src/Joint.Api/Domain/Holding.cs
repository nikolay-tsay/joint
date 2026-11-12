namespace Joint.Api.Domain;

public class Holding
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public required string Ticker { get; set; }
    public required string Name { get; set; }
    public decimal Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public required string Currency { get; set; }
}
