using GalaSoft.MvvmLight;

namespace LS.MapClean.Addin.SpreadPoints.ViewModel
{
    class SpreadPointSettingsViewModel : ViewModelBase
    {
        private bool _showPointOnly = true;
        public bool ShowPointOnly
        {
            get { return _showPointOnly; }
            set
            {
                _showPointOnly = value;
                if (_showPointOnly)
                {
                    ShowPointId = false;
                    ShowPointCode = false;
                }
                RaisePropertyChanged("ShowPointOnly");
            }
        }

        private bool _showPointId = false;
        public bool ShowPointId
        {
            get { return _showPointId; }
            set
            {
                _showPointId = value;
                if (_showPointId)
                {
                    ShowPointOnly = false;
                    ShowPointCode = false;
                }
                RaisePropertyChanged("ShowPointId");
            }
        }

        private bool _showPointCode = false;
        public bool ShowPointCode
        {
            get { return _showPointCode; }
            set
            {
                _showPointCode = value;
                if (_showPointCode)
                {
                    ShowPointOnly = false;
                    ShowPointId = false;
                }
                RaisePropertyChanged("ShowPointCode");
            }
        }
    }
}
