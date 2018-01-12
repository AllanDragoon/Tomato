using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LS.MapClean.Addin.SpreadPoints.View
{
    /// <summary>
    /// Interaction logic for SpreadPointSettingsDlg.xaml
    /// </summary>
    public partial class SpreadPointSettingsDlg : Window
    {
        public SpreadPointSettingsDlg()
        {
            InitializeComponent();
        }

        private void OnCancelClicked(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void OnOkClicked(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
