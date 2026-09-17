using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace BytePeeX.Views
{
    public partial class SplashScreenWindow : Window
    {
        public SplashScreenWindow()
        {
            InitializeComponent();
            SetVersionText();
        }

        private void SetVersionText()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                if (version != null)
                {
                    TxtVersion.Text = $"v{version.Major}.{version.Minor}";
                }
                else
                {
                    TxtVersion.Text = "v0.3";
                }
            }
            catch
            {
                TxtVersion.Text = "v0.3";
            }
        }

        public async Task RunLoadingAsync()
        {
            // Give window a moment to render layout
            await Task.Delay(100);

            double totalWidth = ProgressBarTrack.ActualWidth > 50 ? ProgressBarTrack.ActualWidth - 2 : 418;

            var steps = new (double TargetPercent, string StatusText, int DurationMs)[]
            {
                (20, "Sistem çekirdeği başlatılıyor...", 200),
                (45, "Fiziksel disk sürücüleri taranıyor...", 250),
                (70, "Yüklü uygulamalar ve dizin mimarisi taranıyor...", 300),
                (90, "Arayüz teması ve grafik motoru hazırlanıyor...", 200),
                (100, "BytePeeX hazır!", 120)
            };

            double currentPercent = 0;

            foreach (var (targetPercent, statusText, durationMs) in steps)
            {
                TxtStatus.Text = statusText;

                // Animate progress bar fill width
                double fromWidth = (currentPercent / 100.0) * totalWidth;
                double toWidth = (targetPercent / 100.0) * totalWidth;

                var widthAnim = new DoubleAnimation
                {
                    From = fromWidth,
                    To = toWidth,
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                ProgressBarFill.BeginAnimation(WidthProperty, widthAnim);

                // Smoothly update percentage text
                int startP = (int)currentPercent;
                int endP = (int)targetPercent;
                int ticks = Math.Max(1, durationMs / 20);
                for (int i = 1; i <= ticks; i++)
                {
                    await Task.Delay(durationMs / ticks);
                    int p = startP + (int)((endP - startP) * ((double)i / ticks));
                    TxtPercent.Text = $"{p}%";
                }

                currentPercent = targetPercent;
            }

            TxtPercent.Text = "100%";
            await Task.Delay(80);
        }
    }
}
