using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;

using Emgu;
using Emgu.CV.CvEnum;
using Emgu.CV;
using Emgu.Util;
using Emgu.CV.Util;
using Emgu.CV.Structure;

namespace FaceRec
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private VideoCapture capture;
        private string savePath = @"C:\Users\MooN\source\repos\FaceRec\FaceRec\image.jpg";
        public MainWindow()
        {
            InitializeComponent();

            if (Uri.IsWellFormedUriString(faceEndpoint, UriKind.Absolute))
            {
                faceClient.Endpoint = faceEndpoint;
            }
            else
            {
                MessageBox.Show(faceEndpoint,
                    "Invalid URI", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(0);
            }
        }

        // Добовление ключа подписки.
        private static string subscriptionKey = "5e3465ac18d14496813f3ddcbee3d982";
        // Добавление конечной точки.
        private static string faceEndpoint = "https://testfacerec-emgucv.cognitiveservices.azure.com/";


        // Создание экземпляра FaceClient
        private readonly IFaceClient faceClient = new FaceClient(
            new ApiKeyServiceClientCredentials(subscriptionKey),
            new System.Net.Http.DelegatingHandler[] { });

        // Список обнаруженных лиц
        private IList<DetectedFace> faceList;
        // Список описаний для найденных лиц
        private string[] faceDescriptions;
        // Скейлинг фото относительно размера
        private double resizeFactor;
        // Надпись по умолчанию для статус бара
        private const string defaultStatusBarText = "Place the mouse pointer over a face to see the face description.";

        // Отображает изображение и вызывает метод UploadAndDetectFaces.
        private async void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            // Начало получение изображения с камеры.
            if (capture == null)
            {
                try
                {
                    // Создание экземляра захвата изображения
                    capture = new VideoCapture(0);
                }
                catch(NullReferenceException ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
            if(capture != null)
            {
                try
                {
                    Image<Bgr, double> capturedImage = capture.QueryFrame().ToImage<Bgr, double>();
                    capturedImage.Save(@"C:\Users\MooN\source\repos\FaceRec\FaceRec\image.jpg");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }

            BitmapImage bitmapSource = new BitmapImage();

            bitmapSource.BeginInit();
            bitmapSource.CacheOption = BitmapCacheOption.None;
            bitmapSource.UriSource = new Uri(@"C:\Users\MooN\source\repos\FaceRec\FaceRec\image.jpg");
            bitmapSource.EndInit();

            FacePhoto.Source = bitmapSource;

            // Обнаружение лиц на полученном изображении.
            Title = "Detecting...";
            faceList = await UploadAndDetectFaces(savePath);
            Title = String.Format(
                "Detection Finished. {0} face(s) detected", faceList.Count);

            if (faceList.Count > 0)
            {
                // Подготовка отрисовки прямоугольников на изображении.
                DrawingVisual visual = new DrawingVisual();
                DrawingContext drawingContext = visual.RenderOpen();
                drawingContext.DrawImage(bitmapSource,
                    new Rect(0, 0, bitmapSource.Width, bitmapSource.Height));
                double dpi = bitmapSource.DpiX;
                resizeFactor = (dpi == 0) ? 1 : 96 / dpi;
                faceDescriptions = new String[faceList.Count];

                for (int i = 0; i < faceList.Count; ++i)
                {
                    DetectedFace face = faceList[i];

                    // Отображение прямоугольников вокруг лица.
                    drawingContext.DrawRectangle(
                        Brushes.Transparent,
                        new Pen(Brushes.Red, 2),
                        new Rect(
                            face.FaceRectangle.Left * resizeFactor,
                            face.FaceRectangle.Top * resizeFactor,
                            face.FaceRectangle.Width * resizeFactor,
                            face.FaceRectangle.Height * resizeFactor
                            )
                    );

                    // Сохранение описаний лица.
                    faceDescriptions[i] = FaceDescription(face);
                }

                drawingContext.Close();

                // Отображение изображения с нарисованным прямоугольником.
                RenderTargetBitmap faceWithRectBitmap = new RenderTargetBitmap(
                    (int)(bitmapSource.PixelWidth * resizeFactor),
                    (int)(bitmapSource.PixelHeight * resizeFactor),
                    96,
                    96,
                    PixelFormats.Pbgra32);

                faceWithRectBitmap.Render(visual);
                FacePhoto.Source = faceWithRectBitmap;

                // Установка текста в StatusBar.
                faceDescriptionStatusBar.Text = defaultStatusBarText;
            }
        }
        // Метод отображения информации о лице при наведении на прямоугольник.
        private void FacePhoto_MouseMove(object sender, MouseEventArgs e)
        {
            if (faceList == null)
                return;

            // Получение координат мыши относительно изображения.
            Point mouseXY = e.GetPosition(FacePhoto);

            ImageSource imageSource = FacePhoto.Source;
            BitmapSource bitmapSource = (BitmapSource)imageSource;

            // Регулировка масштаба с помощью resizeFactor.
            var scale = FacePhoto.ActualWidth / (bitmapSource.PixelWidth / resizeFactor);

            // Проверка, находится ли курсор над прямоугольником
            bool mouseOverFace = false;

            for (int i = 0; i < faceList.Count; ++i)
            {
                FaceRectangle fr = faceList[i].FaceRectangle;
                double left = fr.Left * scale;
                double top = fr.Top * scale;
                double width = fr.Width * scale;
                double height = fr.Height * scale;

                // Отображение описание если мышь находится над прямоугольником
                if (mouseXY.X >= left && mouseXY.X <= left + width &&
                    mouseXY.Y >= top && mouseXY.Y <= top + height)
                {
                    faceDescriptionStatusBar.Text = faceDescriptions[i];
                    mouseOverFace = true;
                    break;
                }
            }

            // Строка которая будет отображатся если мышь не находится над прямоугольником
            if (!mouseOverFace) faceDescriptionStatusBar.Text = defaultStatusBarText;
        }
        // Загружает изображение и вызывает метод DetectWithStreamAsync.
        private async Task<IList<DetectedFace>> UploadAndDetectFaces(string imageFilePath)
        {
            // Список атрибутов лица, которые будут возвращены в statusBar.
            IList<FaceAttributeType?> faceAttributes =
                new FaceAttributeType?[]
                {
            FaceAttributeType.Gender, FaceAttributeType.Age,
            FaceAttributeType.Emotion
                };

            // Вызов API определения лиц.
            try
            {
                using (Stream imageFileStream = File.OpenRead(imageFilePath))
                {
                    IList<DetectedFace> faceList =
                        await faceClient.Face.DetectWithStreamAsync(
                            imageFileStream, true, false, faceAttributes);
                    return faceList;
                }
            }
            // Поимка и отображение ошибок Face API.
            catch (APIErrorException f)
            {
                MessageBox.Show(f.Message);
                return new List<DetectedFace>();
            }
            // Catch and display all other errors.
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error");
                return new List<DetectedFace>();
            }
        }
        // Метод создания строки для отображения в statusBar`е.
        private string FaceDescription(DetectedFace face)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("Face: ");

            // Добавление пола и количества лет.
            sb.Append(face.FaceAttributes.Gender);
            sb.Append(", ");
            sb.Append(face.FaceAttributes.Age);
            sb.Append(", ");

            // Добавление эмоций.
            sb.Append("Emotion: ");
            Emotion emotionScores = face.FaceAttributes.Emotion;
            if (emotionScores.Anger >= 0.1f) sb.Append(
                String.Format("anger {0:F1}%, ", emotionScores.Anger * 100));
            if (emotionScores.Contempt >= 0.1f) sb.Append(
                String.Format("contempt {0:F1}%, ", emotionScores.Contempt * 100));
            if (emotionScores.Disgust >= 0.1f) sb.Append(
                String.Format("disgust {0:F1}%, ", emotionScores.Disgust * 100));
            if (emotionScores.Fear >= 0.1f) sb.Append(
                String.Format("fear {0:F1}%, ", emotionScores.Fear * 100));
            if (emotionScores.Happiness >= 0.1f) sb.Append(
                String.Format("happiness {0:F1}%, ", emotionScores.Happiness * 100));
            if (emotionScores.Neutral >= 0.1f) sb.Append(
                String.Format("neutral {0:F1}%, ", emotionScores.Neutral * 100));
            if (emotionScores.Sadness >= 0.1f) sb.Append(
                String.Format("sadness {0:F1}%, ", emotionScores.Sadness * 100));
            if (emotionScores.Surprise >= 0.1f) sb.Append(
                String.Format("surprise {0:F1}%, ", emotionScores.Surprise * 100));

            // Возврат строки.
            return sb.ToString();
        }
    }
}
