using System;
using System.Threading.Tasks;
using MigrationToolkit.Shared.Models;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

namespace MigrationToolkit.Spy
{
    /// <summary>
    /// Executes UI actions (click, type, toggle, select, clear) on UWP controls.
    /// All actions are dispatched to the UI thread via CoreDispatcher.
    /// </summary>
    public class ActionExecutor
    {
        private readonly CoreDispatcher _dispatcher;

        public ActionExecutor(CoreDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        /// <summary>
        /// Find a control by AutomationId first, then Name. Throws if not found.
        /// </summary>
        public async Task<FrameworkElement> FindControlAsync(string id)
        {
            FrameworkElement? result = null;

            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var root = Window.Current?.Content;
                if (root == null) return;

                result = FindByAutomationId(root, id) ?? FindByName(root, id);
            });

            if (result == null)
                throw new InvalidOperationException(
                    $"Control with AutomationId or Name '{id}' not found in visual tree. " +
                    "Ensure the control exists and the app is on the correct screen.");

            return result;
        }

        /// <summary>
        /// Execute an action on the given control. Must be called from a background thread;
        /// dispatches to UI thread internally.
        /// </summary>
        public async Task ExecuteAsync(FrameworkElement control, ActionCommand command)
        {
            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                switch (command.Action.ToLowerInvariant())
                {
                    case "click":
                        ExecuteClick(control);
                        break;
                    case "type":
                        ExecuteType(control, command.Value ?? string.Empty);
                        break;
                    case "toggle":
                        ExecuteToggle(control);
                        break;
                    case "select":
                        ExecuteSelect(control, command.Value ?? string.Empty);
                        break;
                    case "clear":
                        ExecuteClear(control);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unknown action '{command.Action}'. Supported: click, type, toggle, select, clear.");
                }
            });
        }

        private void ExecuteClick(FrameworkElement control)
        {
            // R-ACT-02: Prefer Command.Execute if command is bound
            if (control is ButtonBase btn && btn.Command != null)
            {
                btn.Command.Execute(btn.CommandParameter);
                return;
            }

            // Fallback: Use AutomationPeer to invoke
            if (control is Button button)
            {
                var peer = FrameworkElementAutomationPeer.CreatePeerForElement(button);
                if (peer?.GetPattern(PatternInterface.Invoke) is IInvokeProvider invoker)
                {
                    invoker.Invoke();
                    return;
                }
            }

            if (control is HyperlinkButton hlBtn)
            {
                var peer = FrameworkElementAutomationPeer.CreatePeerForElement(hlBtn);
                if (peer?.GetPattern(PatternInterface.Invoke) is IInvokeProvider invoker)
                {
                    invoker.Invoke();
                    return;
                }
            }

            if (control is AppBarButton abBtn)
            {
                var peer = FrameworkElementAutomationPeer.CreatePeerForElement(abBtn);
                if (peer?.GetPattern(PatternInterface.Invoke) is IInvokeProvider invoker)
                {
                    invoker.Invoke();
                    return;
                }
            }

            throw new InvalidOperationException(
                $"Cannot click control '{control.Name ?? control.GetType().Name}': " +
                "not a recognized button type or has no invoke capability.");
        }

        private void ExecuteType(FrameworkElement control, string value)
        {
            switch (control)
            {
                case TextBox tb:
                    tb.Text = value;
                    break;
                // R-ACT-03: PasswordBox uses Password property
                case PasswordBox pb:
                    pb.Password = value;
                    break;
                case AutoSuggestBox asb:
                    asb.Text = value;
                    break;
                case RichEditBox reb:
                    reb.Document.SetText(Windows.UI.Text.TextSetOptions.None, value);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Cannot type into control '{control.Name ?? control.GetType().Name}': " +
                        "not a text input control (TextBox, PasswordBox, AutoSuggestBox, RichEditBox).");
            }
        }

        private void ExecuteToggle(FrameworkElement control)
        {
            switch (control)
            {
                case ToggleSwitch ts:
                    ts.IsOn = !ts.IsOn;
                    break;
                case CheckBox cb:
                    cb.IsChecked = !(cb.IsChecked ?? false);
                    break;
                case RadioButton rb:
                    rb.IsChecked = true;
                    break;
                case ToggleButton togBtn:
                    togBtn.IsChecked = !(togBtn.IsChecked ?? false);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Cannot toggle control '{control.Name ?? control.GetType().Name}': " +
                        "not a toggle control (ToggleSwitch, CheckBox, RadioButton, ToggleButton).");
            }
        }

        private void ExecuteSelect(FrameworkElement control, string value)
        {
            switch (control)
            {
                case ComboBox combo:
                    SelectInItemsControl(combo, value);
                    break;
                case ListBox lb:
                    SelectInItemsControl(lb, value);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Cannot select in control '{control.Name ?? control.GetType().Name}': " +
                        "not a selector control (ComboBox, ListBox).");
            }
        }

        private void SelectInItemsControl(Selector selector, string value)
        {
            // Try as numeric index first
            if (int.TryParse(value, out int index))
            {
                selector.SelectedIndex = index;
                return;
            }

            // Find item by text match
            for (int i = 0; i < selector.Items.Count; i++)
            {
                if (selector.Items[i]?.ToString() == value)
                {
                    selector.SelectedIndex = i;
                    return;
                }
            }

            throw new InvalidOperationException(
                $"Could not find item '{value}' in selector '{(selector as FrameworkElement)?.Name}'.");
        }

        private void ExecuteClear(FrameworkElement control)
        {
            switch (control)
            {
                case TextBox tb:
                    tb.Text = string.Empty;
                    break;
                case PasswordBox pb:
                    pb.Password = string.Empty;
                    break;
                case AutoSuggestBox asb:
                    asb.Text = string.Empty;
                    break;
                case RichEditBox reb:
                    reb.Document.SetText(Windows.UI.Text.TextSetOptions.None, string.Empty);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Cannot clear control '{control.Name ?? control.GetType().Name}': " +
                        "not a text input control.");
            }
        }

        #region Visual Tree Search

        private FrameworkElement? FindByAutomationId(DependencyObject root, string id)
        {
            return FindInTree(root, element =>
            {
                if (element is FrameworkElement fe)
                {
                    var autoId = AutomationProperties.GetAutomationId(fe);
                    return autoId == id;
                }
                return false;
            });
        }

        private FrameworkElement? FindByName(DependencyObject root, string name)
        {
            return FindInTree(root, element =>
                element is FrameworkElement fe && fe.Name == name);
        }

        private FrameworkElement? FindInTree(DependencyObject root, Func<DependencyObject, bool> predicate)
        {
            if (predicate(root) && root is FrameworkElement match)
                return match;

            var count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var found = FindInTree(child, predicate);
                if (found != null) return found;
            }

            return null;
        }

        #endregion
    }
}
