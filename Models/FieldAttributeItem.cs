using System.ComponentModel;

namespace HK_AREA_SEARCH.Models
{
    /// <summary>
    /// ÒªËØ×Ö¶ÎÊôÐÔÏî
    /// </summary>
    public class FieldAttributeItem : INotifyPropertyChanged
    {
        private string _fieldName;
        public string FieldName
        {
            get { return _fieldName; }
            set
            {
                _fieldName = value;
                OnPropertyChanged(nameof(FieldName));
            }
        }

        private string _fieldValue;
        public string FieldValue
        {
            get { return _fieldValue; }
            set
            {
                _fieldValue = value;
                OnPropertyChanged(nameof(FieldValue));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}