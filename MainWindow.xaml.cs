using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Brushes = System.Windows.Media.Brushes;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using Cursors = System.Windows.Input.Cursors;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Point = System.Windows.Point;

namespace DesktopPager
{
    public partial class MainWindow : Window
    {
        private readonly PageManager _pageManager;
        private int _currentPage;

        // Farebné dvojice pre náhľad plochy, keď stránka nemá nastavenú vlastnú tapetu.
        // Väčšia paleta = susedné stránky sa farebne takmer nikdy neopakujú.
        private static readonly (Color, Color)[] PagePalette = new (Color, Color)[]
        {
            (Color.FromRgb(0x42,0x85,0xF4), Color.FromRgb(0x34,0xA8,0x53)), // modrá -> zelená
            (Color.FromRgb(0x9C,0x27,0xB0), Color.FromRgb(0xE9,0x1E,0x63)), // fialová -> ružová
            (Color.FromRgb(0xFF,0x98,0x00), Color.FromRgb(0xF4,0x43,0x36)), // oranžová -> červená
            (Color.FromRgb(0x00,0xAC,0xC1), Color.FromRgb(0x00,0x96,0x88)), // tyrkysová -> zelenkavá
            (Color.FromRgb(0x1D,0xE9,0xB6), Color.FromRgb(0x00,0x69,0x5C)), // jasná tyrkysová -> tmavá tyrkysová
            (Color.FromRgb(0xFB,0xC0,0x2D), Color.FromRgb(0xF3,0x91,0x12)), // žltá -> jantárová
            (Color.FromRgb(0x3F,0x51,0xB5), Color.FromRgb(0x1A,0x23,0x7E)), // indigo -> tmavomodrá
            (Color.FromRgb(0x8B,0xC3,0x4A), Color.FromRgb(0x33,0x69,0x1E)), // svetlozelená -> tmavozelená
            (Color.FromRgb(0xD8,0x1B,0x60), Color.FromRgb(0x4A,0x14,0x8C)), // magenta -> fialová
            (Color.FromRgb(0x26,0xC6,0xDA), Color.FromRgb(0x01,0x57,0x9B)), // azúrová -> tmavomodrá
            (Color.FromRgb(0xEF,0x6C,0x00), Color.FromRgb(0xBF,0x36,0x0C)), // tmavooranžová -> tehlová
            (Color.FromRgb(0xFF,0x6F,0x61), Color.FromRgb(0x6A,0x1B,0x9A)), // koralová -> tmavofialová
        };

        private static (Color, Color) GetPaletteColors(int pageNum)
        {
            return PagePalette[Math.Max(0, pageNum - 1) % PagePalette.Length];
        }

        // Zosvetlí farbu smerom k bielej - používa sa pre žiaru aktívnej karty,
        // aby bola kontrastnejšia a "nestrácala sa" na sivom pozadí.
        private static Color LightenTowardWhite(Color c, double amount)
        {
            byte r = (byte)(c.R + (255 - c.R) * amount);
            byte g = (byte)(c.G + (255 - c.G) * amount);
            byte b = (byte)(c.B + (255 - c.B) * amount);
            return Color.FromRgb(r, g, b);
        }

        public MainWindow(PageManager pageManager)
        {
            InitializeComponent();
            _pageManager = pageManager;
            LoadPages();
        }

        private void LoadPages()
        {
            _currentPage = _pageManager.GetCurrentPage();
            var pages = _pageManager.GetAvailablePages();

            PagesItemsControl.Items.Clear();

            foreach (int pageNum in pages)
            {
                var pageCard = CreatePageCard(pageNum, pageNum == _currentPage);
                PagesItemsControl.Items.Add(pageCard);
            }
        }

        private Border CreatePageCard(int pageNum, bool isActive)
        {
            string pageName = _pageManager.GetPageName(pageNum);
            var wallpaperConfig = _pageManager.GetWallpaperConfig(pageNum);

            var (accent1, accent2) = GetPaletteColors(pageNum);

            // Vonkajšia karta - jeden spoločný štýl pre všetky; aktívna karta dostane
            // farebný rámček a žiaru priamo vo farbách svojho náhľadu (nižšie).
            var card = new Border
            {
                Style = (Style)FindResource("PageCardStyle"),
                Width = 252
            };

            if (isActive)
            {
                card.BorderBrush = new LinearGradientBrush(accent1, accent2, new Point(0, 0), new Point(1, 1));
                card.BorderThickness = new Thickness(1.5);
                card.Effect = new DropShadowEffect
                {
                    Color = LightenTowardWhite(accent1, 0.35),
                    Opacity = 0.65,
                    BlurRadius = 26,
                    ShadowDepth = 0
                };
            }

            var outerStack = new StackPanel { Orientation = Orientation.Vertical };

            // ---- Náhľad plochy (16:9), pravouhlý, s 2px čiernym orámovaním ----
            const double screenWidth = 224;
            const double screenHeight = 126;

            var screen = new Border
            {
                Width = screenWidth,
                Height = screenHeight,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Pozadie náhľadu: skutočná tapeta, ak je nastavená, inak farebná dvojica podľa poradia stránky
            Brush screenBackground;
            if (wallpaperConfig != null && wallpaperConfig.Type == "Image" && !string.IsNullOrEmpty(wallpaperConfig.Value)
                && System.IO.File.Exists(wallpaperConfig.Value))
            {
                try
                {
                    var imgBrush = new ImageBrush(new BitmapImage(new Uri(wallpaperConfig.Value)))
                    {
                        Stretch = Stretch.UniformToFill
                    };
                    screenBackground = imgBrush;
                }
                catch
                {
                    screenBackground = GetPaletteBrush(pageNum);
                }
            }
            else
            {
                screenBackground = GetPaletteBrush(pageNum);
            }
            screen.Background = screenBackground;

            var screenOverlay = new Grid();

            // Tmavý prechod dole + názov stránky, aby bol text čitateľný na akomkoľvek pozadí
            var captionBar = new Border
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Height = 34
            };
            var captionGradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            captionGradient.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0));
            captionGradient.GradientStops.Add(new GradientStop(Color.FromArgb(200, 0, 0, 0), 1));
            captionBar.Background = captionGradient;
            captionBar.Child = new TextBlock
            {
                Text = pageName,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Margin = new Thickness(8, 0, 8, 6),
                VerticalAlignment = VerticalAlignment.Bottom,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            screenOverlay.Children.Add(captionBar);

            screen.Child = screenOverlay;

            // Obalový Grid, do ktorého vieme nad náhľad "prisadiť" odznak AKTÍVNA
            // presne na stred hornej čiernej linky (podobne ako výrez pri telefóne).
            var monitorContainer = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
            monitorContainer.Children.Add(screen);

            if (isActive)
            {
                var badge = new Border
                {
                    Background = new LinearGradientBrush(accent1, accent2, new Point(0, 0), new Point(1, 0)),
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(8, 2, 8, 2),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, -6, 0, 0) // straddí hornú čiernu linku rámu presne v strede
                };
                badge.Child = new TextBlock
                {
                    Text = "AKTÍVNA",
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                };
                monitorContainer.Children.Add(badge);
            }

            outerStack.Children.Add(monitorContainer);

            // ---- Ovládacie tlačidlá pod náhľadom ----
            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var renameButton = new Button
            {
                Content = "\uE70F", // Edit (ceruzka)
                Style = (Style)FindResource("ModernButtonStyle"),
                Tag = pageNum,
                ToolTip = "Premenovať plochu"
            };
            renameButton.Click += RenameButton_Click;
            buttonsPanel.Children.Add(renameButton);

            var wallpaperButton = new Button
            {
                Content = "\uEB9F", // Photo2 (obrázok)
                Style = (Style)FindResource("ModernButtonStyle"),
                Tag = pageNum,
                ToolTip = "Tapeta"
            };

            var wpMenu = new ContextMenu();
            var setImgItem = new MenuItem { Header = "Nastaviť obrázok..." };
            setImgItem.Click += (s, e) => SetWallpaperImage(pageNum);
            wpMenu.Items.Add(setImgItem);

            var setWeItem = new MenuItem { Header = "Nastaviť Wallpaper Engine ID..." };
            setWeItem.Click += (s, e) => SetWallpaperEngine(pageNum);
            wpMenu.Items.Add(setWeItem);

            wpMenu.Items.Add(new Separator());

            var applyAllItem = new MenuItem { Header = "Použiť na všetky plochy" };
            applyAllItem.Click += (s, e) => ApplyWallpaperToAll(pageNum);
            wpMenu.Items.Add(applyAllItem);

            var removeItem = new MenuItem { Header = "Odstrániť tapetu" };
            removeItem.Click += (s, e) => RemoveWallpaper(pageNum);
            wpMenu.Items.Add(removeItem);

            wallpaperButton.Click += (s, e) => { wallpaperButton.ContextMenu.IsOpen = true; };
            wallpaperButton.ContextMenu = wpMenu;
            buttonsPanel.Children.Add(wallpaperButton);

            var deleteButton = new Button
            {
                Content = "\uE74D", // Delete (kôš)
                ToolTip = "Vymazať plochu",
                Style = (Style)FindResource("ModernButtonStyle"),
                Tag = pageNum,
                IsEnabled = _pageManager.GetAvailablePages().Count > 1
            };
            deleteButton.Click += DeleteButton_Click;
            buttonsPanel.Children.Add(deleteButton);

            outerStack.Children.Add(buttonsPanel);
            card.Child = outerStack;

            // Klik na kartu (mimo tlačidiel) prepne plochu
            if (!isActive)
            {
                card.MouseLeftButtonDown += (s, e) =>
                {
                    SwitchToPage(pageNum);
                };
            }

            return card;
        }

        private static Brush GetPaletteBrush(int pageNum)
        {
            var (c1, c2) = GetPaletteColors(pageNum);
            return new LinearGradientBrush(c1, c2, new Point(0, 0), new Point(1, 1));
        }

        private void SwitchToPage(int pageNum)
        {
            try
            {
                _pageManager.SwitchPage(pageNum);
                LoadPages(); // Refresh
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chyba pri prepínaní plochy: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveWallpaper(int pageNum)
        {
            try
            {
                _pageManager.RemoveWallpaper(pageNum);
                LoadPages();
            }
            catch (Exception ex) { MessageBox.Show($"Chyba: {ex.Message}"); }
        }

        private void ApplyWallpaperToAll(int sourcePage)
        {
            try
            {
                string pageName = _pageManager.GetPageName(sourcePage);
                if (MessageBox.Show($"Použiť tapetu z plochy '{pageName}' na VŠETKY plochy?", "Potvrdenie", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _pageManager.CopyWallpaperToAll(sourcePage);
                    LoadPages();
                }
            }
            catch (Exception ex) { MessageBox.Show($"Chyba: {ex.Message}"); }
        }

        private void RenameButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            int pageNum = (int)button!.Tag;
            string currentName = _pageManager.GetPageName(pageNum);

            var dialog = new InputDialog("Premenovať plochu", $"Zadaj nový názov pre '{currentName}':", currentName)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
            {
                try
                {
                    _pageManager.RenamePage(pageNum, dialog.ResponseText);
                    LoadPages(); // Refresh
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Chyba pri premenovaní plochy: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            int pageNum = (int)button!.Tag;
            string pageName = _pageManager.GetPageName(pageNum);

            if (_pageManager.GetCurrentPage() == pageNum)
            {
                MessageBox.Show("Aktívnu plochu nemôžeš vymazať", "Upozornenie", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Naozaj chceš vymazať plochu '{pageName}'?\n\nVšetky súbory na tejto ploche budú natrvalo odstránené!",
                "Vymazať plochu",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _pageManager.DeletePage(pageNum);
                    LoadPages(); // Refresh
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Chyba pri mazaní plochy: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void NewPageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _pageManager.CreateNewPage();
                LoadPages(); // Refresh
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chyba pri vytváraní plochy: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetWallpaperImage(int pageNum)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Obrázky|*.jpg;*.jpeg;*.png;*.bmp",
                Title = "Vybrať tapetu"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    _pageManager.SetWallpaperForPage(pageNum, openFileDialog.FileName);
                    LoadPages(); // Refresh UI
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Chyba pri nastavovaní tapety: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SetWallpaperEngine(int pageNum)
        {
            var dialog = new InputDialog("Wallpaper Engine", "Zadaj Workshop ID alebo cestu k súboru:");
            dialog.Owner = this;

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
            {
                try
                {
                    _pageManager.SetWallpaperEngineForPage(pageNum, dialog.ResponseText.Trim());
                    LoadPages(); // Refresh UI
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Chyba pri nastavovaní tapety: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            // Ikonka tlačidla sa prepne medzi "Maximalizovať" a "Obnoviť" podľa aktuálneho stavu okna
            if (MaximizeButton != null)
            {
                MaximizeButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
                MaximizeButton.ToolTip = WindowState == WindowState.Maximized ? "Obnoviť" : "Maximalizovať";
            }

            UpdateWindowChromeCorners();
        }

        // Pri maximalizácii má okno vyplniť celú obrazovku s pravými uhlami (presne ako
        // v prehliadači); pri bežnom (obnovenom) stave sa vrátia zaoblené rohy.
        private void UpdateWindowChromeCorners()
        {
            bool maximized = WindowState == WindowState.Maximized;

            if (RootBorder != null)
                RootBorder.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(12);

            if (TitleBarBorder != null)
                TitleBarBorder.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(11, 11, 0, 0);

            if (BottomBarBorder != null)
                BottomBarBorder.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(0, 0, 11, 11);

            if (Chrome != null)
                Chrome.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(12);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateWindowChromeCorners();
            CheckSystemStatus();
        }

        private void CheckSystemStatus()
        {
            if (!_pageManager.IsJunctionActive)
            {
                string title = _pageManager.IsFirstRun ? "Aktivovať Desktop Pager" : "Desktop Pager je neaktívny";
                string message = _pageManager.IsFirstRun
                    ? "Vitaj v Desktop Pageri!\n\nChceš aktivovať stránkované plochy? Táto voľba premení tvoju plochu na stránkovaný priečinok."
                    : "Desktop Pager momentálne nespravuje tvoju plochu (obnovený stav).\n\nChceš ho znova aktivovať? Táto voľba znova premení tvoju plochu na stránkovaný priečinok.";

                var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Explicitly switch to current page to force junction creation
                        _pageManager.SwitchPage(_pageManager.GetCurrentPage());
                        LoadPages();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Aktivácia zlyhala: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                        System.Windows.Application.Current.Shutdown();
                    }
                }
                else
                {
                    // User declined, close the app
                    System.Windows.Application.Current.Shutdown();
                }
            }
        }

        // =====================================================================
        // Oprava správania pri maximalizácii, aby sa okno neprekrývalo s
        // Windows lištou (bežný problém pri WindowStyle="None" + WindowChrome).
        // =====================================================================

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
        }

        private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_GETMINMAXINFO = 0x0024;
            if (msg == WM_GETMINMAXINFO)
            {
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO))!;

            const int MONITOR_DEFAULTTONEAREST = 0x00000002;
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            if (monitor != IntPtr.Zero)
            {
                var monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                GetMonitorInfo(monitor, ref monitorInfo);

                RECT rcWorkArea = monitorInfo.rcWork;
                RECT rcMonitorArea = monitorInfo.rcMonitor;

                mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.left - rcMonitorArea.left);
                mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.top - rcMonitorArea.top);
                mmi.ptMaxSize.x = Math.Abs(rcWorkArea.right - rcWorkArea.left);
                mmi.ptMaxSize.y = Math.Abs(rcWorkArea.bottom - rcWorkArea.top);
                mmi.ptMaxTrackSize.x = mmi.ptMaxSize.x;
                mmi.ptMaxTrackSize.y = mmi.ptMaxSize.y;
            }

            Marshal.StructureToPtr(mmi, lParam, true);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int left; public int top; public int right; public int bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }
    }

    // Modern input dialog
    public class InputDialog : Window
    {
        private TextBox _textBox;
        public string ResponseText => _textBox.Text;

        public InputDialog(string title, string prompt, string defaultValue = "")
        {
            Title = title;
            Width = 450;
            Height = 260;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            ResizeMode = ResizeMode.NoResize;

            // Main border with rounded corners
            var mainBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                CornerRadius = new CornerRadius(12),
                BorderBrush = new SolidColorBrush(Color.FromRgb(62, 62, 66)),
                BorderThickness = new Thickness(1)
            };

            var grid = new Grid { Margin = new Thickness(25) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Title
            var titleText = new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 15)
            };
            Grid.SetRow(titleText, 0);
            grid.Children.Add(titleText);

            // Prompt
            var promptText = new TextBlock
            {
                Text = prompt,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                Margin = new Thickness(0, 0, 0, 10),
                FontSize = 13
            };
            Grid.SetRow(promptText, 1);
            grid.Children.Add(promptText);

            // TextBox with modern style
            _textBox = new TextBox
            {
                Text = defaultValue,
                Padding = new Thickness(10, 0, 10, 0),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                CaretBrush = Brushes.White,
                SelectionBrush = new SolidColorBrush(Color.FromRgb(70, 130, 255)),
                SelectionOpacity = 0.5,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            // Rounded corners for textbox container
            var textBoxBorder = new Border
            {
                Child = _textBox,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(50, 50, 55)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 5, 0, 20),
                Height = 60,
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetRow(textBoxBorder, 2);
            grid.Children.Add(textBoxBorder);

            // Buttons panel
            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            // Cancel button
            var cancelButton = new Button
            {
                Content = "Zrušiť",
                Width = 100,
                Height = 36,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(62, 62, 66)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                Cursor = Cursors.Hand,
                IsCancel = true
            };

            var cancelTemplate = new ControlTemplate(typeof(Button));
            var cancelBorder = new FrameworkElementFactory(typeof(Border));
            cancelBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            cancelBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            cancelBorder.SetValue(Border.PaddingProperty, new Thickness(20, 8, 20, 8));
            var cancelPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            cancelPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cancelPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            cancelBorder.AppendChild(cancelPresenter);
            cancelTemplate.VisualTree = cancelBorder;
            cancelButton.Template = cancelTemplate;
            cancelButton.MouseEnter += (s, e) => cancelButton.Background = new SolidColorBrush(Color.FromRgb(80, 80, 80));
            cancelButton.MouseLeave += (s, e) => cancelButton.Background = new SolidColorBrush(Color.FromRgb(62, 62, 66));
            buttonsPanel.Children.Add(cancelButton);

            // OK button
            var okButton = new Button
            {
                Content = "OK",
                Width = 100,
                Height = 36,
                Background = new SolidColorBrush(Color.FromRgb(66, 133, 244)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                IsDefault = true
            };

            var okTemplate = new ControlTemplate(typeof(Button));
            var okBorder = new FrameworkElementFactory(typeof(Border));
            okBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            okBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            okBorder.SetValue(Border.PaddingProperty, new Thickness(20, 8, 20, 8));
            var okPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            okPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            okPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            okBorder.AppendChild(okPresenter);
            okTemplate.VisualTree = okBorder;
            okButton.Template = okTemplate;
            okButton.Click += (s, e) => DialogResult = true;
            okButton.MouseEnter += (s, e) => okButton.Background = new SolidColorBrush(Color.FromRgb(82, 149, 255));
            okButton.MouseLeave += (s, e) => okButton.Background = new SolidColorBrush(Color.FromRgb(66, 133, 244));
            buttonsPanel.Children.Add(okButton);

            Grid.SetRow(buttonsPanel, 3);
            grid.Children.Add(buttonsPanel);

            mainBorder.Child = grid;
            Content = mainBorder;
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            _textBox.SelectAll();
            _textBox.Focus();
        }
    }
}
