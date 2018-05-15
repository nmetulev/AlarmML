using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Toolkit.Uwp.Helpers.CameraHelper;
using MLHelpers;
using Microsoft.Toolkit.Uwp.UI.Animations;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.FaceAnalysis;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
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

        DispatcherTimer timeTimer;
        InkDrawingAttributes ida;
        Random random = new Random((int)DateTime.Now.Ticks);
        MediaElement mediaElement;
        bool animating = false;

        private bool _alarmOn = true;
        private bool AlarmOn
        {
            get { return _alarmOn; }
            set {
                if (_alarmOn != value)
                {
                    if (!value)
                    {
                        RunAlarmOffAnimation();
                        mediaElement.Stop();
                        SubText.Text = $"Good Morning!";
                    }
                    else
                    {
                        mediaElement.Play();
                    }
                }
                _alarmOn = value;
            }
        }

        InkshapesModel model;
        private string currentShape;
        private List<string> shapeLabels = new List<string>()
        {
            "airplane",
            "axe", 
            "bike", 
            "bird", 
            "bomb", 
            "cake", 
            "car",
            "cat",
            "chair",      
            "doughnut",
            "duck", 
            "fish",   
            "flower", 
            "guitar", 
            "heart",
            "house",
            "poop",  
            "rocket", 
            "shoe",       
            "stick_figure",
            "sun",
        };

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            // load Model
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///model.onnx"));
            model = await InkshapesModel.CreateInkshapesModel(file);


            #region setup InkCanvs, sound and timers
            currentShape = shapeLabels[random.Next(shapeLabels.Count)];

            timeTimer = new DispatcherTimer();
            timeTimer.Interval = TimeSpan.FromMilliseconds(100);
            timeTimer.Tick += Timer_Tick;
            timeTimer.Start();


            var alarmFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///wakeup.m4a"));
            var stream = await alarmFile.OpenAsync(FileAccessMode.Read);

            mediaElement = new MediaElement();
            mediaElement.IsLooping = true;
            mediaElement.AutoPlay = true;
            mediaElement.SetSource(stream, alarmFile.ContentType);

            Inker.InkPresenter.InputDeviceTypes =
            CoreInputDeviceTypes.Pen |
            CoreInputDeviceTypes.Touch |
            CoreInputDeviceTypes.Mouse;

            ida = InkDrawingAttributes.CreateForPencil();
            ida.Size = new Size(30, 30);
            ida.Color = Colors.White;
            ida.PencilProperties.Opacity = 1;
            Inker.InkPresenter.UpdateDefaultDrawingAttributes(ida);

            Inker.InkPresenter.StrokesCollected += InkPresenter_StrokesCollectedAsync;

            SubText.Text = $"draw {currentShape} to Snooze";
            #endregion
        }

        private async void InkPresenter_StrokesCollectedAsync(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            var bitmap = Inker.GetCropedSoftwareBitmap(newWidth: 227, newHeight: 227, keepRelativeSize: true);
            var frame = VideoFrame.CreateWithSoftwareBitmap(bitmap);
            var input = new InkshapesModelInput() { data = frame };

            var output = await model.EvaluateAsync(input);

            var guessedTag = output.classLabel.First();
            var guessedPercentage = output.loss.OrderByDescending(kv => kv.Value).First().Value;

            if (guessedPercentage < 0.8)
            {
                SubText.Text = $"draw {currentShape} to snooze - don't know what that is";
            }
            else if (guessedTag != currentShape)
            {
                SubText.Text = $"draw {currentShape} to snooze - you drew {guessedTag}";
            }
            else
            {
                AlarmOn = false;
                foreach (var stroke in Inker.InkPresenter.StrokeContainer.GetStrokes())
                {
                    var attributes = stroke.DrawingAttributes;
                    attributes.PencilProperties.Opacity = 1;
                    attributes.Color = Colors.DarkBlue;
                    attributes.Size = new Size(60, 60);
                    stroke.DrawingAttributes = attributes;
                    stroke.PointTransform = Matrix3x2.CreateScale(2, new Vector2((float)ActualWidth / 2, (float)ActualHeight /2));
                }
            }

            Debug.WriteLine($"Current guess: {guessedTag}({guessedPercentage})");
        }

        private async Task RunAlarmOnAnimation()
        {
            if (!animating)
            {
                animating = true;
                var centerY = (float)AlarmIcon.ActualHeight / 2;
                var centerX = (float)AlarmIcon.ActualWidth / 2;
                await AlarmIcon.Rotate(10, centerX, centerY).Offset().Then()
                               .Rotate(-10, centerX, centerY).Then()
                               .Rotate(10, centerX, centerY).Then()
                               .Rotate(-10, centerX, centerY).SetDurationForAll(100).StartAsync();
                await Task.Delay(400);
                animating = false;
            }
        }

        private async Task RunAlarmOffAnimation()
        {
            animating = true;
            await AlarmIcon.Offset(0, 400).StartAsync();
            
            animating = false;
        }

        // decide if the alarm should be on
        private void Timer_Tick(object sender, object e)
        {
            var now = DateTime.Now;
            if (_alarmOn)
            {
                RunAlarmOnAnimation();
            }

            foreach (var stroke in Inker.InkPresenter.StrokeContainer.GetStrokes())
            {
                if (stroke.DrawingAttributes.PencilProperties.Opacity < 0.02)
                {
                    stroke.Selected = true;
                }
                else
                {
                    var attributes = stroke.DrawingAttributes;
                    attributes.PencilProperties.Opacity -= 0.02;
                    stroke.DrawingAttributes = attributes;
                }
            }

            Inker.InkPresenter.StrokeContainer.DeleteSelected();

            TimeText.Text = now.Hour.ToString("00");
            TimeMinutesText.Text = $":{now.Minute.ToString("00")}";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!AlarmOn) AlarmOn = true;

            currentShape = shapeLabels[random.Next(shapeLabels.Count)];
            SubText.Text = $"draw {currentShape} to Snooze";
        }
    }
}
