using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using MindAttic.Orchestrator.Tickets;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// <c>local:TicketText.Text="{Binding QueueMessage}"</c> on a TextBlock: the text, with every ticket id in it a link that opens the
/// ticket in Tickets Desktop (M.ORCH-T762: every ticket id the Orchestrator shows links there, not only the ones in an id column).
/// The links take the window's <c>TicketLink</c> style, so they read and act like every other ticket link (OpenInTicketsCommand).
/// </summary>
public static class TicketText
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(TicketText), new PropertyMetadata(null, OnTextChanged));

    public static string? GetText(DependencyObject o) => (string?)o.GetValue(TextProperty);
    public static void SetText(DependencyObject o, string? value) => o.SetValue(TextProperty, value);

    private static void OnTextChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        if (o is not TextBlock block) return;
        block.Inlines.Clear();
        foreach (var part in TicketsDesktop.Split(e.NewValue as string))
        {
            if (part.TicketId is not { } id) { block.Inlines.Add(new Run(part.Text)); continue; }
            var link = new Hyperlink(new Run(part.Text)) { CommandParameter = id };
            // Looked up once it's in the window, which holds the style: the text can be set before the block is.
            link.SetResourceReference(FrameworkContentElement.StyleProperty, "TicketLink");
            System.Windows.Automation.AutomationProperties.SetName(link, $"{id}: open it in Tickets Desktop");
            block.Inlines.Add(link);
        }
    }
}
