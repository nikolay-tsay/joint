namespace Joint.Mobile.Features.Insights;

/// <summary>Gives each page of the Insights carousel its own layout.</summary>
public class InsightsViewTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Overview { get; set; }

    public DataTemplate? Recurring { get; set; }

    public DataTemplate? Categories { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
        (item switch
        {
            InsightsView { Key: InsightsView.Recurring } => Recurring,
            InsightsView { Key: InsightsView.Categories } => Categories,
            _ => Overview,
        })!;
}
