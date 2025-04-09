using NAudio.Wave;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Diagnostics;
using System.Windows.Forms; // For screen capture

namespace ScreenRecordingApp
{
    public partial class MainWindow : Window
    {
        private bool isRecording = false;
        private bool isPaused = false;  // To track if the recording is paused
        private int frameRate = 30; // 30 FPS
        private DispatcherTimer timer;
        private int frameCount = 0; // To keep track of frame numbers
        private WaveInEvent waveIn;
        private WaveFileWriter waveFileWriter;
        private string outputAudioFile = "output.wav"; // Default audio file path
        private string framesDirectory = @"C:\Users\kiran\source\repos\ScreenRecordingApp\ScreenRecordingApp\Frames"; // Directory to save frames

        public MainWindow()
        {
            InitializeComponent();
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(1000 / frameRate); // Set interval based on frame rate
            timer.Tick += Timer_Tick;
        }

        private async void StartRecording_Click(object sender, RoutedEventArgs e)
        {
            isRecording = true;
            btnStartRecording.IsEnabled = false;
            btnStopRecording.IsEnabled = true;
            btnPauseRecording.IsEnabled = true;  // Enable Pause button
            progressBar.Value = 0;
            frameCount = 0; // Reset the frame count
            timer.Start();

            // Start audio recording
            StartRecordingAudio();

            // Start screen capture
            await CaptureScreenAsync();
        }

        private void StopRecording_Click(object sender, RoutedEventArgs e)
        {
            isRecording = false;
            isPaused = false; // Reset pause state
            timer.Stop();
            btnStartRecording.IsEnabled = true;
            btnStopRecording.IsEnabled = false;
            btnPauseRecording.IsEnabled = false;  // Disable Pause button

            // Stop audio recording
            StopRecordingAudio();

            // Ask the user to save the video
            SaveVideo();
        }

        private void PauseRecording_Click(object sender, RoutedEventArgs e)
        {
            if (isPaused)
            {
                // If recording is paused, resume it
                ResumeRecording();
            }
            else
            {
                // If recording is not paused, pause it
                PauseRecording();
            }
        }

        private void PauseRecording()
        {
            // Pause the recording
            isPaused = true;
            btnPauseRecording.Content = "Resume Recording"; // Change button text to Resume

            // Stop screen capture and audio recording temporarily
            timer.Stop();
            waveIn.StopRecording();
        }

        private void ResumeRecording()
        {
            // Resume the recording
            isPaused = false;
            btnPauseRecording.Content = "Pause Recording"; // Change button text back to Pause

            // Restart the screen capture and audio recording
            timer.Start();
            waveIn.StartRecording();
        }

        private void SaveVideo()
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "MP4 Video|*.mp4",
                Title = "Save Video"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                string videoFilePath = saveFileDialog.FileName;

                // Call EncodeVideo with the path for the output video
                EncodeVideo(videoFilePath, outputAudioFile);
            }
        }

        private async Task CaptureScreenAsync()
        {
            while (isRecording)
            {
                var screenImage = CaptureScreen();
                screenPreview.Source = screenImage;

                // Save screen image as PNG (adjusted folder path)
                SaveFrameAsPng(screenImage);

                await Task.Delay(1000 / frameRate); // Ensure 30 FPS capture
            }
        }

        private BitmapSource CaptureScreen()
        {
            // Capture the entire screen
            var screenBounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            var screenCapture = new System.Drawing.Bitmap(screenBounds.Width, screenBounds.Height);
            using (var g = System.Drawing.Graphics.FromImage(screenCapture))
            {
                g.CopyFromScreen(screenBounds.Left, screenBounds.Top, 0, 0, screenBounds.Size);
            }

            // Convert to BitmapSource for use in WPF
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(screenCapture.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }

        private void SaveFrameAsPng(BitmapSource screenImage)
        {
            // Save the captured frame as PNG
            string framePath = Path.Combine(framesDirectory, $"screen_capture_{frameCount:D3}.png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(screenImage));
            using (var stream = new FileStream(framePath, FileMode.Create))
            {
                encoder.Save(stream);
            }
            frameCount++; // Increment frame counter
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // Update progress bar or other UI elements
            progressBar.Value += 1;
        }

        private void StartRecordingAudio()
        {
            waveIn = new WaveInEvent();
            waveIn.DeviceNumber = 0;  // Select default audio device
            waveIn.WaveFormat = new WaveFormat(44100, 1); // Set audio format (44.1kHz, Mono)
            waveIn.DataAvailable += OnAudioDataAvailable;
            waveIn.StartRecording();

            waveFileWriter = new WaveFileWriter(outputAudioFile, waveIn.WaveFormat);
        }

        private void OnAudioDataAvailable(object sender, WaveInEventArgs e)
        {
            // Write audio data to file
            waveFileWriter.Write(e.Buffer, 0, e.BytesRecorded);
        }

        private void StopRecordingAudio()
        {
            waveIn.StopRecording();
            waveIn.Dispose();
            waveFileWriter.Close();
        }

        private void EncodeVideo(string videoFileName, string audioFileName)
        {
            string ffmpegPath = @"C:\Users\kiran\source\repos\ScreenRecordingApp\ScreenRecordingApp\FFmpeg\ffmpeg.exe";  // Path to FFmpeg executable
            string framesDirectory = @"C:\Users\kiran\source\repos\ScreenRecordingApp\ScreenRecordingApp\Frames";  // Directory where frames are stored

            // Arguments for FFmpeg with frame rate control
            string arguments = $"-framerate {frameRate} -i \"{framesDirectory}\\screen_capture_%03d.png\" -i {audioFileName} -c:v libx264 -preset veryfast -crf 23 -pix_fmt yuv420p -r {frameRate} -c:a aac -b:a 192k -strict experimental -y {videoFileName}";

            try
            {
                // Start FFmpeg process
                Process ffmpegProcess = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                ffmpegProcess.Start();
                ffmpegProcess.WaitForExit(); // Wait for FFmpeg to finish processing
            }
            catch (Exception ex)
            {
              //  MessageBox.Show("Error during video encoding: " + ex.Message);
            }
        }



    }
}
