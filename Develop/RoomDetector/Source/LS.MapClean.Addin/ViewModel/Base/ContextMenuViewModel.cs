using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Input;
using GalaSoft.MvvmLight;

namespace LS.MapClean.Addin.ViewModel.Base
{
    public class ContextMenuViewModel : ViewModelBase
    {
        public ContextMenuViewModel(string header, ICommand command, bool isCheckable = false, bool isChecked = false, bool isToolTipShowOnDisabled = false, string toolTip = "")
        {
            Header = header;
            Command = command;
            IsCheckable = isCheckable;
            IsChecked = isChecked;
            ToolTip = toolTip;
            ToolTipShowOnDisabled = isToolTipShowOnDisabled;
            IsEnabled = false;
        }

        /// <summary>
        /// Add a submenu
        /// </summary>
        /// <param name="displayName"></param>
        /// <param name="command"></param>
        /// <param name="isCheckable"></param>
        /// <param name="isChecked"></param>
        /// <param name="toolTip"></param>
        public void Add(string displayName, ICommand command, bool isCheckable = false, bool isChecked = false, bool isToolTipShowOnDisabled = false, string toolTip = "")
        {
            m_subMenus.Add(new ContextMenuViewModel(displayName, command, isCheckable, isChecked, isToolTipShowOnDisabled, toolTip)
            {
                ParentMenu = this
            });
        }

        public string Header { get; set; }
        public ICommand Command { get; set; }
        public Icon Icon { get; set; }
        public bool IsCheckable { get; set; }
        public bool IsEnabled { get; set; }

        public bool HasToolTip
        {
            get { return !String.IsNullOrEmpty(ToolTip); }
        }

        private ObservableCollection<ContextMenuViewModel> m_subMenus = new ObservableCollection<ContextMenuViewModel>();

        public ObservableCollection<ContextMenuViewModel> SubMenus
        {
            get { return m_subMenus; }
        }

        /// <summary>
        /// Append a separator at the end of the menu
        /// </summary>
        /// <param name="beforeViewModel"></param>
        public void AppendSeparator()
        {
            //use a null object to represent a Separator
            //Reference from http://www.japf.fr/2008/12/how-insert-separator-in-a-databound-combobox/
            m_subMenus.Insert(m_subMenus.Count, null);
        }

        private bool m_bIsChecked;

        public bool IsChecked
        {
            get { return m_bIsChecked; }
            set
            {
                if (m_bIsChecked != value)
                {
                    if (null != ParentMenu)
                    {
                        if (value)
                        {
                            m_bIsChecked = value;
                            //Partly Fix defect FDS-3153 to check if ParentMenu is exist 
                            //Disable checked status of sibling menus
                            foreach (ContextMenuViewModel sibling in ParentMenu.SubMenus)
                            {
                                if (sibling != this)
                                    sibling.IsChecked = false;
                            }
                            base.RaisePropertyChanged(/*MSG0*/"IsChecked");
                        }
                        else
                        {
                            //check if others have check one, if no, then disable uncheck
                            bool bOthersHasCheck = false;
                            foreach (ContextMenuViewModel sibling in ParentMenu.SubMenus)
                            {
                                if ((sibling != this) && (sibling.IsChecked))
                                {
                                    bOthersHasCheck = true;
                                    break;
                                }
                            }
                            if (bOthersHasCheck)
                            {
                                m_bIsChecked = value;
                                base.RaisePropertyChanged(/*MSG0*/"IsChecked");
                            }
                            else
                            {
                                m_bIsChecked = true;
                            }
                        }
                    }
                    else
                    {
                        m_bIsChecked = value;
                        base.RaisePropertyChanged(/*MSG0*/"IsChecked");
                    }
                }
            }
        }

        public string ToolTip { get; set; }
        public bool ToolTipShowOnDisabled { get; set; }

        /// <summary>
        /// Hold on current menu's parent menu
        /// </summary>
        public ContextMenuViewModel ParentMenu { get; private set; }

        /// <summary>
        /// Override ToString() method to fix Automation test issues(From Donald Lu)
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Header;
        }
    }
}
