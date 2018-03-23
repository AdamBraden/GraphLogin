using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.ApplicationSettings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Microsoft.Graph;
using System.Threading.Tasks;
using Windows.Storage;
using System.Net.Http.Headers;
using Windows.Security.Authentication.Web.Core;
using Windows.Storage.Streams;
using Windows.Security.Authentication.Web;
using Microsoft.IdentityModel.Clients.ActiveDirectory;


// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace GraphLoginSample
{
    public sealed partial class GraphLogin : UserControl
    {
        //for most Enteprise apps, we only care about AAD version of MSGraph
        private const string MicrosoftAccountProvider = "https://login.microsoft.com";
        private const string MicrosoftGraphResource = "https://graph.microsoft.com";    //Microsoft Graph
        private const string MicrosoftCommonAuthority = "https://login.microsoftonline.com/common/";  //for use with ADAL
        private const string EnterpriseAuthority = "organizations";
        
        private ViewStyle _view = ViewStyle.SmallProfile;
        private BitmapImage _backgroundImage = null;
        private User _currentUser = null;
        private string _tenant = "";


        public enum ViewStyle
        {
            Picture = 0,        //picture only
            SmallProfile = 1,   //pic + displayname
            LargeProfile = 2    //pic, displayname, email
        }

        /// <summary>
        /// The ClientId for the app registered in the Azure portal (portal.azure.com)
        /// </summary>
        public string ClientId { get; set; }
        
        /// <summary>
        /// Scope - TBD...
        /// </summary>
        public string Scope { get; set; }
        
        /// <summary>
        /// sets the default image for the control when there is no logged on user
        /// </summary>
        public BitmapImage BackgroundImage {
            get { return _backgroundImage;  }
            set { _backgroundImage = value;
                    profilePic.ImageSource = _backgroundImage;
                }
        }

        /// <summary>
        /// Changes the display of the control between small profile pic view and larger more detailed view
        /// </summary>
        public ViewStyle View
        {
            get { return _view; }
            set
            {
                //TODO: change display to resize appropriately
                _view = value;
            }
        }

        public string Tenant { get => _tenant; set => _tenant = value; }

        public GraphLogin()
        {
            this.InitializeComponent();
            
        }

        /// <summary>
        /// Signout of the Microsoft Graph
        /// </summary>
        public void SignOut(RoutedEventArgs e)
        {
            //clear user info
            profilePic.ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/person-placeholder.jpg"));
            displayName.Text = "";
            emailName.Text = "";

            //release internal objects
            _currentUser = null;

            //fire event for consumers
            SignOutCompleted?.Invoke(this, e);

        }

        public async void SignInAsync(bool Prompt = false)
        {
            var _client = new GraphServiceClient(
                 new DelegateAuthenticationProvider(
                                                    async (requestMessage) =>
                                                    {
                                                        var token = await GetTokenForUserAsync(Prompt);
                                                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", token);
                                                    }));

            //update UI
            var _user = await _client.Me.Request().GetAsync();
            LoadUserInfo(_user);

            var _pic = await _client.Me.Photo.Content.Request().GetAsync();
            LoadProfilePicture(_pic);

            //if the user changed, fire the event
            //the graphserviceclient instance is returned through the eventargs
            if (_currentUser != _user)
            {
                _currentUser = _user;

                //see if anyone is listening
                SignInCompleted?.Invoke(this, new SignInEventArgs(_client));
            }

        }

        private async Task<string> GetTokenForUserAsync(bool Prompt = false)
        {
            string tokenForUser = null;
            Windows.Security.Credentials.WebAccountProvider wap = null;
           
            Scope = "";  //null will cause an error

            //check if the user is attempting to signin with a different account
            if (Prompt == true)
            {
                //use ADAL 
                AuthenticationResult authResult;
                var authContext = new AuthenticationContext(MicrosoftCommonAuthority + _tenant);

                try
                {
                    //authContext = new AuthenticationContext(authContext.TokenCache.ReadItems().First().Authority);
                    authResult = await authContext.AcquireTokenSilentAsync(MicrosoftGraphResource, ClientId);
                }
                catch (AdalSilentTokenAcquisitionException)
                {
                    var redirectURI = new Uri("ms-appx-web://microsoft.aad.brokerplugin/" + WebAuthenticationBroker.GetCurrentApplicationCallbackUri());

                    try
                    {
                        authResult = await authContext.AcquireTokenAsync(MicrosoftGraphResource, ClientId, redirectURI, new PlatformParameters(PromptBehavior.Auto, false));
                    }
                    catch (AdalException ex)
                    {
                        if (ex.ErrorCode != "authentication_canceled")
                        {
                            System.Diagnostics.Debug.WriteLine(string.Format("Authentication error, please contact your administrator.\n\nError: {0}\n\nError Description:\n\n{1}", ex.ErrorCode, ex.Message));
                        }
                        return "";
                    }

                }

                tokenForUser = authResult.AccessToken;
                
            }
            else   //use WAM
            {
                wap = await WebAuthenticationCoreManager.FindAccountProviderAsync(MicrosoftAccountProvider, EnterpriseAuthority);
                WebTokenRequest wtr = new WebTokenRequest(wap, Scope, ClientId);
                wtr.Properties.Add("resource", MicrosoftGraphResource);
                WebTokenRequestResult wtrr = await WebAuthenticationCoreManager.RequestTokenAsync(wtr);
                if (wtrr.ResponseStatus == WebTokenRequestStatus.Success)
                {
                    tokenForUser = wtrr.ResponseData[0].Token;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(wtrr.ResponseError);
                }

            }

            return tokenForUser;
        }


        /// <summary>
        /// load the users display name and email
        /// </summary>
        /// <param name="loggedInUser"></param>
        private void LoadUserInfo(User loggedInUser)
        {
            displayName.Text = loggedInUser.DisplayName;
            emailName.Text = loggedInUser.Mail;
        }

        //load the users profile picture
        private async void LoadProfilePicture(Stream photoStream)
        {
            //must convert System.IO.Stream into something UWP can use
            // taken from: https://stackoverflow.com/questions/7669311/is-there-a-way-to-convert-a-system-io-stream-to-a-windows-storage-streams-irando

            var memStream = new MemoryStream();
            await photoStream.CopyToAsync(memStream);
            memStream.Position = 0;
            var bitmap = new BitmapImage();
            bitmap.SetSource(memStream.AsRandomAccessStream());
            profilePic.ImageSource = bitmap;
            
        }


        #region Event Definition
        public class SignInEventArgs : EventArgs
        {
            private Microsoft.Graph.GraphServiceClient _graphClient;

            public SignInEventArgs(GraphServiceClient GraphClient)
            {
                _graphClient = GraphClient;
            }
            public Microsoft.Graph.GraphServiceClient GraphClient
            {
                get { return this._graphClient; }
            }

        }

        
        //Event name 
        public event SignInHandler SignInCompleted;
        public event SignOutHandler SignOutCompleted;
        public delegate void SignInHandler(object sender, SignInEventArgs e);
        public delegate void SignOutHandler(object sender, RoutedEventArgs e);

        #endregion

        private void SwitchAccount_Click(object sender, RoutedEventArgs e)
        {
            //implement switching account - signout and call WAM with prompt UI
            SignOut(e);

            //signin, this will also fire the signincompleted event
            SignInAsync(true);

        }

        private void SignOut_Click(object sender, RoutedEventArgs e)
        {
            //implement sign out
            SignOut(e);

        }

        private void Grid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        }

    }
}
