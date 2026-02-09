using System;
using System.Collections.Generic;
using System.Diagnostics;
using MigrationToolkit.Shared.Models;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

namespace MigrationToolkit.Spy
{
    /// <summary>
    /// Maps UWP visual tree elements to framework-agnostic AbstractControl models.
    /// This is the ONLY place where UWP types are referenced for mapping purposes.
    /// </summary>
    public class UWPMapper
    {
        /// <summary>
        /// Map a DependencyObject and its children to an AbstractControl tree.
        /// Returns null if depth exceeded or element is not a UIElement.
        /// </summary>
        public AbstractControl? Map(DependencyObject element, int currentDepth, int maxDepth)
        {
            if (currentDepth > maxDepth)
            {
                Debug.WriteLine($"UWPMapper: depth limit {maxDepth} hit at {element.GetType().Name}");
                return null;
            }
            if (element is not UIElement uiElement) return null;

            var fe = element as FrameworkElement;
            var automationId = fe != null ? AutomationProperties.GetAutomationId(fe) : null;
            var name = fe?.Name;

            var id = !string.IsNullOrEmpty(automationId) ? automationId
                   : !string.IsNullOrEmpty(name) ? name
                   : null;

            if (currentDepth <= 3 || id != null)
            {
                Debug.WriteLine($"UWPMapper: [{currentDepth}] {element.GetType().Name} id={id ?? "(none)"}");
            }

            var kind = MapKind(element);
            var state = CaptureState(element, kind);
            var visual = CaptureVisual(fe);
            var label = GetLabel(element);

            // Build children list first to support R-SPY-07 skip logic
            var children = new List<AbstractControl>();
            var childCount = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                var mapped = Map(child, currentDepth + 1, maxDepth);
                if (mapped != null)
                    children.Add(mapped);
            }

            // R-SPY-07: Skip controls with no AutomationId AND no Name,
            // BUT still include their children (they bubble up).
            // Exception: always include containers that have named descendants.
            if (id == null && children.Count == 0)
                return null;

            // If unnamed but has children, include as a structural container
            // with a synthetic id based on type name
            var control = new AbstractControl
            {
                Id = id ?? $"_{element.GetType().Name}",
                Kind = kind,
                NativeType = element.GetType().Name,
                Label = label,
                State = state,
                Visual = visual,
                Children = children
            };

            return control;
        }

        private string MapKind(DependencyObject element)
        {
            return element switch
            {
                // TextInput
                TextBox => "TextInput",
                PasswordBox => "TextInput",
                RichEditBox => "TextInput",
                AutoSuggestBox => "TextInput",

                // TextDisplay
                TextBlock => "TextDisplay",
                RichTextBlock => "TextDisplay",

                // ActionButton
                AppBarButton => "ActionButton",
                HyperlinkButton => "ActionButton",
                RepeatButton => "ActionButton",
                Button => "ActionButton",

                // Toggle
                ToggleSwitch => "Toggle",
                CheckBox => "Toggle",
                RadioButton => "Toggle",
                ToggleButton => "Toggle",

                // Selector
                ComboBox => "Selector",
                ListBox => "Selector",
                DatePicker => "Selector",
                TimePicker => "Selector",

                // RangeInput
                Slider => "RangeInput",

                // Image
                Image => "Image",

                // List
                ListView => "List",
                GridView => "List",

                // LoadingIndicator
                ProgressRing => "LoadingIndicator",

                // ProgressIndicator
                ProgressBar => "ProgressIndicator",

                // Screen
                Page => "Screen",

                // Navigation
                Frame => "Navigation",
                NavigationView => "Navigation",

                // TabGroup
                Pivot => "TabGroup",

                // Container — check after more specific types
                Canvas => "Container",
                RelativePanel => "Container",
                VariableSizedWrapGrid => "Container",
                StackPanel => "Container",
                Grid => "Container",
                Border => "Container",
                ScrollViewer => "Container",
                Panel => "Container",

                // Unknown — preserve type name via NativeType
                _ => "Unknown"
            };
        }

        private ControlState CaptureState(DependencyObject element, string kind)
        {
            var state = new ControlState();

            switch (element)
            {
                // TextInput
                case TextBox tb:
                    state.Value = tb.Text;
                    state.Placeholder = tb.PlaceholderText;
                    state.Enabled = tb.IsEnabled;
                    state.Visible = tb.Visibility == Visibility.Visible;
                    state.ReadOnly = tb.IsReadOnly;
                    state.Interactive = tb.IsEnabled && !tb.IsReadOnly;
                    break;

                case PasswordBox pb:
                    state.Value = "***"; // Don't expose actual password
                    state.Placeholder = pb.PlaceholderText;
                    state.Enabled = pb.IsEnabled;
                    state.Visible = pb.Visibility == Visibility.Visible;
                    state.Interactive = pb.IsEnabled;
                    break;

                case RichEditBox reb:
                    reb.Document.GetText(Windows.UI.Text.TextGetOptions.None, out string rebText);
                    state.Value = rebText?.Trim();
                    state.Enabled = reb.IsEnabled;
                    state.Visible = reb.Visibility == Visibility.Visible;
                    state.ReadOnly = reb.IsReadOnly;
                    state.Interactive = reb.IsEnabled && !reb.IsReadOnly;
                    break;

                case AutoSuggestBox asb:
                    state.Value = asb.Text;
                    state.Placeholder = asb.PlaceholderText;
                    state.Enabled = asb.IsEnabled;
                    state.Visible = asb.Visibility == Visibility.Visible;
                    state.Interactive = asb.IsEnabled;
                    break;

                // TextDisplay
                case TextBlock txtBlk:
                    state.Value = txtBlk.Text;
                    state.Visible = txtBlk.Visibility == Visibility.Visible;
                    break;

                case RichTextBlock rtb:
                    state.Visible = rtb.Visibility == Visibility.Visible;
                    break;

                // ActionButton — AppBarButton before Button (AppBarButton inherits from Button)
                case AppBarButton abBtn:
                    state.Enabled = abBtn.IsEnabled;
                    state.Visible = abBtn.Visibility == Visibility.Visible;
                    state.Interactive = abBtn.IsEnabled;
                    break;

                case HyperlinkButton hlBtn:
                    state.Enabled = hlBtn.IsEnabled;
                    state.Visible = hlBtn.Visibility == Visibility.Visible;
                    state.Interactive = hlBtn.IsEnabled;
                    break;

                case Button btn:
                    state.Enabled = btn.IsEnabled;
                    state.Visible = btn.Visibility == Visibility.Visible;
                    // R-MAP-08: If Command bound, Interactive reflects CanExecute
                    if (btn.Command != null)
                    {
                        try { state.Interactive = btn.Command.CanExecute(btn.CommandParameter); }
                        catch { state.Interactive = btn.IsEnabled; }
                    }
                    else
                    {
                        state.Interactive = btn.IsEnabled;
                    }
                    break;

                // Toggle — CheckBox/RadioButton before ToggleButton (they inherit from it)
                case ToggleSwitch ts:
                    state.Checked = ts.IsOn;
                    state.Enabled = ts.IsEnabled;
                    state.Visible = ts.Visibility == Visibility.Visible;
                    break;

                case CheckBox cb:
                    state.Checked = cb.IsChecked;
                    state.Enabled = cb.IsEnabled;
                    state.Visible = cb.Visibility == Visibility.Visible;
                    break;

                case RadioButton rb:
                    state.Checked = rb.IsChecked;
                    state.Enabled = rb.IsEnabled;
                    state.Visible = rb.Visibility == Visibility.Visible;
                    break;

                case ToggleButton togBtn:
                    state.Checked = togBtn.IsChecked;
                    state.Enabled = togBtn.IsEnabled;
                    state.Visible = togBtn.Visibility == Visibility.Visible;
                    break;

                // Selector
                case ComboBox combo:
                    state.SelectedIndex = combo.SelectedIndex;
                    state.ItemCount = combo.Items?.Count;
                    state.Value = combo.SelectedItem?.ToString();
                    state.Enabled = combo.IsEnabled;
                    state.Visible = combo.Visibility == Visibility.Visible;
                    break;

                case ListBox lb:
                    state.SelectedIndex = lb.SelectedIndex;
                    state.ItemCount = lb.Items?.Count;
                    state.Value = lb.SelectedItem?.ToString();
                    state.Enabled = lb.IsEnabled;
                    state.Visible = lb.Visibility == Visibility.Visible;
                    break;

                case DatePicker dp:
                    state.Value = dp.Date.ToString("yyyy-MM-dd");
                    state.Enabled = dp.IsEnabled;
                    state.Visible = dp.Visibility == Visibility.Visible;
                    break;

                case TimePicker tp:
                    state.Value = tp.Time.ToString();
                    state.Enabled = tp.IsEnabled;
                    state.Visible = tp.Visibility == Visibility.Visible;
                    break;

                // RangeInput
                case Slider slider:
                    state.Value = slider.Value.ToString();
                    state.Enabled = slider.IsEnabled;
                    state.Visible = slider.Visibility == Visibility.Visible;
                    break;

                // List
                case ListView lv:
                    state.ItemCount = lv.Items?.Count;
                    state.Visible = lv.Visibility == Visibility.Visible;
                    break;

                case GridView gv:
                    state.ItemCount = gv.Items?.Count;
                    state.Visible = gv.Visibility == Visibility.Visible;
                    break;

                // LoadingIndicator
                case ProgressRing ring:
                    state.Visible = ring.IsActive;
                    state.Opacity = ring.Opacity;
                    break;

                // ProgressIndicator
                case ProgressBar bar:
                    state.Value = bar.Value.ToString();
                    state.Visible = bar.Visibility == Visibility.Visible;
                    break;

                // For containers/screens/navigation — capture basic visibility
                default:
                    if (element is FrameworkElement fe)
                    {
                        state.Visible = fe.Visibility == Visibility.Visible;
                    }
                    break;
            }

            return state;
        }

        private ControlVisual CaptureVisual(FrameworkElement? element)
        {
            var visual = new ControlVisual();
            if (element == null) return visual;

            try
            {
                // R-MAP-06: Position relative to window via TransformToVisual(null)
                var transform = element.TransformToVisual(null);
                var point = transform.TransformPoint(new Point(0, 0));
                visual.X = Sanitize(point.X);
                visual.Y = Sanitize(point.Y);
            }
            catch
            {
                // TransformToVisual fails for collapsed/unloaded elements
                visual.X = 0;
                visual.Y = 0;
            }

            visual.Width = Sanitize(element.ActualWidth);
            visual.Height = Sanitize(element.ActualHeight);
            visual.Opacity = element.Opacity;

            // Extract font and color info from Control or TextBlock
            switch (element)
            {
                case Control ctrl:
                    visual.FontSize = Sanitize(ctrl.FontSize);
                    visual.FontWeight = ctrl.FontWeight.Weight.ToString();
                    visual.Foreground = BrushToString(ctrl.Foreground);
                    visual.Background = BrushToString(ctrl.Background);
                    break;

                case TextBlock tb:
                    visual.FontSize = Sanitize(tb.FontSize);
                    visual.FontWeight = tb.FontWeight.Weight.ToString();
                    visual.Foreground = BrushToString(tb.Foreground);
                    break;
            }

            return visual;
        }

        /// <summary>
        /// Replace NaN/Infinity with 0 — these are invalid JSON and crash Newtonsoft.Json.
        /// </summary>
        private static double Sanitize(double value)
            => double.IsNaN(value) || double.IsInfinity(value) ? 0 : value;

        private string? GetLabel(DependencyObject element)
        {
            // AppBarButton before Button (AppBarButton inherits from Button)
            return element switch
            {
                AppBarButton abBtn => abBtn.Label,
                HyperlinkButton hlBtn => hlBtn.Content?.ToString(),
                Button btn => btn.Content?.ToString(),
                TextBlock tb => tb.Text,
                ToggleSwitch ts => ts.Header?.ToString(),
                CheckBox cb => cb.Content?.ToString(),
                RadioButton rb => rb.Content?.ToString(),
                ToggleButton togBtn => togBtn.Content?.ToString(),
                _ => null
            };
        }

        private static string? BrushToString(Brush? brush)
        {
            if (brush is SolidColorBrush scb)
                return scb.Color.ToString();
            return brush?.ToString();
        }
    }
}
