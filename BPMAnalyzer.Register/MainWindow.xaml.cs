using System;
using System.Security.Principal;
using System.Windows;
using Microsoft.Win32;

namespace BPMAnalyzer.Register
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            CheckAdmin();
            InitializeComponent();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var key = Registry.ClassesRoot.OpenSubKey(@"*\shell", true);
            var bpmKey = key.CreateSubKey("Bpm");
            bpmKey.SetValue("AppliesTo", ".wav OR .mp3");
            var commandKey = bpmKey.CreateSubKey("command");
            commandKey.SetValue(null, $"{Environment.CurrentDirectory}\\BPMAnalyzer.exe \"%1\"");

            MessageBox.Show($"Add Successfully.");
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var key = Registry.ClassesRoot.OpenSubKey(@"*\shell", true);
            key.DeleteSubKeyTree("Bpm");
            MessageBox.Show($"Remove Successfully.");
        }

        private void CheckAdmin()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            var isElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);

            if (!isElevated)
            {
                MessageBox.Show("Administrator permission is required to running.");
                Environment.Exit(-1);
            }
        }
    }
}