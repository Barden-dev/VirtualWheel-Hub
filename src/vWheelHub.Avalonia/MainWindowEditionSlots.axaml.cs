using System.Linq;
using Avalonia.Controls;
using SimRacingHub.Avalonia.Controls;

namespace SimRacingHub.Avalonia;

public partial class MainWindowEditionSlots : UserControl
{
    public MainWindowEditionSlots()
    {
        InitializeComponent();
    }

    internal void AttachTo(MainWindow window)
    {
        EditionSlotNames.Attach(window, this);

        var topNotices = TopNoticeHost.Children.ToArray();
        var controlTabs = ControlTabHost.Children.ToArray();
        var appTabs = ApplicationTabHost.Children.ToArray();
        var pages = PagesHost.Children.ToArray();
        var footerRight = FooterRightHost.Children.ToArray();

        TopNoticeHost.Children.Clear();
        ControlTabHost.Children.Clear();
        ApplicationTabHost.Children.Clear();
        PagesHost.Children.Clear();
        FooterRightHost.Children.Clear();

        foreach (var notice in topNotices)
            window.TopNoticeExtraSlot.Children.Add(notice);

        int controlIndex = 4;
        foreach (var tab in controlTabs)
            window.ControlSectionNavigation.Children.Insert(controlIndex++, tab);

        int appIndex = 1;
        foreach (var tab in appTabs)
            window.ApplicationSectionNavigation.Children.Insert(appIndex++, tab);

        foreach (var page in pages)
            window.PagesHost.Children.Add(page);

        foreach (var item in footerRight)
        {
            Grid.SetColumn(item, 1);
            window.FooterGrid.Children.Add(item);
        }
    }
}
