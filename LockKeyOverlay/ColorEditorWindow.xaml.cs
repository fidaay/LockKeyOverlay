using System.Windows;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;

namespace LockKeyOverlay
{
    public partial class ColorEditorWindow : Window
    {
        private bool _updatingControls;
        private bool _hexValid = true;
        private byte? _explicitHexAlpha;

        public byte SelectedR { get; private set; }
        public byte SelectedG { get; private set; }
        public byte SelectedB { get; private set; }
        public byte SelectedA { get; private set; }

        public ColorEditorWindow(string title, byte r, byte g, byte b, byte a)
        {
            InitializeComponent();

            Title = title;

            _updatingControls = true;

            RedSlider.Value = r;
            GreenSlider.Value = g;
            BlueSlider.Value = b;
            OpacitySlider.Value = Math.Round(a * 100.0 / 255.0);

            _updatingControls = false;

            ApplyUiFromSliders(updateHex: true);
        }

        private void AnySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updatingControls)
                return;

            _explicitHexAlpha = null;
            ApplyUiFromSliders(updateHex: true);
        }

        private void HexTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_updatingControls)
                return;

            HandleHexLiveChange();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateHexForAccept())
            {
                return;
            }

            SelectedR = (byte)Math.Round(RedSlider.Value);
            SelectedG = (byte)Math.Round(GreenSlider.Value);
            SelectedB = (byte)Math.Round(BlueSlider.Value);
            SelectedA = ResolveSelectedAlpha(_explicitHexAlpha, OpacitySlider.Value);

            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ApplyUiFromSliders(bool updateHex)
        {
            byte r = (byte)Math.Round(RedSlider.Value);
            byte g = (byte)Math.Round(GreenSlider.Value);
            byte b = (byte)Math.Round(BlueSlider.Value);
            byte a = ResolveSelectedAlpha(_explicitHexAlpha, OpacitySlider.Value);

            RedValueText.Text = r.ToString();
            GreenValueText.Text = g.ToString();
            BlueValueText.Text = b.ToString();

            double opacity = OpacitySlider.Value / 100.0;
            OpacityValueText.Text = $"{opacity:0.00}";
            OpacityInfoText.Text = $"{opacity:0.00} ({OpacitySlider.Value:0}%)";

            if (updateHex)
            {
                _updatingControls = true;
                HexTextBox.Text = $"#{r:X2}{g:X2}{b:X2}";
                _updatingControls = false;

                SetHexStatusNeutral();
            }

            UpdatePreview(r, g, b, a);
        }

        private void UpdatePreview(byte r, byte g, byte b, byte a)
        {
            PreviewSampleBorder.Background = new SolidColorBrush(WpfColor.FromArgb(a, r, g, b));

            double luminance = (0.299 * r) + (0.587 * g) + (0.114 * b);
            PreviewSampleText.Foreground = luminance > 150 ? WpfBrushes.Black : WpfBrushes.White;
        }

        private void HandleHexLiveChange()
        {
            string input = (HexTextBox.Text ?? string.Empty).Trim();
            string raw = input.StartsWith("#") ? input[1..] : input;

            if (string.IsNullOrWhiteSpace(raw))
            {
                _explicitHexAlpha = null;
                _hexValid = true;
                SetHexStatusNeutral();
                return;
            }

            // Mientras escribe, no molestamos si está incompleto
            if (raw.Length < 6 || raw.Length == 7)
            {
                _explicitHexAlpha = null;
                _hexValid = false;
                SetHexStatus("HEX incompleto...", WpfBrushes.DarkGoldenrod);
                return;
            }

            if (raw.Length != 6 && raw.Length != 8)
            {
                _explicitHexAlpha = null;
                _hexValid = false;
                SetHexStatus("HEX inválido", WpfBrushes.Firebrick);
                return;
            }

            if (!ColorHexParser.IsHexString(raw))
            {
                _explicitHexAlpha = null;
                _hexValid = false;
                SetHexStatus("HEX inválido", WpfBrushes.Firebrick);
                return;
            }

            if (!ColorHexParser.TryParse(input, out ParsedHexColor parsed))
            {
                _explicitHexAlpha = null;
                _hexValid = false;
                SetHexStatus("HEX inválido", WpfBrushes.Firebrick);
                return;
            }

            _hexValid = true;
            SetHexStatus(raw.Length == 8 ? "HEX aplicado con alpha" : "HEX aplicado", WpfBrushes.SeaGreen);

            _updatingControls = true;

            RedSlider.Value = parsed.R;
            GreenSlider.Value = parsed.G;
            BlueSlider.Value = parsed.B;

            if (parsed.A.HasValue)
            {
                _explicitHexAlpha = parsed.A.Value;
                double opacityPercent = Math.Round((parsed.A.Value / 255.0) * 100.0);
                opacityPercent = Math.Max(0, Math.Min(100, opacityPercent));
                OpacitySlider.Value = opacityPercent;
            }
            else
            {
                _explicitHexAlpha = null;
            }

            _updatingControls = false;

            ApplyUiFromSliders(updateHex: false);
        }

        private bool ValidateHexForAccept()
        {
            string input = (HexTextBox.Text ?? string.Empty).Trim();
            string raw = input.StartsWith("#") ? input[1..] : input;

            if (string.IsNullOrWhiteSpace(raw))
                return true;

            if ((raw.Length != 6 && raw.Length != 8) || !ColorHexParser.IsHexString(raw))
            {
                WpfMessageBox.Show(
                    "El valor HEX no es válido. Usa #RRGGBB o #RRGGBBAA.",
                    "Color inválido",
                    WpfMessageBoxButton.OK,
                    WpfMessageBoxImage.Warning);

                return false;
            }

            return _hexValid;
        }

        internal static byte ResolveSelectedAlpha(byte? explicitHexAlpha, double opacityPercent)
        {
            return explicitHexAlpha ?? AlphaFromOpacityPercent(opacityPercent);
        }

        private static byte AlphaFromOpacityPercent(double opacityPercent)
        {
            opacityPercent = Math.Max(0.0, Math.Min(100.0, opacityPercent));
            return (byte)Math.Round((opacityPercent / 100.0) * 255.0);
        }

        private void SetHexStatusNeutral()
        {
            SetHexStatus("Formato: #RRGGBB o #RRGGBBAA", WpfBrushes.Gray);
        }

        private void SetHexStatus(string text, WpfBrush color)
        {
            HexStatusText.Text = text;
            HexStatusText.Foreground = color;
        }

    }
}
