using System.Windows.Controls;
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
            try
            {
                if (freq >= 37 && freq <= 32767 && dur > 0)
                {
                    long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                    double elapsed = 0;
                    while (elapsed < 0.5)
                    {
                        System.Console.Beep(freq, dur);
                        
                        // Используем точный таймер вместо Thread.Sleep, чтобы избежать 
                        // наложения звуков из-за неточности системного таймера Windows.
                        // Гарантированная пауза 25 мс между писками.
                        long pauseStart = System.Diagnostics.Stopwatch.GetTimestamp();
                        while ((System.Diagnostics.Stopwatch.GetTimestamp() - pauseStart) / (double)System.Diagnostics.Stopwatch.Frequency < 0.025)
                        {
                            System.Threading.Thread.SpinWait(50);
                        }
                        
                        long currentTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                        elapsed = (currentTicks - startTicks) / (double)System.Diagnostics.Stopwatch.Frequency;
                    }
                }
            }
            catch { }
        }
    }
}
