using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Toolkit.Uwp.Helpers.CameraHelper;
using MLHelpers;
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

        InkshapesModel model;

        DispatcherTimer timer;
        bool alarmOn = true;

        DateTime? lastTimeEmotionMatched;

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            // load Model
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///model.onnx"));
            model = await InkshapesModel.CreateInkshapesModel(file);

            Inker.InkPresenter.InputDeviceTypes =
            CoreInputDeviceTypes.Pen |
            CoreInputDeviceTypes.Touch |
            CoreInputDeviceTypes.Mouse;

            Inker.InkPresenter.StrokesCollected += InkPresenter_StrokesCollectedAsync;

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(300);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private async void InkPresenter_StrokesCollectedAsync(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            var bitmap = Inker.GetCropedSoftwareBitmap(newWidth: 227, newHeight: 227, keepRelativeSize: true);
            var frame = VideoFrame.CreateWithSoftwareBitmap(bitmap);
            var input = new InkshapesModelInput();
            input.data = frame;

            var output = await model.EvaluateAsync(input);

            var guessedTag = output.classLabel.First();
            var guessedPercentage = output.loss.OrderByDescending(kv => kv.Value).First().Value.ToString();

            GuessText.Text = $"Current Guess: {guessedTag}({guessedPercentage})";
        }

        // decide if the alarm should be on
        private void Timer_Tick(object sender, object e)
        {
            if (alarmOn)
            {
                Alarmbackground.Visibility = Alarmbackground.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                Alarmbackground.Visibility = Visibility.Collapsed;
            }

            TimeText.Text = DateTime.Now.ToShortTimeString();
        }

    }
}
