using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using LS.MapClean.Addin.MapClean;
using LS.MapClean.Addin.ViewModel.Base;

namespace LS.MapClean.Addin.ViewModel
{
    public class MapCleanPanelViewModel : ViewModelBase
    {
        private readonly MapCleanService _service;

        #region Properties
        public CheckResultsViewModel CheckResultsVM { get; private set; }
        public ActionsSettingViewModel ActionsSettingVM { get; set; }

        private bool _showContinueButton = true;
        public bool ShowContinueButton
        {
            get { return _showContinueButton; }
            set
            {
                _showContinueButton = value;
                RaisePropertyChanged("ShowContinueButton");
            }
        }
        #endregion

        #region Constructors
        public MapCleanPanelViewModel(MapCleanService service)
        {
            _service = service;
            CheckResultsVM = new CheckResultsViewModel(service);
        }
        #endregion

        #region Commands
        public ICommand FixAllCommand
        {
            get { return new RelayCommand(DoFixAll, CanDoFixAll);}
        }

        private bool CanDoFixAll()
        {
            return true;
        }

        private void DoFixAll()
        {
            _service.FixAllCheckResults(recursiveCheck: false, verb:"修复", objectName: "拓扑错误");
        }

        public ICommand RecheckCommand
        {
            get { return new RelayCommand(DoRecheck, CanRecheck);}
        }

        private bool CanRecheck()
        {
            return true;
        }

        private void DoRecheck()
        {
            _service.Recheck();
        }

        public ICommand SettingsCommand
        {
            get { return new RelayCommand(DoSettings, CanSettings);}
        }

        private bool CanSettings()
        {
            return true;
        }

        private void DoSettings()
        {
            // TODO: Show up settings dialog.
        }

        public ICommand ExitCommand
        {
            get { return new RelayCommand(DoExit, CanExit);}
        }

        private bool CanExit()
        {
            return true;
        }

        private void DoExit()
        {
            _service.End();
        }

        public ICommand ClearTransientCommand
        {
            get { return new RelayCommand(DoClearTransient);}
        }

        private void DoClearTransient()
        {
            _service.ClearTransientGraphics();
        }
        #endregion
    }
}
