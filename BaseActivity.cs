using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Acr.UserDialogs;
using Android.App;
using Android.Content;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using AndroidX.AppCompat.App;
using Auth0.OidcClient;
using Bumptech.Glide;
using BusinessPartners.Activity;
using BusinessPartners.Helper;
using BusinessPartners.Models;
using BusinessPartners.Service;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using Newtonsoft.Json;
using RestSharp;
using Xamarin.Essentials;

namespace BusinessPartners
{
    public class BaseActivity : AppCompatActivity
    {
        #region Declarations
        public static ServiceManager eGFServiceManager = new ServiceManager(new RestService());
        Global global = new Global();
        public static Auth0Client client;
        public Android.App.Dialog popupDialog, popupdialogDownload;
        public bool IsNavigated = false, IsDownload = false;
        public ImageView gifImageView;
        public TextView DownloadPopUpText;
        public bool IsDownloaded = false;
        public string PageName = "";
        public RewardSectionResult rewardSectionResult;
        public GetDealerUser DealerUserByDetails;
        public dealerUser dealerUser;
        //public string OAuthUrl = string.Empty, AccessToken = string.Empty;

        #endregion

        #region Methods
        public async Task SetUserProfileImage(ImageView imageView)
        {
            try
            {
                if (Connectivity.NetworkAccess == NetworkAccess.Internet)
                {
                    var result = await eGFServiceManager.GetUserProfile(new UserProfileInputModel());
                    if (result.IsActive)
                    {
                        if (result.Status)
                        {
                            Preferences.Set("DealerImage", result.Profile.DealerLogo);
                            Preferences.Set("UserImage", result.Profile.UserImage);
                            if (!string.IsNullOrEmpty(result.Profile.UserImage))
                            {
                                var UserImage = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), System.IO.Path.GetFileName(result.Profile.UserImage).Replace(" ", ""));
                                if (!System.IO.File.Exists(UserImage))
                                    UserImage = await DownloadUserImage(result.Profile.UserImage);
                                Glide.With(this).Load(UserImage).CenterInside().Into(imageView);
                            }
                            else
                            {
                                imageView.SetImageResource(Resource.Drawable.dp);
                            }
                        }
                    }
                }
                else
                {
                    Toast.MakeText(this, Resource.String.No_Internet, ToastLength.Long).Show();
                }
            }
            catch (Exception ex)
            {

            }
        }

        public async Task<string> DownloadUserImage(string userimage)
        {
            if (string.IsNullOrEmpty(userimage)) return "";
            try
            {
                using (UserDialogs.Instance.Loading("Loading..."))
                {
                    var result = await eGFServiceManager.GetBlobCredentials(new GetBlobDetailInputModel());
                    string completeName = "";
                    if (result.IsActive)
                    {
                        if (result.Status)
                        {
                            if (result.BlobDetail != null)
                            {
                                StorageCredentials credentials = new StorageCredentials(result.BlobDetail.AzureArchiveStorageAccount, result.BlobDetail.AzureArchiveStorageKey);
                                CloudStorageAccount storageAccount = new CloudStorageAccount(credentials, useHttps: true);
                                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                                CloudBlobContainer cloudBlobContainer = blobClient.GetContainerReference(result.BlobDetail.BlobContainerName);
                                var filename = System.IO.Path.GetFileName(userimage);
                                CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(filename);

                                bool blobExists = await cloudBlockBlob.ExistsAsync();

                                if (!blobExists)
                                    return "";

                                completeName = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), filename.Replace(" ", ""));
                                if (!System.IO.File.Exists(completeName))
                                {
                                    using (var fileStream = System.IO.File.OpenWrite(completeName))
                                    {
                                        await cloudBlockBlob.DownloadToStreamAsync(fileStream);
                                    }
                                    if (!System.IO.File.Exists(completeName))
                                        return "";
                                }
                            }
                        }
                    }
                    else
                    {
                        Logout();
                        ClearUserData();
                    }
                    return completeName;
                }
            }
            catch (Exception ex)
            {
                return "";
            }
        }

        public async Task GlobalConfigurations()
        {
            try
            {
                if (Connectivity.NetworkAccess == NetworkAccess.Internet)
                {

                    var result = await eGFServiceManager.GetConfiguratorsDetails();
                    Preferences.Set(Constants.CallingNumberMinMax, result.CallingNumberMinMax);
                    Preferences.Set(Constants.DealerAppInvalidNumberMessage, result.DealerAppInvalidNumberMessage);
                    var ProvisionToken = await eGFServiceManager.GetProvisionAccountToken();
                    Preferences.Set(Constants.ProvisionToken, ProvisionToken.Replace("\"", ""));

                }
                else
                {
                    Toast.MakeText(this, Resource.String.No_Internet, ToastLength.Long).Show();
                }
            }
            catch (Exception ex)
            {
                ShowToast(ex);
            }
        }

        public async Task GetNotificationStatus()
        {
            try
            {
                if (Connectivity.NetworkAccess == NetworkAccess.Internet)
                {

                    var result = await eGFServiceManager.GetNotification(new NotificationInputModel());
                    if (result.IsActive)
                    {
                        if (result.Status)
                        {
                            ObservableCollection<Notifications> NotificationList = new ObservableCollection<Notifications>(result.Notifications.OrderByDescending(x => x.DateCreated));
                            var newnotification = NotificationList.Any(x => x.Highlight == 1);
                            Preferences.Set("NotificationIconUpdate", newnotification);

                            rewardSectionResult = await eGFServiceManager.AreRewardsEnabled();
                        }
                    }
                }
                else
                {
                    Toast.MakeText(this, Resource.String.No_Internet, ToastLength.Long).Show();
                }
            }
            catch (Exception ex)
            {
                ShowToast(ex);
            }
        }

        public async Task<dealerUser> GetDealerUserById()
        {
            try
            {
                if (Connectivity.NetworkAccess == NetworkAccess.Internet)
                {
                    DealerUserByDetails = await eGFServiceManager.GetDealerUserById();
                    dealerUser = DealerUserByDetails.dealerUser;
                    return DealerUserByDetails.dealerUser;
                }
                else
                {
                    Toast.MakeText(this, Resource.String.No_Internet, ToastLength.Long).Show();
                }
                return null;
            }
            catch (Exception ex)
            {
                ShowToast(ex);
                return null;
            }
        }

        public async Task<string> DownloadOrderDocument(QuoteDetails quoteDetails)
        {
            try
            {
                if (Connectivity.NetworkAccess == NetworkAccess.Internet)
                {
                    using (UserDialogs.Instance.Loading("Loading..."))
                    {
                        IsDownloaded = false;
                        popupdialogDownload = new Dialog(this);
                        popupdialogDownload.SetContentView(Resource.Layout.downloading_dialog_popup);
                        popupdialogDownload.Window.SetSoftInputMode(SoftInput.AdjustResize);
                        popupdialogDownload.Show();
                        popupdialogDownload.Window.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
                        popupdialogDownload.Window.SetBackgroundDrawableResource(Android.Resource.Color.Transparent);
                        var BtnCancel = (Button)popupdialogDownload.FindViewById(Resource.Id.btnclose);
                        gifImageView = (ImageView)popupdialogDownload.FindViewById(Resource.Id.gifImageView);
                        Glide.With(Application.Context).Load(Resource.Drawable.loder).CenterInside().Into(gifImageView);
                        DownloadPopUpText = (TextView)popupdialogDownload.FindViewById(Resource.Id.DownloadPopUpText);
                        DownloadPopUpText.Text = "Downloading...";
                        BtnCancel.Click += CancelDownloadClikced;
                        using (popupdialogDownload)
                        {
                            var result = await eGFServiceManager.GetBlobCredentials(new GetBlobDetailInputModel());
                            string completeName = "";
                            if (result.IsActive)
                            {
                                if (result.Status)
                                {
                                    if (result.BlobDetail != null)
                                    {
                                        StorageCredentials credentials = new StorageCredentials(result.BlobDetail.AzureArchiveStorageAccount, result.BlobDetail.AzureArchiveStorageKey);
                                        CloudStorageAccount storageAccount = new CloudStorageAccount(credentials, useHttps: true);
                                        CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                                        CloudBlobContainer cloudBlobContainer = blobClient.GetContainerReference(result.BlobDetail.BlobContainerName);
                                        var filename = System.IO.Path.GetFileName(quoteDetails.QuoteDocumentUrl);
                                        CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(filename);

                                        bool blobExists = await cloudBlockBlob.ExistsAsync();

                                        if (!blobExists)
                                        {
                                            popupdialogDownload.Dismiss();
                                            popupdialogDownload.Hide();
                                            return "";
                                        }

                                        completeName = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), filename.Replace(" ", ""));
                                        if (!File.Exists(completeName))
                                        {
                                            using (var fileStream = File.OpenWrite(completeName))
                                            {
                                                await cloudBlockBlob.DownloadToStreamAsync(fileStream);
                                            }
                                            if (!File.Exists(completeName))
                                            {
                                                popupdialogDownload.Dismiss();
                                                popupdialogDownload.Hide();
                                                return "";
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Logout();
                                ClearUserData();
                            }
                            popupdialogDownload.Dismiss();
                            popupdialogDownload.Hide();
                            return completeName;
                        }
                    }
                }
                else
                {
                    Toast.MakeText(this, Resource.String.No_Internet, ToastLength.Long).Show();
                }
                return "";
            }
            catch (Exception ex)
            {
                IsNavigated = false;
                popupdialogDownload.Dismiss();
                popupdialogDownload.Hide();
                global.ShowToast(ex, "DocumentFragment", "DownloadFileAsync");
                return "";
            }
        }

        private void CancelDownloadClikced(object sender, EventArgs e)
        {
            try
            {
                IsNavigated = false;
                IsDownloaded = true;
                popupdialogDownload.Dismiss();
                popupdialogDownload.Hide();
            }
            catch (Exception ex)
            {
                global.ShowToast(ex, "DocumentFragment", "DownloadFileAsync");
            }
        }

        public static async void Logout()
        {
            await client.LogoutAsync();
            Preferences.Set(Constants.IsLogin, false);
        }

        public static void ClearUserData()
        {
            Preferences.Clear();
        }

        public static bool ValidateEmail(string email)
        {

            bool isEmail = Regex.IsMatch(email, @"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z", RegexOptions.IgnoreCase);
            if (isEmail)
                return true;
            else
                return false;
        }

        public bool IsValidPhone(string phone)
        {
            try
            {
                var digit = Preferences.Get(Constants.CallingNumberMinMax, "");
                string[] MinMax = digit.Split(',');
                if (MinMax[0].ToInt32() <= phone.Length && MinMax[1].ToInt32() >= phone.Length)
                    return true;
                else
                    return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public void showSoftKeyboard(View view)
        {
            if (view.RequestFocus())
            {
                InputMethodManager imm = (InputMethodManager)GetSystemService(InputMethodService);
                imm.ShowSoftInput(view, (ShowFlags)ShowSoftInputFlags.Forced);
            }
        }

        public void hideSoftKeyboard(View view)
        {
            InputMethodManager inputManager = (InputMethodManager)GetSystemService(InputMethodService);
            inputManager.HideSoftInputFromWindow(view.WindowToken, 0);
        }

        public async void ShowToast(Exception exception, string PageName = "", string FunctionName = "")
        {
            try
            {
                string text = "Message: " + exception;
                Firebase.Crashlytics.FirebaseCrashlytics.Instance.SetUserId(Preferences.Get("InvitationCode", ""));
                Firebase.Crashlytics.FirebaseCrashlytics.Instance.RecordException(CrashlyticsException.Create(exception));

                var result = await eGFServiceManager.WriteError(exception.Message.ToString());

                //Toast.MakeText(this, "Something went wrong try again after some time.", ToastLength.Long).Show();
                // Toast.MakeText(this, text, ToastLength.Long).Show();
            }
            catch (Exception ex)
            {
                ShowToast(ex, "Global", "ShowToast");
            }
        }

        #endregion

    }
}
