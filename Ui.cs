using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Markup;

namespace FolderBloom
{
    internal static class Ui
    {
        internal static readonly Brush Back = Brush("#121720"), Card = Brush("#1C2330"), BorderColor = Brush("#303B4E"), Muted = Brush("#A4B0C5"), TextColor = Brush("#F3F6FC"), Accent = Brush("#9BCABC");
        internal static Brush Brush(string value) { var b = (Brush)new BrushConverter().ConvertFromString(value); b.Freeze(); return b; }
        internal static TextBlock Text(string text, double size, Brush color)
        {
            return new TextBlock { Text = text, FontSize = size, Foreground = color, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Segoe UI") };
        }
        internal static Button Button(string text, Action action, bool primary)
        {
            var b = new Button { Content = text, Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0), Cursor = Cursors.Hand, Foreground = primary ? Back : TextColor, Background = primary ? Accent : Card, BorderBrush = BorderColor, BorderThickness = new Thickness(1), FontSize = 13, MinHeight = 34 };
            b.Click += delegate { action(); }; return b;
        }
        internal static Image FolderImage(string color, double size) { return new Image { Source = Icons.Folder(color), Width = size, Height = size, Stretch = Stretch.Uniform }; }
        internal static MenuItem Menu(string label, Action action) { var m = new MenuItem { Header = label }; m.Click += delegate { action(); }; return m; }
        internal static Border Box(UIElement child, int padding)
        {
            return new Border { Background = Card, BorderBrush = BorderColor, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(padding), Child = child };
        }
        internal static void InstallStyles(Application app)
        {
            const string xaml = @"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
<Style TargetType='Button'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='surface' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='7' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='surface' Property='Opacity' Value='0.78'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='surface' Property='BorderBrush' Value='#A8DDCE'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='surface' Property='Opacity' Value='0.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='TextBox'><Setter Property='Background' Value='#10151E'/><Setter Property='Foreground' Value='#F3F6FC'/><Setter Property='CaretBrush' Value='#F3F6FC'/><Setter Property='BorderBrush' Value='#3B475C'/><Setter Property='Padding' Value='10,7'/><Setter Property='FontSize' Value='14'/><Setter Property='VerticalContentAlignment' Value='Center'/></Style>
<Style TargetType='ToolTip'><Setter Property='Background' Value='#243044'/><Setter Property='Foreground' Value='#F3F6FC'/></Style>
</ResourceDictionary>";
            app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(xaml));
        }
        internal static void SetupWindow(Window window, string title, double width, double height)
        {
            window.Title = title; window.Width = width; window.Height = height; window.Background = Back; window.Foreground = TextColor;
            window.FontFamily = new FontFamily("Segoe UI"); window.FontSize = 14; window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.Icon = Icons.Folder("#79B9A5");
        }
        internal static void Error(Window owner, string message) { MessageBox.Show(owner, message, "FolderBloom", MessageBoxButton.OK, MessageBoxImage.Information); }
    }
}
