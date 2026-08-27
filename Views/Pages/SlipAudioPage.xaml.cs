using System.Windows.Controls;
using SimRacingHub.Core;
using SimRacingHub.ViewModels;

namespace SimRacingHub.Views.Pages
{
    public partial class SlipAudioPage : Page
    {
        public SlipAudioPage(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void PreviewUndersteer_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.CurrentProfile != null)
            {
                int freq = vm.CurrentProfile.SlipAudioUnderFreq ?? 650;
                int dur = vm.CurrentProfile.SlipAudioUnderDur ?? 80;
                System.Threading.Tasks.Task.Run(() => SimulateSlide(freq, dur));
            }
        }

        private void PreviewOversteer_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.CurrentProfile != null)
            {
                int freq = vm.CurrentProfile.SlipAudioOverFreq ?? 280;
                int dur = vm.CurrentProfile.SlipAudioOverDur ?? 100;
                System.Threading.Tasks.Task.Run(() => SimulateSlide(freq, dur));
            }
        }

        private void PreviewTc_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.CurrentProfile != null)
            {
                int freq = vm.CurrentProfile.TcAudioFreq ?? 500;
                int dur = vm.CurrentProfile.TcAudioDur ?? 60;
                System.Threading.Tasks.Task.Run(() => SimulateSlide(freq, dur));
            }
        }

        private void SimulateSlide(int freq, int dur)
        {
            freq = Math.Clamp(freq, 37, 32767);
            dur = Math.Clamp(dur, 10, 500);

            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                while (stopwatch.ElapsedMilliseconds < 500)
                {
                    System.Console.Beep(freq, dur);

                    System.Threading.Thread.Sleep(25);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogWarning($"Could not play the audio cue preview: {ex.Message}");
            }
        }
    }
}
