using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Toolkit.Uwp.Helpers.CameraHelper;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.FaceAnalysis;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace AlarmML
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        CNTKGraphModel model;
        FaceDetector faceDetector;
        List<string> labels;
        int currentEmotionIndex;

        DispatcherTimer timer;
        bool alarmOn = true;

        DateTime? lastTimeEmotionMatched;

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            // load Model
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///emotion_ferplus.onnx"));
            model = await CNTKGraphModel.CreateCNTKGraphModel(file);

            labels = new List<string>()
            {
                "Neutral",
                "Happiness",
                "Surprise",
                "Sadness",
                "Anger",
                "Disgust",
                "Fear",
                "Contempt"
            };

            Random random = new Random();
            currentEmotionIndex = random.Next(labels.Count);
            EmotionText.Text = $"Show {labels[currentEmotionIndex]} to Snooze";

            faceDetector = await FaceDetector.CreateAsync();

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(300);
            timer.Tick += Timer_Tick;
            timer.Start();

            camera.FrameArrived += Preview_FrameArrived;
        }

        // decide if the alarm should be on
        private void Timer_Tick(object sender, object e)
        {
            if (alarmOn)
            {
                Alarmbackground.Visibility = Alarmbackground.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
            }

            TimeText.Text = DateTime.Now.ToShortTimeString();
        }

        // get frame and analyze
        private async void Preview_FrameArrived(object sender, FrameEventArgs e)
        {
            if (!alarmOn)
            {
                return;
            }

            var bitmap = e.VideoFrame.SoftwareBitmap;
            if (bitmap == null)
            {
                return;
            }

            // faceDector requires Gray8 or Nv12
            var convertedBitmap = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Gray8);
            var faces = await faceDetector.DetectFacesAsync(convertedBitmap);

            // if there is a face in the frame, evaluate the emotion
            var detectedFace = faces.FirstOrDefault();
            if (detectedFace != null)
            {
                var boundingBox = new Rect(detectedFace.FaceBox.X,
                                           detectedFace.FaceBox.Y,
                                           detectedFace.FaceBox.Width,
                                           detectedFace.FaceBox.Height);

                var croppedFace = Crop(convertedBitmap, boundingBox);

                CNTKGraphModelInput input = new CNTKGraphModelInput();
                input.Input338 = VideoFrame.CreateWithSoftwareBitmap(croppedFace);

                var emotionResults = await model.EvaluateAsync(input);
                var softMaxOutputs = SoftMax(emotionResults.Plus692_Output_0);

                var emotionIndex = softMaxOutputs.IndexOf(softMaxOutputs.Max());

                if (emotionIndex == currentEmotionIndex)
                {
                    // if the user has been dooing the same emotion for over 3 seconds - turn off alarm
                    if (lastTimeEmotionMatched != null && DateTime.Now - lastTimeEmotionMatched >= TimeSpan.FromSeconds(3))
                    {
                        alarmOn = false;
                    }

                    if (lastTimeEmotionMatched == null)
                    {
                        lastTimeEmotionMatched = DateTime.Now;
                    }
                }
                else
                {
                    lastTimeEmotionMatched = null;
                }
            }
            else
            {
                // can't find face
                lastTimeEmotionMatched = null;
            }
        }

        // crop
        public SoftwareBitmap Crop(SoftwareBitmap softwareBitmap, Rect bounds)
        {
            if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8)
            {
                softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8);
            }

            var resourceCreator = CanvasDevice.GetSharedDevice();
            using (var canvasBitmap = CanvasBitmap.CreateFromSoftwareBitmap(resourceCreator, softwareBitmap))
            using (var canvasRenderTarget = new CanvasRenderTarget(resourceCreator, (float)bounds.Width, (float)bounds.Width, canvasBitmap.Dpi))
            using (var drawingSession = canvasRenderTarget.CreateDrawingSession())
            using (var cropEffect = new CropEffect())
            using (var atlasEffect = new AtlasEffect())
            {
                drawingSession.Clear(Colors.White);

                cropEffect.SourceRectangle = bounds;
                cropEffect.Source = canvasBitmap;

                atlasEffect.SourceRectangle = bounds;
                atlasEffect.Source = cropEffect;

                drawingSession.DrawImage(atlasEffect);
                drawingSession.Flush();

                return SoftwareBitmap.CreateCopyFromBuffer(canvasRenderTarget.GetPixelBytes().AsBuffer(), BitmapPixelFormat.Bgra8, (int)bounds.Width, (int)bounds.Width, BitmapAlphaMode.Premultiplied);
            }
        }

        //softmax based on postporcessing notes on the input page on github
        private List<float> SoftMax(IList<float> inputs)
        {
            List<float> inputsExp = new List<float>();
            float inputsExpSum = 0;
            for (int i = 0; i < inputs.Count; i++)
            {
                var input = inputs[i];
                inputsExp.Add((float)Math.Exp(input));
                inputsExpSum += inputsExp[i];
            }
            inputsExpSum = inputsExpSum == 0 ? 1 : inputsExpSum;
            for (int i = 0; i < inputs.Count; i++)
            {
                inputsExp[i] /= inputsExpSum;
            }
            return inputsExp;
        }
    }
}
