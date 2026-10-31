using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SimRacingHub.Application.Features.Shell;
using SimRacingHub.Application.Localization;

namespace SimRacingHub.Avalonia.Onboarding;

/// <summary>A read-only tour of the actual pages. Illustrations never alter game or runtime state.</summary>
public partial class GettingStartedOverlay : UserControl
{
    public sealed record Step(PageId Page, string Target, string Key);
    public static IReadOnlyList<Step> Steps { get; } = Array.AsReadOnly(new[]
    {
        new Step(PageId.Dashboard, "TourDashboard", "Dashboard"),
        new Step(PageId.Profiles, "TourProfiles", "Profiles"),
        new Step(PageId.Steering, "TourSteering", "Axes"),
        new Step(PageId.KeyBindings, "TourKeys", "Keys"),
        new Step(PageId.ControlAssignment, "ControlAssignmentPage", "Assignment"),
        new Step(PageId.InputDevices, "InputDevicesPage", "Input"),
        new Step(PageId.Output, "TourOutput", "Output"),
        new Step(PageId.Settings, "TourSettings", "Settings"),
        new Step(PageId.Dashboard, "TourDashboard", "Ready")
    });

    private MainWindow? _owner;
    private AppShellViewModel? _shell;
    private TaskCompletionSource? _completion;
    private PageId _originalPage;
    private int _index = -1;
    private bool _changingLanguage;
    private bool _compact;
    public int StepIndex => _index;
    public bool IsOpen => _completion != null;

    public GettingStartedOverlay()
    {
        InitializeComponent();
        NextStep.Click += (_, _) => Advance();
        PreviousStep.Click += (_, _) => { if (_index >= 0) { _index--; ShowStep(); } };
        SkipGuide.Click += (_, _) => Finish();
        ShowPageButton.Click += (_, _) => { _compact = !_compact; UpdateCompactMode(); };
        LanguageBox.SelectionChanged += (_, _) =>
        {
            if (_changingLanguage || _shell?.AppSettings == null || LanguageBox.SelectedItem is not LanguageInfo language) return;
            _shell.AppSettings.SelectedLanguageInfo = language;
            ShowStep();
        };
        LayoutUpdated += (_, _) => PositionSpotlight();
    }

    public Task OpenAsync(MainWindow owner, AppShellViewModel shell)
    {
        if (_completion != null) return _completion.Task;
        _owner = owner; _shell = shell; _originalPage = shell.CurrentPageId;
        _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _index = -1;
        IsVisible = true;
        ShowStep();
        Dispatcher.UIThread.Post(() => LanguageBox.Focus());
        return _completion.Task;
    }

    public void Advance()
    {
        if (!IsOpen) return;
        if (_index == Steps.Count - 1) { Finish(); return; }
        _index++;
        ShowStep();
    }

    public void Finish(bool remember = true)
    {
        if (_completion == null) return;
        var completion = _completion; _completion = null;
        IsVisible = false;
        _shell?.NavigateTo(_originalPage);
        if (remember) _shell?.AppSettings?.DismissWelcome();
        _owner = null; _shell = null;
        completion.TrySetResult();
    }

    private string Text(string key) => _shell?.LocalizationManager.GetString("Tour_" + key) ?? key;
    private void ShowStep()
    {
        if (_shell == null) return;
        bool welcome = _index < 0;
        _compact = false;
        var key = welcome ? "Welcome" : Steps[_index].Key;
        if (!welcome) _shell.NavigateTo(Steps[_index].Page);
        LanguagePanel.IsVisible = welcome;
        _changingLanguage = true;
        try
        {
            LanguageBox.ItemsSource = _shell.AppSettings?.AvailableLanguages;
            LanguageBox.SelectedItem = _shell.AppSettings?.SelectedLanguageInfo;
        }
        finally { _changingLanguage = false; }
        ProgressText.Text = welcome ? "vWheel Hub" : string.Format(Text("Progress"), _index + 1, Steps.Count);
        GuideTitle.Text = Text(key + "Title");
        GuideBody.Text = Text(key + "Body");
        GuidePath.Text = welcome ? Text("WelcomePath") : Text(key + "Path");
        GuideTip.Text = key == "Output" ? Text(OperatingSystem.IsWindows() ? "OutputWindows" : "OutputLinux") : Text(key + "Tip");
        ExampleCard.IsVisible = key is "Prepare" or "Menu";
        ExampleText.Text = ExampleCard.IsVisible ? Text(key + "Example") : "";
        PreviousStep.IsVisible = !welcome;
        ShowPageButton.IsVisible = !welcome;
        UpdateCompactMode();
        NextStep.Content = Text(welcome ? "Start" : _index == Steps.Count - 1 ? "Finish" : "Next");
        SkipGuide.Content = Text(welcome ? "Later" : "Skip");
        GuideCard.VerticalAlignment = welcome ? global::Avalonia.Layout.VerticalAlignment.Center : global::Avalonia.Layout.VerticalAlignment.Bottom;
        GuideScroll.Offset = default;
        if (!welcome) Dispatcher.UIThread.Post(() => NextStep.Focus());
        PositionSpotlight();
    }

    private void UpdateCompactMode()
    {
        GuideScroll.IsVisible = !_compact;
        GuideCard.Padding = new Thickness(_compact ? 12 : 24);
        ShowPageButton.Content = Text(_compact ? "ShowExplanation" : "ShowPage");
    }

    private void PositionSpotlight()
    {
        if (!IsOpen || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var width = Bounds.Width; var height = Bounds.Height;
        Rect hole = default;
        if (_index >= 0 && _owner?.FindControl<Control>(Steps[_index].Target) is { } target && target.IsEffectivelyVisible &&
            target.TranslatePoint(default, this) is { } point && GuideCard.TranslatePoint(default, this) is { } card)
        {
            // Keep the page header and upper controls visible, never under the guide card.
            var top = Math.Max(38, point.Y - 5);
            if (_shell?.IsControlSection == true && Steps[_index].Target != "ProfileHeader" &&
                _owner.FindControl<Control>("ControlSectionNavigation")?.TranslatePoint(default, this) is { } tabs)
                top = Math.Max(38, Math.Min(top, tabs.Y - 5));
            var left = Math.Max(6, point.X - 5);
            var right = Math.Min(width - 6, point.X + target.Bounds.Width + 5);
            var bottom = Math.Min(card.Y - 12, point.Y + target.Bounds.Height + 5);
            if (right > left && bottom > top) hole = new Rect(left, top, right - left, bottom - top);
        }
        Spotlight.IsVisible = hole.Width > 0;
        Place(Spotlight, hole.X, hole.Y, hole.Width, hole.Height);
        Place(ShadeTop, 0, 0, width, hole.Width > 0 ? hole.Top : height);
        Place(ShadeBottom, 0, hole.Bottom, width, hole.Width > 0 ? height - hole.Bottom : 0);
        Place(ShadeLeft, 0, hole.Top, hole.Left, hole.Height);
        Place(ShadeRight, hole.Right, hole.Top, width - hole.Right, hole.Height);
    }

    private static void Place(Control control, double x, double y, double width, double height)
    {
        Canvas.SetLeft(control, x); Canvas.SetTop(control, y);
        control.Width = Math.Max(0, width); control.Height = Math.Max(0, height);
    }
}
