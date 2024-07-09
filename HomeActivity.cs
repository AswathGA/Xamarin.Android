
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace BusinessPartners
{
    [Activity(Label = "HomeActivity", MainLauncher = false, ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize)]
    public class HomeActivity : BaseActivity
    {
        #region Declartions
        public static Context GetContext;
        #endregion

        #region PageLoad
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            GetContext = this;
            SetContentView(Resource.Layout.tab_page);
        }
        #endregion
    }
}
