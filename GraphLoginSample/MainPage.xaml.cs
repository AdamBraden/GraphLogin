using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Security.Authentication.Web;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace GraphLoginSample
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            login.ClientId = "935c61af-136a-4671-b4f6-cabf7964bfb4";
            System.Diagnostics.Debug.WriteLine(WebAuthenticationBroker.GetCurrentApplicationCallbackUri().Host.ToUpper());
            var gsc = await login.SignInAsync();

        }

        private void login_SignInCompleted(object sender, GraphLogin.SignInEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(e.User.DisplayName);
        }
    }
}
