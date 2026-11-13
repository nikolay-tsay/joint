namespace Joint.Mobile.Features.Budget;

/// <summary>Gives each page of the Budget carousel its own layout.</summary>
public class BudgetViewTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Budget { get; set; }

    public DataTemplate? Goals { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
        (item is BudgetView { Key: BudgetView.Goals } ? Goals : Budget)!;
}
