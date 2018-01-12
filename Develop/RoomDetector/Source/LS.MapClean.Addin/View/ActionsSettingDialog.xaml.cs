using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Internal;
using LS.MapClean.Addin.MapClean;
using LS.MapClean.Addin.Utils;
using LS.MapClean.Addin.ViewModel;
using Visibility = System.Windows.Visibility;

namespace LS.MapClean.Addin.View
{
    public partial class ActionsSettingDialog : Window
    {
        public ActionsSettingDialog()
        {
            InitializeComponent();
        }

        private void OkButton_OnClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void CancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
