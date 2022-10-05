using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Audio;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Media.Capture;
using Windows.Media.Render;
using Windows.Media.Transcoding;
using Windows.ApplicationModel;

using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System.Runtime.CompilerServices;
using System.Net.Http;
using System.ServiceModel.Channels;
using System.Net.Http.Json;
using Newtonsoft.Json.Linq;


// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409
// UWP Hello World https://learn.microsoft.com/en-us/windows/uwp/get-started/create-a-hello-world-app-xaml-universal
// Speech Recognition https://learn.microsoft.com/en-us/azure/cognitive-services/speech-service/get-started-speech-to-text?tabs=windows%2Cterminal&pivots=programming-language-csharp
// How to Recognize Speech https://learn.microsoft.com/en-us/azure/cognitive-services/speech-service/how-to-recognize-speech?pivots=programming-language-csharp

namespace UWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private AudioDeviceInputNode deviceInputNode;
        private AudioDeviceOutputNode deviceOutputNode;
        private AudioFileOutputNode fileOutputNode;
        private DeviceInformation inputDevice;
        private DeviceInformation outputDevice;
        private AudioGraph graph;

        static string speechKey = "8144d34c85814471b54440739c0a88d0";
        static string speechRegion = "westus";

        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            //await ToggleRecordStop();

            var devices = await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture);

            var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
            speechConfig.SpeechRecognitionLanguage = "en-US";

            var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            var speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);

            //Console.WriteLine("Speak into your microphone.");
            var speechRecognitionResult = await speechRecognizer.RecognizeOnceAsync();
            OutputSpeechRecognitionResult(speechRecognitionResult);
        }

        private void OutputSpeechRecognitionResult(SpeechRecognitionResult speechRecognitionResult)
        {
            switch (speechRecognitionResult.Reason)
            {
                case ResultReason.RecognizedSpeech:
                    AudioTextBlock.Text = AudioTextBlock.Text + $"{speechRecognitionResult.Text}\r\n\r\n";
                    GetResult(AudioTextBlock.Text);
                    break;
                case ResultReason.NoMatch:
                    Console.WriteLine($"NOMATCH: Speech could not be recognized.");
                    break;
                case ResultReason.Canceled:
                    var cancellation = CancellationDetails.FromResult(speechRecognitionResult);
                    Console.WriteLine($"CANCELED: Reason={cancellation.Reason}");

                    if (cancellation.Reason == CancellationReason.Error)
                    {
                        Console.WriteLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                        Console.WriteLine($"CANCELED: ErrorDetails={cancellation.ErrorDetails}");
                        Console.WriteLine($"CANCELED: Did you set the speech resource key and region values?");
                    }
                    break;
            }
        }

        private async Task PopulateInputDeviceList()
        {
            inputDevice = await DeviceInformation.CreateFromIdAsync(MediaDevice.GetDefaultAudioCaptureId(AudioDeviceRole.Default));
        }

        private async Task PopulateOutputDeviceList()
        {
            outputDevice = await DeviceInformation.CreateFromIdAsync(MediaDevice.GetDefaultAudioRenderId(AudioDeviceRole.Default));
        }

        private async Task CreateAudioFile()
        {
            // Create sample file; replace if exists.
            //StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
            StorageFolder storageFolder = KnownFolders.MusicLibrary;
            StorageFile file = 
                await storageFolder.CreateFileAsync("mytest.wav", Windows.Storage.CreationCollisionOption.ReplaceExisting);

            // Operate node at the graph format, but save file at the specified format
            CreateAudioFileOutputNodeResult fileOutputNodeResult = 
                await graph.CreateFileOutputNodeAsync(file, MediaEncodingProfile.CreateWav(AudioEncodingQuality.High));

            fileOutputNode = fileOutputNodeResult.FileOutputNode;

            // Connect the input node to both output nodes
            deviceInputNode.AddOutgoingConnection(fileOutputNode);
        }

        private async Task CreateAudioGraph()
        {
            AudioGraphSettings settings = new AudioGraphSettings(AudioRenderCategory.Media);
            settings.QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency;
            settings.PrimaryRenderDevice = outputDevice;

            CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);
            graph = result.Graph;

            // Create a device output node
            CreateAudioDeviceOutputNodeResult deviceOutputNodeResult = await graph.CreateDeviceOutputNodeAsync();
            deviceOutputNode = deviceOutputNodeResult.DeviceOutputNode;

            // Create a device input node using the default audio input device
            CreateAudioDeviceInputNodeResult deviceInputNodeResult = await graph.CreateDeviceInputNodeAsync(MediaCategory.Other, graph.EncodingProperties, inputDevice);
            deviceInputNode = deviceInputNodeResult.DeviceInputNode;
        }

        private async Task ToggleRecordStop()
        {
            if (RecordButton.Content.Equals("Record"))
            {
                await PopulateOutputDeviceList();
                await PopulateInputDeviceList();

                await CreateAudioGraph();
                await CreateAudioFile();

                graph.Start();
                RecordButton.Content = "Stop";
            }
            else if (RecordButton.Content.Equals("Stop"))
            {
                // Good idea to stop the graph to avoid data loss
                graph.Stop();
                TranscodeFailureReason finalizeResult = await fileOutputNode.FinalizeAsync();

                RecordButton.Content = "Record";
            }
        }

        private async void GetResult(string message)
        {
            try
            {
                string model_id = "unitary/toxic-bert";
                string api_token = "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";
                string userfeedback = "";
                double highestscore = 0;

                string requestUrl = "https://api-inference.huggingface.co/models/" + model_id;

                HttpClient myClient = new HttpClient();

                HttpRequestMessage requestMessage = new HttpRequestMessage();
                requestMessage.Content = JsonContent.Create(message);
                requestMessage.Headers.TryAddWithoutValidation("Authorization: Bearer ", api_token);

                HttpResponseMessage responseMessage = await myClient.PostAsync(requestUrl, requestMessage.Content);
                HttpContent content = responseMessage.Content;
                string result = await content.ReadAsStringAsync();

                var myJarray = JArray.Parse(result).Children().FirstOrDefault();

                List<myScores> sl = new List<myScores>();

                foreach (var item in myJarray)
                {
                    myScores s = item.ToObject<myScores>();
                    s.score = s.score * 100;
                    AudioTextBlock.Text = AudioTextBlock.Text + s.label + ": " + s.score.ToString("00.0") + "%\r\n";

                    string f = ProcessScore(s, highestscore);
                    if (!String.IsNullOrEmpty(f))
                        userfeedback = f;
                }

                if(String.IsNullOrEmpty(userfeedback))
                    AudioTextBlock.Text = AudioTextBlock.Text + "\r\n";
                else
                    AudioTextBlock.Text = AudioTextBlock.Text + "\r\n" + userfeedback + "\r\n";
            }
            catch(Exception e)
            {
                AudioTextBlock.Text = e.Message;
            }
        }

        public string ProcessScore(myScores s, double currentscore)
        {
            string myfeedback = "";

            if(s.score > 5)
                if(s.score > currentscore)
                {
                    myfeedback = "We have detected " + s.label + " in your speech.\r\n Please take a moment to calm down.\r\n Please start treating others with more respect.\r\n Thank you\r\n";
                }

            return myfeedback;
        }

        public class myScores
        {
            public string label { get; set; }

            public double score { get; set; }
        }
    }
}
