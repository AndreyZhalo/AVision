using Microsoft.Win32;
using System.Windows;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using System.IO;

namespace AVision
{
    public partial class MainWindow : System.Windows.Window
    {
        private Mat referenceMat;
        private Mat testMat;
        private Mat differenceMat;

        public MainWindow()
        {
            InitializeComponent();
            ThresholdSlider.ValueChanged += ThresholdSlider_ValueChanged;
            UpdateThresholdDisplay();
        }

        private void LoadReferenceBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadImage(true);
        }

        private void LoadTestBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadImage(false);
        }

        private void LoadImage(bool isReference)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.jpg; *.jpeg; *.png; *.bmp)|*.jpg; *.jpeg; *.png; *.bmp|All files (*.*)|*.*",
                Title = "Выберите изображение"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var imagePath = openFileDialog.FileName;
                    var mat = new Mat(imagePath);

                    if (isReference)
                    {
                        referenceMat = mat;
                        ReferenceImage.Source = ConvertMatToBitmapImage(mat);
                        ReferenceInfo.Text = $"{Path.GetFileName(imagePath)} - {mat.Width}x{mat.Height}";
                    }
                    else
                    {
                        testMat = mat;
                        TestImage.Source = ConvertMatToBitmapImage(mat);
                        TestInfo.Text = $"{Path.GetFileName(imagePath)} - {mat.Width}x{mat.Height}";
                        DifferenceOverlay.Source = null;
                        ResultInfo.Text = "";
                    }

                    StatusText.Text = "Изображение загружено";
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки изображения: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CompareBtn_Click(object sender, RoutedEventArgs e)
        {
            if (referenceMat == null || testMat == null)
            {
                MessageBox.Show("Сначала загрузите оба изображения", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                StatusText.Text = "Выполняется сравнение...";
                ProgressBar.Visibility = Visibility.Visible;

                // Основная логика сравнения
                differenceMat = CompareImages(referenceMat, testMat, (int)ThresholdSlider.Value);

                // Отображение результата
                DifferenceOverlay.Source = ConvertMatToBitmapImage(differenceMat);

                // Анализ результатов
                AnalyzeResults(differenceMat);

                StatusText.Text = "Сравнение завершено";
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Ошибка при сравнении: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Ошибка сравнения";
            }
            finally
            {
                ProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private Mat CompareImages(Mat reference, Mat test, int thresholdValue)
        {
            // Приведение к одинаковому размеру
            if (reference.Size() != test.Size())
            {
                Cv2.Resize(test, test, reference.Size());
            }

            // Вычисление разницы
            Mat diff = new Mat();
            Cv2.Absdiff(reference, test, diff);

            // Преобразование в оттенки серого
            Mat grayDiff = new Mat();
            if (diff.Channels() == 3)
            {
                Cv2.CvtColor(diff, grayDiff, ColorConversionCodes.BGR2GRAY);
            }
            else
            {
                grayDiff = diff;
            }

            // Пороговая обработка
            Mat thresholdMask = new Mat();
            Cv2.Threshold(grayDiff, thresholdMask, thresholdValue, 255, ThresholdTypes.Binary);

            // Создание цветного наложения (красным цветом)
            Mat coloredDiff = new Mat();
            Cv2.CvtColor(thresholdMask, coloredDiff, ColorConversionCodes.GRAY2BGR);
            coloredDiff.SetTo(new Scalar(0, 0, 255), thresholdMask); // Красный цвет для различий

            return coloredDiff;
        }

        private void AnalyzeResults(Mat difference)
        {
            // Подсчет пикселей с различиями
            int totalPixels = difference.Width * difference.Height;
            Mat gray = new Mat();
            Cv2.CvtColor(difference, gray, ColorConversionCodes.BGR2GRAY);
            int diffPixels = Cv2.CountNonZero(gray);

            double diffPercentage = (diffPixels / (double)totalPixels) * 100;

            ResultInfo.Text = $"Различия: {diffPixels} пикселей ({diffPercentage:F2}%)";

            // Цветовая индикация результата
            if (diffPercentage < 1.0)
                ResultInfo.Foreground = System.Windows.Media.Brushes.Green;
            else if (diffPercentage < 5.0)
                ResultInfo.Foreground = System.Windows.Media.Brushes.Orange;
            else
                ResultInfo.Foreground = System.Windows.Media.Brushes.Red;
        }

        private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateThresholdDisplay();

            // Автоматическое обновление сравнения при изменении порога
            if (referenceMat != null && testMat != null && DifferenceOverlay.Source != null)
            {
                CompareBtn_Click(null, null);
            }
        }

        private void UpdateThresholdDisplay()
        {
            ThresholdValue.Text = ((int)ThresholdSlider.Value).ToString();
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            referenceMat = null;
            testMat = null;
            differenceMat = null;

            ReferenceImage.Source = null;
            TestImage.Source = null;
            DifferenceOverlay.Source = null;

            ReferenceInfo.Text = "Изображение не загружено";
            TestInfo.Text = "Изображение не загружено";
            ResultInfo.Text = "";
            StatusText.Text = "Готов к работе";
        }

        private BitmapImage ConvertMatToBitmapImage(Mat mat)
        {
            using (var memoryStream = new MemoryStream())
            {
                var imageBytes = mat.ToBytes();
                memoryStream.Write(imageBytes, 0, imageBytes.Length);
                memoryStream.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            }
        }
    }
}