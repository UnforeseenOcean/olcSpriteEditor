﻿using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SPE.Engine;
using static SPE.Properties.Settings;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace SPE
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public Sprite LoadedSprite { get; set; }

        public readonly WindowDataContext WindowDataContext = new WindowDataContext();

        private bool _isLeftClickHeldDown;
        private bool _isRightClickHeldDown;

        private Colour _activeColour;
        private readonly Brush _gridColorBrush = new SolidColorBrush(Colors.White);
        private readonly Brush _colorBorder = new SolidColorBrush(Colors.LightGray);
        private readonly Brush _hoverBrush = new SolidColorBrush(Colors.Brown);
        private readonly Brush _activeColor = new SolidColorBrush(Colors.Red);

        private bool _firstBoot = true;

        public MainWindow()
        {
            /*
            if (Debugger.IsAttached)
                Default.Reset();
            */

            InitializeComponent();

            DataContext = WindowDataContext;

            LoadRecentsFilesList();

            WindowDataContext.ToggleCanvasGrid = Default.UseGridOnCanvas;

            LoadedSprite = new Sprite(10, 10, null, this);

            WindowDataContext.CurrentProgramStatus = "Loaded Empty Sprite";

            if (Default.ShowAllColours)
            {
                WindowDataContext.ModeAllColours = true;
                ColourHandler.SwapColours(true);
            }
            else
            {
                WindowDataContext.ModeSystemColours = true;
                ColourHandler.SwapColours(false);
            }

            CreateColorPalletWindow();

            WindowDataContext.PropertyChanged += WindowDataContextOnPropertyChanged;
            WindowDataContext.SpriteBlockSize =
                WindowDataContext.AllowedSpriteSizes.First(x => x.Size == Default.SpriteCellSize);

            for(var i = 0; i < WindowDataContext.AllowedSpriteSizes.Length; i++)
            {
                if (WindowDataContext.AllowedSpriteSizes[i].Size == WindowDataContext.SpriteBlockSize.Size)
                {
                    SpriteCellSize.SelectedIndex = i;
                    break;
                }
            }

            UpdateCanvas();
        }

        private void WindowDataContextOnPropertyChanged(object o, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            if (_firstBoot)
            {
                _firstBoot = false;
                return;
            }

            if (propertyChangedEventArgs.PropertyName.Equals(nameof(WindowDataContext.SpriteBlockSize)))
            {
                SpriteViewCanvas.Children.Clear();
                UpdateCanvas();

                if (Default.SpriteCellSize != WindowDataContext.SpriteBlockSize.Size)
                {
                    Default.SpriteCellSize = WindowDataContext.SpriteBlockSize.Size;
                    Default.Save();
                }
            }
        }

        private void CreateColorPalletWindow()
        {
            var size = 32;

            _activeColour = ColourHandler.ByHex("00000000", Pixal.PIXEL_SPACE);

            ColorViewCanvas.Width = ColorScrollViewer.Width;

            var width = (int)(ColorViewCanvas.Width / size);
            var height = (int)Math.Ceiling((decimal)ColourHandler.Colours.Count / width);
            ColorViewCanvas.Height = height * size;

            var colorIdx = 0;
            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width; i++)
                {
                    if (colorIdx == ColourHandler.Colours.Count)
                    {
                        break;
                    }

                    var c = ColourHandler.Colours[colorIdx];

                    var rect = new Rectangle
                    {
                        Width = size,
                        Height = size,
                        Fill = c.Brush,
                        Stroke = _colorBorder,
                        StrokeThickness = 1.2
                    };

                    rect.MouseEnter += (sender, args) =>
                    {
                        var brush = (SolidColorBrush)rect.Stroke;
                        if (Equals(brush.Color, Colors.Red)) return;

                        rect.Stroke = _hoverBrush;
                    };

                    rect.MouseLeave += (sender, args) =>
                    {
                        var brush = (SolidColorBrush)rect.Stroke;
                        if (Equals(brush.Color, Colors.Red)) return;

                        rect.Stroke = _colorBorder;
                    };

                    rect.MouseUp += (sender, args) =>
                    {
                        _isLeftClickHeldDown = false;
                        _isRightClickHeldDown = false;

                        var p = (Rectangle)sender;

                        p.Stroke = _activeColor;
                        _activeColour = c;

                        foreach (var child in ColorViewCanvas.Children)
                        {
                            var r = (Rectangle)child;
                            if (Equals(r, p)) continue;

                            if (!Equals(r.Stroke, _activeColor)) continue;
                            r.Stroke = _colorBorder;
                            break;

                        }
                    };

                    var tt = new ToolTip
                    {
                        Content = $"Hex: #{c.Hex}{Environment.NewLine}" +
                                  $"RGB: {c.R},{c.G},{c.B}" +
                                  $"{(c.A < 255 ? $"{Environment.NewLine}Transparent" : "")}{Environment.NewLine}" +
                                  $"IDX: {colorIdx}{Environment.NewLine}" +
                                  $"Pixal: {(char)c.Pixal}",
                        Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255))
                    };

                    rect.ToolTip = tt;

                    if (_activeColour != null && _activeColour.Hex == c.Hex)
                    {
                        rect.Stroke = _activeColor;
                    }

                    ColorViewCanvas.Children.Add(rect);
                    Canvas.SetLeft(rect, i * size);
                    Canvas.SetTop(rect, j * size);

                    colorIdx++;
                }
            }
        }

        private void UpdateCanvas()
        {
            SpriteViewCanvas.IsEnabled = false;

            SpriteViewCanvas.Width = LoadedSprite.Width * WindowDataContext.SpriteBlockSize.Size;
            SpriteViewCanvas.Height = LoadedSprite.Height * WindowDataContext.SpriteBlockSize.Size;
                
            for (var column = 0; column < LoadedSprite.Height; column++)
            {
                for (var row = 0; row < LoadedSprite.Width; row++)
                {
                    var row1 = row;
                    var column1 = column;

                    var c = LoadedSprite.GetColour(row, column);
                    var g = LoadedSprite.GetGlyph(row, column);                    
                    var correctColor = ColourHandler.ByCode(c, (Pixal)g);

                    if(correctColor == null) continue;
                    
                    var rect = new Rectangle
                    {
                        Width = WindowDataContext.SpriteBlockSize.Size,
                        Height = WindowDataContext.SpriteBlockSize.Size,
                        Fill = correctColor.Brush,    
                        StrokeThickness = 2
                    };

                    if (Default.UseGridOnCanvas)
                    {
                        rect.Stroke = _gridColorBrush;
                    }

                    rect.MouseEnter += (sender, args) =>
                    {
                        var rr = (Rectangle) sender;
                        var ca = _activeColour;
                        if (_isRightClickHeldDown)
                        {
                            ca = ColourHandler.Colours[0];
                        }

                        if (_isLeftClickHeldDown || _isRightClickHeldDown)
                        {
                            UpdateRect(rr, row1, column1, ca);
                        }

                        Colour rectColour = GetColourFromRect(rr);
                        rr.ToolTip = new ToolTip();

                        var rrT = (ToolTip) rr.ToolTip;
                        rrT.Content += $"POS: {rr.Tag}" +
                                       $"{Environment.NewLine}Hex: #{rectColour.Hex}" +
                                       $"{Environment.NewLine}Pixal: {(char)rectColour.Pixal}";
                        rect.Stroke = _hoverBrush;
                    };

                    rect.MouseLeave += (sender, args) =>
                    {
                        var rr = (Rectangle)sender;
                        rr.ToolTip = null;

                        if (Default.UseGridOnCanvas)
                        {
                            rect.Stroke = _gridColorBrush;
                            return;
                        }

                        rect.Stroke = null;
                    };

                    rect.MouseUp += (sender, args) =>
                    {
                        if (args.LeftButton == MouseButtonState.Released)
                        {
                            _isLeftClickHeldDown = false;
                            WindowDataContext.CurrentSystemTool = "";
                        }

                        if (args.RightButton == MouseButtonState.Released)
                        {
                            _isRightClickHeldDown = false;
                            WindowDataContext.CurrentSystemTool = "";
                        }
                    };

                    rect.MouseDown += (sender, args) =>
                    {
                        var ca = _activeColour;
                        if (args.LeftButton == MouseButtonState.Pressed)
                        {
                            _isRightClickHeldDown = false;
                            _isLeftClickHeldDown = true;
                            WindowDataContext.CurrentSystemTool = "Dragging Mode";

                        }
                        else if (args.RightButton == MouseButtonState.Pressed)
                        {
                            _isLeftClickHeldDown = false;
                            _isRightClickHeldDown = true;
                            WindowDataContext.CurrentSystemTool = "Erasing Mode";
                            ca = ColourHandler.Colours[0];
                        }
                        
                        UpdateRect((Rectangle)sender, row1, column1, ca);
                    };

                    rect.Tag = $"{(column + 1)}, {(row + 1)}";

                    SpriteViewCanvas.Children.Add(rect);
                    Canvas.SetTop(rect, column * WindowDataContext.SpriteBlockSize.Size);
                    Canvas.SetLeft(rect, row * WindowDataContext.SpriteBlockSize.Size);
                }
            }

            SpriteViewCanvas.IsEnabled = true;
        }

        private Colour GetColourFromRect(Rectangle rr)
        {
            if (!(rr.Fill is SolidColorBrush s))
            {
                return ColourHandler.Colours[0];
            }

            return ColourHandler.ByRgb(s.Color.R, s.Color.B, s.Color.G, s.Color.A);
        }

        private void UpdateRect(Rectangle rect, int i1, int j1, Colour c = null)
        {
            if (c == null)
            {
                c = _activeColour;
            }

            if (c.A < 255)
            {
                c = ColourHandler.Colours[0];
            } 

            rect.Fill = c.Brush;
            LoadedSprite.SetColour(i1, j1, c);
            LoadedSprite.SetGlyph(i1, j1, c.Pixal);
        }

        private void FileOptionClicked(object sender, ExecutedRoutedEventArgs e)
        {
            var command = ((RoutedCommand)e.Command).Name;

            switch (command)
            {
                case "SaveSprite":
                    if (string.IsNullOrEmpty(LoadedSprite.File))
                    {
                        SaveSpriteAs();
                        return;
                    }
                    LoadedSprite.Save();
                    break;
                case "SaveAsSprite":
                    SaveSpriteAs();
                    break;
                case "OpenSprite":
                    var ofd = new OpenFileDialog
                    {
                        Title = "Open Sprite File",
                        Multiselect = false,
                        Filter = "Sprite File (*.spr)|*.spr"
                    };

                    if (ofd.ShowDialog() == true)
                    {
                        LoadedSprite = new Sprite(ofd.FileName, this);
                        if (!LoadedSprite.FailedToLoad)
                        {
                            SpriteViewCanvas.Children.Clear();
                            SaveToRecentsList(ofd.FileName);
                            WindowDataContext.CurrentProgramStatus = $"Loaded: {Path.GetFileName(ofd.FileName)}";
                            UpdateCanvas();
                        }
                        else
                        {
                            LoadedSprite = new Sprite(10, 10, null, this);
                            SpriteViewCanvas.Children.Clear();
                            UpdateCanvas();
                        }

#if DEBUG
                        Console.WriteLine(LoadedSprite);
#endif
                    }

                    break;
                case "ToggleCanvasGrid":
                case "ToggleGridView":
                    ToggleSpriteGrid();
                    break;
                case "ExportSprite":
                    ExportSpriteAsImage();
                    break;
                case "NewSprite":
                    CreateNewSprite();
                    break;
                case "ShowSystemColours":
                    Default.ShowAllColours = false;
                    Default.Save();

                    WindowDataContext.ModeAllColours = false;
                    WindowDataContext.ModeSystemColours = true;

                    ColourHandler.SwapColours(Default.ShowAllColours);
                    ColorViewCanvas.Children.Clear();
                    CreateColorPalletWindow();
                    break;
                case "ShowAllColours":
                    Default.ShowAllColours = true;
                    Default.Save();

                    WindowDataContext.ModeAllColours = true;
                    WindowDataContext.ModeSystemColours = false;

                    ColourHandler.SwapColours(Default.ShowAllColours);
                    ColorViewCanvas.Children.Clear();
                    CreateColorPalletWindow();
                    break;
                case "ResetSystemSettings":
                    var msgBox = MessageBox.Show("Are you sure you wish to reset the application settings",
                        "Are you sure?", MessageBoxButton.YesNo, MessageBoxImage.Error);
                    if (msgBox == MessageBoxResult.Yes)
                    {
                        Default.Reset();
                        Application.Current.Exit += delegate
                        {
                            Process.Start(Application.ResourceAssembly.Location);
                        };
                        Application.Current.Shutdown();

                    }
                    break;
            }
        }

        private void CreateNewSprite()
        {
            var newSpriteDialog = new Dialogs.NewSpriteDialog(this) {Owner = this};

            if (newSpriteDialog.ShowDialog() == true)
            {
                SpriteViewCanvas.Children.Clear();
                UpdateCanvas();
            }
        }

        private void ExportSpriteAsImage()
        {
            var sfd = new SaveFileDialog
            {
                Title = "Export Sprite File as PNG",
                Filter = "PNG File (*.png)|*.png|JPG File (*.jpg)|*.jpg",
                FileName = "Export",
                AddExtension = true
            };

            if (sfd.ShowDialog() == true)
            {
                var file = sfd.FileName;

                var flag = new Bitmap(LoadedSprite.Width * WindowDataContext.SpriteBlockSize.Size, LoadedSprite.Height * WindowDataContext.SpriteBlockSize.Size);
                flag.SetResolution(100, 100);
                var flagGraphics = Graphics.FromImage(flag);

                for (var i = 0; i < LoadedSprite.Width; i++)
                {
                    for (var j = 0; j < LoadedSprite.Height; j++)
                    {
                        var c = LoadedSprite.GetColour(i, j);
                        var correctColor = ColourHandler.ByCode(c);

                        if(correctColor == null) continue;

                        flagGraphics.FillRectangle(correctColor.Color.ToSolidBrush(), 
                            new RectangleF(i * WindowDataContext.SpriteBlockSize.Size, 
                                j * WindowDataContext.SpriteBlockSize.Size, 
                                WindowDataContext.SpriteBlockSize.Size, 
                                WindowDataContext.SpriteBlockSize.Size));
                    }
                }

                switch (Path.GetExtension(file).Split('.').Last().ToLower())
                {
                    case "png":
                        flag.Save(file, ImageFormat.Png);
                        break;
                    case "jpg":
                        flag.Save(file, ImageFormat.Jpeg);
                        break;
                }

                WindowDataContext.CurrentProgramStatus = $"Exported: {Path.GetFileName(file)}";
            }
        }

        private void SaveToRecentsList(string file)
        {
            if (Default.RecentFiles.Contains(file)) return;

            Default.RecentFiles.Insert(0, file);

            if (Default.RecentFiles.Count > 6)
            {
                Default.RecentFiles.RemoveAt(Default.RecentFiles.Count - 1);
            }

            RecentFilesList.Items.Insert(0, CreateRecentItem(file));
            if (RecentFilesList.Items.Count > 6)
            {
                RecentFilesList.Items.RemoveAt(RecentFilesList.Items.Count - 1);
            }

            Default.Save();
        }

        private void SaveSpriteAs()
        {
            var sfd = new SaveFileDialog
            {
                Title = "Save Sprite File",
                Filter = "Sprite File (*.spr)|*.spr",
                FileName = "Default.spr",
                AddExtension = true
            };

            if (sfd.ShowDialog() != true) return;
            var file = sfd.FileName;

            SaveToRecentsList(file);
            LoadedSprite.Save(file);
            WindowDataContext.CurrentProgramStatus = $"Saved: {Path.GetFileName(file)}";
        }

        private void ToggleSpriteGrid()
        {
            WindowDataContext.ToggleCanvasGrid = !Default.UseGridOnCanvas;
            Default.UseGridOnCanvas = WindowDataContext.ToggleCanvasGrid;

            if (Default.UseGridOnCanvas)
            {
                foreach (var child in SpriteViewCanvas.Children)
                {
                    var rect = (Rectangle) child;
                    rect.Stroke = _gridColorBrush;
                }
            }
            else
            {
                foreach (var child in SpriteViewCanvas.Children)
                {
                    var rect = (Rectangle)child;
                    var brush = (SolidColorBrush) rect.Stroke;
                    if (brush.Color == ((SolidColorBrush) _hoverBrush).Color) continue;

                    rect.Stroke = null;
                }
            }

            Default.Save();
        }

        private void LoadRecentsFilesList()
        {
            if (Default.RecentFiles == null)
            {
                Default.RecentFiles = new StringCollection();
                Default.Save();
            }

            foreach (var item in Default.RecentFiles)
            {
                RecentFilesList.Items.Insert(0, CreateRecentItem(item));
            }
        }

        private MenuItem CreateRecentItem(string item)
        {
            var mItem = new MenuItem
            {
                Header = Path.GetFileName(item),
                Tag = item
            };

            mItem.Click += (sender, args) =>
            {
                var file = ((MenuItem) sender).Tag.ToString();

                if (!File.Exists(file))
                {
                    RecentFilesList.Items.Remove(sender);
                    MessageBox.Show($"{file} no longer exist", "Sprite Loading Error", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                LoadedSprite = new Sprite(file, this);

                if (LoadedSprite.FailedToLoad)
                {
                    RecentFilesList.Items.Remove(sender);
                    LoadedSprite = new Sprite(10, 10, null, this);
                }
                else
                {
                    WindowDataContext.CurrentProgramStatus = $"Loaded: {Path.GetFileName(file)}";
                }

                SpriteViewCanvas.Children.Clear();
                UpdateCanvas();
            };

            return mItem;
        }
    }
}
