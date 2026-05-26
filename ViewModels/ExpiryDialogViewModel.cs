using CommunityToolkit.Mvvm.ComponentModel;

namespace Kapture.ViewModels;

public partial class ExpiryDialogViewModel : ViewModelBase
{
    [ObservableProperty] private TimeSpan? _selectedDuration;

    public record ExpiryOption(string Label, TimeSpan? Duration);

    public List<ExpiryOption> Options { get; } =
    [
        new("1 Hour", TimeSpan.FromHours(1)),
        new("6 Hours", TimeSpan.FromHours(6)),
        new("1 Day", TimeSpan.FromDays(1)),
        new("3 Days", TimeSpan.FromDays(3)),
        new("7 Days", TimeSpan.FromDays(7)),
        new("30 Days", TimeSpan.FromDays(30)),
        new("Never", null),
    ];

    [ObservableProperty] private ExpiryOption? _selectedOption;
}
