using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Data;

namespace ComTransfer
{
    /// <summary>
    /// INotifyPropertyChanged implementation for one-way data binding
    /// </summary>
    public class CustomINotifyPropertyChanged : INotifyPropertyChanged
    {
        /// <summary>
        /// Property changed event handler
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Method that notifies the UI to update data
        /// </summary>
        /// <typeparam name="T">generic type parameter</typeparam>
        /// <param name="obj">anonymous object holding the property to update</param>
        protected void Notify<T>(T obj)
        {
            if (obj == null)
            {
                return;
            }
            foreach (PropertyInfo property in typeof(T).GetProperties())
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property.Name));
            }
        }

        /// <summary>
        /// Method that notifies the UI to update data
        /// </summary>
        /// <param name="name">name of the property to update</param>
        protected void Notify(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class IsMoreThanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                bool flag = (double)value > double.Parse(parameter.ToString());
                return flag;
            }
            catch
            {
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
