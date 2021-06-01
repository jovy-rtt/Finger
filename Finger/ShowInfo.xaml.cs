using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Finger
{
    /// <summary>
    /// ShowInfo.xaml 的交互逻辑
    /// </summary>
    public partial class ShowInfo : Window
    {
        public ShowInfo()
        {
            InitializeComponent();
            var t = User.infos;
            var q = from t1 in t
                    select new
                    {
                        a = t1.id,
                        b = t1.name,
                        c = t1.sex,
                        d = t1.phone,
                        f = t1.finger
                    };
            dg.ItemsSource = q.ToList();
        }
    }
}
