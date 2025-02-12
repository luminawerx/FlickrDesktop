using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using System.Windows.Threading;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Timer = System.Timers.Timer;


namespace FlickrDesktop;

public partial class App : Application
    {
        private const string FlickrApiUrl = "https://api.flickr.com/services/rest/?method=flickr.interestingness.getList&api_key=3103129f7e79ace086511b6c18fa212f&format=json&nojsoncallback=1&extras=url_h,media,path_alias&per_page=70";
        
    private readonly Timer _timer;
        private readonly NotifyIcon _notifyIcon;
        private static readonly HttpClient _httpClient = new HttpClient();
        private string _lastWallpaperPath = string.Empty;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        private const int SPI_SETDESKWALLPAPER = 20;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDCHANGE = 0x02;

        private readonly Random _random;

    public App()
        {
        try
        {

            DispatcherUnhandledException += App_DispatcherUnhandledException;
            SentrySdk.Init(o =>
            {
                o.Dsn = "https://fc05513f92b248359113140ad4843e11@o4507774531993600.ingest.us.sentry.io/4508800648806400";
                o.Debug = false;
                o.TracesSampleRate = 1.0;
            });

            _random  = new Random();

            _notifyIcon = new NotifyIcon
            {
                Icon = new Icon("flickr.ico"),
                Visible = true,
                ContextMenuStrip = new ContextMenuStrip(),
                Text = "Flickr Desktop"
            };

            _notifyIcon.ContextMenuStrip.Items.Add("Change Wallpaper", null, async (s, e) => await ChangeWallpaperAsync());
            _notifyIcon.ContextMenuStrip.Items.Add("Close", null, (s, e) => ExitApplication());

            _ = ChangeWallpaperAsync();

            _timer = new Timer(60 * 60 * 1000);
            _timer.Elapsed += async (s, e) => await ChangeWallpaperAsync();
            _timer.Start();
        }
        catch (Exception ex)
        {
#if !DEBUG         
        SentrySdk.CaptureException(ex);
#endif
        }
    }

    void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        SentrySdk.CaptureException(e.Exception);
        e.Handled = true;
    }

    private async Task ChangeWallpaperAsync()
        {
            try
            {
                string? imageUrl = null;
                string jsonResponse = await _httpClient.GetStringAsync(FlickrApiUrl);
                using JsonDocument doc = JsonDocument.Parse(jsonResponse);
                var photos = doc.RootElement.GetProperty("photos").GetProperty("photo").EnumerateArray().ToList();
                if (photos.Count != 0)
                {
                    var selectedPhoto = photos[_random.Next(photos.Count)];
                    if (selectedPhoto.TryGetProperty("url_h", out var urlProperty))
                        imageUrl = urlProperty.GetString();
                }

                if (imageUrl!=null)
                {
                    string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid().ToString().Replace("-","")}.jpg");
                    byte[] imageData = await _httpClient.GetByteArrayAsync(imageUrl);
                    await File.WriteAllBytesAsync(tempPath, imageData);
                    string newWallpaperPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid().ToString().Replace("-", "")}.jpg");
                    using (var originalImage = new Bitmap(tempPath))
                    {
                        var resizedImage = new Bitmap(originalImage, new Size(1920, 1080));
                        resizedImage.Save(newWallpaperPath);
                        // TODO: render picture title, description etc. based on settings.
                    }
                    File.Delete(tempPath);
                    SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, newWallpaperPath, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                    if(!string.IsNullOrEmpty(_lastWallpaperPath) && File.Exists(_lastWallpaperPath)) File.Delete(_lastWallpaperPath);    
                    _lastWallpaperPath = newWallpaperPath;
            }
            }
            catch (Exception ex)
            {
#if DEBUG
            MessageBox.Show("Error: " + ex.Message);
#else
            SentrySdk.CaptureException(ex);
#endif
        }
    }

    private void ExitApplication()
    {
        if(_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        Current.Shutdown();
    }
    }


