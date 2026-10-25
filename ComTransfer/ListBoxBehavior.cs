using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ComTransfer
{
    /// <summary>
    /// ListBox behavior that automatically scrolls to the bottom when items are added
    /// </summary>
    public class ListBoxBehavior
    {
        /// <summary>
        /// Attached property that enables or disables this behavior
        /// </summary>
        public bool ScrollOnNewItem { get; set; }

        /// <summary>
        /// Get the attached property value
        /// </summary>
        /// <param name="obj">control</param>
        /// <returns>property value</returns>
        public static bool GetScrollOnNewItem(DependencyObject obj)
        {
            return (bool)obj.GetValue(ScrollOnNewItemProperty);
        }

        /// <summary>
        /// Set the attached property value
        /// </summary>
        /// <param name="obj">control</param>
        /// <param name="value">property value</param>
        public static void SetScrollOnNewItem(DependencyObject obj, bool value)
        {
            obj.SetValue(ScrollOnNewItemProperty, value);
        }

        /// <summary>
        /// Attached property <c>ScrollOnNewItem</c>: scroll to the bottom when a new item is added
        /// </summary>
        public static readonly DependencyProperty ScrollOnNewItemProperty =
            DependencyProperty.RegisterAttached(
                "ScrollOnNewItem",
                typeof(bool),
                typeof(ListBoxBehavior),
                new UIPropertyMetadata(false, OnScrollOnNewItemChanged));

        /// <summary>
        /// Attached property change callback
        /// </summary>
        /// <param name="sender">control</param>
        /// <param name="e">event arguments</param>
        public static void OnScrollOnNewItemChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender == null)
            {
                return;
            }
            bool newValue = (bool)e.NewValue;
            if (newValue)
            {
                ((ListBox)sender).Loaded += ListBox_Loaded;
            }
        }

        /// <summary>
        /// ListBox loaded event handler
        /// </summary>
        /// <param name="sender">event sender</param>
        /// <param name="e">event arguments</param>
        private static void ListBox_Loaded(object sender, RoutedEventArgs e)
        {
            new ListBoxScroll(sender as ListBox);
        }

        /// <summary>
        /// Private helper that wires up ListBox events
        /// </summary>
        private class ListBoxScroll
        {
            private readonly ListBox listBox;
            private ScrollViewer scrollViewer;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="listBox">ListBox instance</param>
            public ListBoxScroll(ListBox listBox)
            {
                if (listBox != null && listBox.ItemsSource != null)
                {
                    this.listBox = listBox;
                    if (listBox.ItemsSource is INotifyCollectionChanged)
                    {
                        ((INotifyCollectionChanged)listBox.ItemsSource).CollectionChanged += CollectionChanged;
                    }
                }
            }

            /// <summary>
            /// Scroll the ListBox to the bottom when a new item is added
            /// </summary>
            /// <param name="sender">event sender</param>
            /// <param name="e">event arguments</param>
            private void CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
            {
                if (e.Action == NotifyCollectionChangedAction.Add && !listBox.IsMouseOver)
                {
                    if (scrollViewer == null)
                    {
                        scrollViewer = GetDescendantByType(listBox, typeof(ScrollViewer)) as ScrollViewer;
                    }
                    scrollViewer?.ScrollToBottom();
                }
            }

            private static Visual GetDescendantByType(Visual element, Type type)
            {
                if (element == null)
                {
                    return null;
                }
                if (element.GetType() == type)
                {
                    return element;
                }
                Visual foundElement = null;
                if (element is FrameworkElement)
                {
                    (element as FrameworkElement).ApplyTemplate();
                }
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
                {
                    Visual visual = VisualTreeHelper.GetChild(element, i) as Visual;
                    foundElement = GetDescendantByType(visual, type);
                    if (foundElement != null)
                    {
                        break;
                    }
                }
                return foundElement;
            }
        }
    }
}