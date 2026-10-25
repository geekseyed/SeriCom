using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ComTransfer
{
    /// <summary>
    /// Scheduled task edit window
    /// </summary>
    public partial class TaskWindow : Window
    {
        /// <summary>
        /// Scheduled task object list
        /// </summary>
        public ObservableCollection<TaskNode> TaskList { get; set; }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="window">owner window</param>
        public TaskWindow(Window window)
        {
            Owner = window;

            DataContext = this;

            TaskList = new ObservableCollection<TaskNode>(TaskManager.Instance.TaskList.Select(item => item.Clone()));

            InitializeComponent();
        }

        /// <summary>
        /// Window closing event
        /// </summary>
        /// <param name="sender">event sender</param>
        /// <param name="e">event arguments</param>
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            FocusManager.SetFocusedElement(this, this);

            TaskManager.SaveData(TaskList.ToList());
            TaskManager.SaveFailFileData(TaskManager.Instance.FailFileList);
        }

        /// <summary>
        /// Add button click event
        /// </summary>
        /// <param name="sender">event sender</param>
        /// <param name="e">event arguments</param>
        private void Button_Add_Click(object sender, RoutedEventArgs e)
        {
            TaskList.Add(new TaskNode());
        }

        /// <summary>
        /// Delete button click event
        /// </summary>
        /// <param name="sender">event sender</param>
        /// <param name="e">event arguments</param>
        private void Button_Delete_Click(object sender, RoutedEventArgs e)
        {
            TaskList.Remove(ListBox_Task.SelectedItem as TaskNode);
        }
    }
}
