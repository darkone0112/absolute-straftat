using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SharpImage = SixLabors.ImageSharp.Image;

namespace AbsoluteStraftat.Installer;

internal static class InstallerUi
{
    private static readonly ManualResetEventSlim Started = new();
    private static readonly ManualResetEventSlim Closed = new();
    private static Thread? uiThread;

    public static void Start(bool noPopup)
    {
        if (noPopup)
        {
            return;
        }

        uiThread = new Thread(() =>
        {
            try
            {
                InstallerAppBuilder.Build().StartWithClassicDesktopLifetime(
                    Array.Empty<string>(),
                    ShutdownMode.OnExplicitShutdown);
            }
            finally
            {
                Started.Set();
                Closed.Set();
            }
        })
        {
            IsBackground = true,
            Name = "Absolute STRAFTAT UI"
        };

        if (OperatingSystem.IsWindows())
        {
            uiThread.SetApartmentState(ApartmentState.STA);
        }

        uiThread.Start();
        Started.Wait(TimeSpan.FromSeconds(5));
    }

    public static void ShowCompletion(bool noPopup)
    {
        ShowResult(noPopup, "Installation complete", "Launch STRAFTAT once so BepInEx can finish generating its config files.");
    }

    public static void ShowResult(bool noPopup, string title, string message)
    {
        if (noPopup || uiThread is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => InstallerApp.CurrentWindow?.ShowResult(title, message));
        Closed.Wait();
    }

    internal static void SignalStarted()
    {
        Started.Set();
    }

    internal static void SignalClosed()
    {
        Closed.Set();
    }
}

internal static class InstallerAppBuilder
{
    public static AppBuilder Build()
    {
        return AppBuilder.Configure<InstallerApp>()
            .UsePlatformDetect();
    }
}

internal sealed class InstallerApp : Application
{
    public static InstallerWindow? CurrentWindow { get; private set; }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            CurrentWindow = new InstallerWindow();
            desktop.MainWindow = CurrentWindow;
            CurrentWindow.Show();
            InstallerUi.SignalStarted();
        }

        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed class InstallerWindow : Window
{
    private readonly Image posterImage;
    private readonly Grid resultPanel;
    private readonly AnimatedGifImage borderGif;
    private readonly AnimatedGifImage completionGif;
    private readonly AnimatedGifImage communicationGif;
    private readonly AnimatedGifImage exitGif;
    private readonly TextBlock resultTitle;
    private readonly TextBlock resultMessage;
    private readonly Button closeButton;
    private readonly TranslateTransform exitTransform = new(-230, 0);
    private readonly CancellationTokenSource animationCancellation = new();
    private static readonly FontFamily GoofyFont = new("Comic Sans MS, Berlin Sans FB, Trebuchet MS, Impact, sans-serif");
    private static readonly IBrush[] TextCycleBrushes =
    {
        new SolidColorBrush(Color.Parse("#FF00E6")),
        new SolidColorBrush(Color.Parse("#39FF14")),
        new SolidColorBrush(Color.Parse("#FFFF00")),
        new SolidColorBrush(Color.Parse("#00F5FF")),
        new SolidColorBrush(Color.Parse("#FF6A00"))
    };

    public InstallerWindow()
    {
        Title = "Absolute STRAFTAT";
        Width = 620;
        Height = 520;
        MinWidth = 620;
        MinHeight = 520;
        CanResize = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.Parse("#111111"));
        using (var iconStream = ResourceImages.OpenResource("straftat-ico.jpg"))
        {
            Icon = new WindowIcon(iconStream);
        }

        posterImage = new Image
        {
            Source = ResourceImages.LoadBitmap("absolute.straftat.jpg"),
            Stretch = Stretch.Uniform,
            Width = 560,
            Height = 390,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var backgroundImage = new Image
        {
            Source = ResourceImages.LoadBitmap("background.png"),
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        completionGif = new AnimatedGifImage("hop-on-straftat-straftat.gif")
        {
            Width = 430,
            Height = 280,
            IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        communicationGif = new AnimatedGifImage("communication.gif")
        {
            Width = 220,
            Height = 76,
            IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 8)
        };

        exitGif = new AnimatedGifImage("exit.gif")
        {
            Width = 78,
            Height = 78,
            IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransform = exitTransform,
            Margin = new Thickness(0, 0, 8, 0)
        };

        resultTitle = new TextBlock
        {
            IsVisible = false,
            Foreground = new SolidColorBrush(Color.Parse("#FF00E6")),
            FontSize = 26,
            FontFamily = GoofyFont,
            FontWeight = FontWeight.Black,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 8, 0, 2)
        };

        resultMessage = new TextBlock
        {
            IsVisible = false,
            Foreground = new SolidColorBrush(Color.Parse("#39FF14")),
            FontSize = 16,
            FontFamily = GoofyFont,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 500,
            Margin = new Thickness(0, 0, 0, 12)
        };

        closeButton = new Button
        {
            Content = new AnimatedGifImage("absolute.straftat.gif")
            {
                Width = 154,
                Height = 48,
                HorizontalAlignment = HorizontalAlignment.Center
            },
            IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent
        };
        closeButton.Click += (_, _) => Close();
        Closed += (_, _) =>
        {
            animationCancellation.Cancel();
            InstallerUi.SignalClosed();
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        };

        resultPanel = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Children =
            {
                completionGif,
                resultTitle,
                resultMessage,
                communicationGif,
                exitGif,
                closeButton
            }
        };
        Grid.SetColumnSpan(completionGif, 2);
        Grid.SetColumnSpan(resultTitle, 2);
        Grid.SetColumnSpan(resultMessage, 2);
        Grid.SetColumnSpan(communicationGif, 1);
        Grid.SetRow(resultTitle, 1);
        Grid.SetRow(resultMessage, 2);
        Grid.SetRow(communicationGif, 3);
        Grid.SetRow(exitGif, 3);
        Grid.SetColumn(exitGif, 1);
        Grid.SetRow(closeButton, 3);
        Grid.SetColumn(closeButton, 1);

        borderGif = new AnimatedGifImage("border.gif")
        {
            Stretch = Stretch.Fill,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var layout = new Grid
        {
            Margin = new Thickness(14),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star)
            },
            Children =
            {
                backgroundImage,
                posterImage,
                resultPanel,
                borderGif
            }
        };
        layout.PointerReleased += (_, _) =>
        {
            if (resultPanel.IsVisible)
            {
                Close();
            }
        };
        borderGif.IsVisible = false;
        resultPanel.IsVisible = false;
        Content = new Border
        {
            BorderBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#FF00E6"), 0),
                    new GradientStop(Color.Parse("#FFFF00"), 0.5),
                    new GradientStop(Color.Parse("#00F5FF"), 1)
                }
            },
            BorderThickness = new Thickness(5),
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.Parse("#120018")),
            Child = new Border
            {
                Margin = new Thickness(8),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(190, 18, 0, 24)),
                BorderBrush = new SolidColorBrush(Color.Parse("#00F5FF")),
                BorderThickness = new Thickness(1),
                Child = layout
            }
        };
    }

    public void ShowResult(string title, string message)
    {
        Width = 620;
        Height = 560;
        posterImage.IsVisible = false;
        resultPanel.IsVisible = true;
        completionGif.IsVisible = true;
        resultTitle.Text = title;
        resultTitle.IsVisible = true;
        resultMessage.Text = message;
        resultMessage.IsVisible = true;
        communicationGif.IsVisible = true;
        exitGif.IsVisible = true;
        borderGif.IsVisible = true;
        closeButton.IsVisible = true;
        _ = CycleTextColorsAsync(animationCancellation.Token);
        _ = SlideExitGifAsync(animationCancellation.Token);
        Activate();
    }

    private async Task CycleTextColorsAsync(CancellationToken cancellationToken)
    {
        var index = 0;
        while (!cancellationToken.IsCancellationRequested && resultPanel.IsVisible)
        {
            var titleBrush = TextCycleBrushes[index % TextCycleBrushes.Length];
            var messageBrush = TextCycleBrushes[(index + 2) % TextCycleBrushes.Length];
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                resultTitle.Foreground = titleBrush;
                resultMessage.Foreground = messageBrush;
            });

            index++;
            await Task.Delay(230, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SlideExitGifAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && resultPanel.IsVisible)
        {
            for (var x = -230; x <= -8; x += 8)
            {
                var current = x;
                await Dispatcher.UIThread.InvokeAsync(() => exitTransform.X = current);
                await Task.Delay(28, cancellationToken).ConfigureAwait(false);
            }

            await Task.Delay(450, cancellationToken).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() => exitTransform.X = -230);
            await Task.Delay(180, cancellationToken).ConfigureAwait(false);
        }
    }
}

internal sealed class AnimatedGifImage : Image, IDisposable
{
    private readonly IReadOnlyList<GifFrame> frames;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private int frameIndex;

    public AnimatedGifImage(string resourceName)
    {
        Stretch = Stretch.Uniform;
        frames = ResourceImages.LoadGifFrames(resourceName);
        if (frames.Count > 0)
        {
            Source = frames[0].Bitmap;
            _ = PlayAsync(cancellationTokenSource.Token);
        }
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }

    private async Task PlayAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && frames.Count > 1)
        {
            var delay = frames[frameIndex].Delay;
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            frameIndex = (frameIndex + 1) % frames.Count;
            await Dispatcher.UIThread.InvokeAsync(() => Source = frames[frameIndex].Bitmap);
        }
    }
}

internal sealed record GifFrame(Bitmap Bitmap, TimeSpan Delay);

internal static class ResourceImages
{
    public static Bitmap LoadBitmap(string resourceName)
    {
        using var stream = OpenResource(resourceName);
        return new Bitmap(stream);
    }

    public static IReadOnlyList<GifFrame> LoadGifFrames(string resourceName)
    {
        using var stream = OpenResource(resourceName);
        using var image = SharpImage.Load<Rgba32>(stream);
        var frames = new List<GifFrame>();

        for (var index = 0; index < image.Frames.Count; index++)
        {
            var frame = image.Frames[index];
            using var frameImage = image.Frames.CloneFrame(index);
            using var pngStream = new MemoryStream();
            SixLabors.ImageSharp.ImageExtensions.SaveAsPng(frameImage, pngStream);
            pngStream.Position = 0;

            var delay = SixLabors.ImageSharp.MetadataExtensions.GetGifMetadata(frame.Metadata).FrameDelay;
            frames.Add(new GifFrame(new Bitmap(pngStream), TimeSpan.FromMilliseconds(Math.Max(delay * 10, 60))));
        }

        return frames;
    }

    public static Stream OpenResource(string resourceName)
    {
        return Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InstallerException($"Missing embedded image resource: {resourceName}");
    }
}
