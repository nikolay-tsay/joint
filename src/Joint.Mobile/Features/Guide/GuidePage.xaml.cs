namespace Joint.Mobile.Features.Guide;

/// <summary>The guide to the app's figures, opened from Profile. Plain text; it loads nothing.</summary>
public partial class GuidePage : ContentPage
{
    public GuidePage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    public IReadOnlyList<GuideSection> Sections => GuideContent.Sections;

    private async void OnBackTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
