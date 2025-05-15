using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using Newtonsoft.Json;

namespace WpfApp3
{
    public partial class MainWindow : Window
    {
        private HttpServer server;
        private HttpClientHelper client = new HttpClientHelper();
        private PlotModel plotModel;
        private ConcurrentDictionary<DateTime, int> requestCountsPerMinute = new ConcurrentDictionary<DateTime, int>();
        private ObservableCollection<DataPoint> requestLoadData = new ObservableCollection<DataPoint>();
        private int maxPoints = 60;

        public MainWindow()
        {
            InitializeComponent();
            RequestMethodComboBox.SelectedIndex = 0;
            LogFilterMethodComboBox.SelectedIndex = 0;
            LogFilterStatusComboBox.SelectedIndex = 0;
            InitializePlot();
        }

        private void InitializePlot()
        {
            plotModel = new PlotModel { Title = "Request Load (Requests per Minute)" };

            plotModel.Axes.Add(new DateTimeAxis { Position = AxisPosition.Bottom, StringFormat = "HH:mm", Title = "Time (HH:mm)" });
            plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Minimum = 0, Title = "Requests" });

            var lineSeries = new LineSeries { Title = "Requests", ItemsSource = requestLoadData, Color = OxyColors.Blue, MarkerType = MarkerType.None };
            plotModel.Series.Add(lineSeries);

            RequestLoadPlot.Model = plotModel;
        }

        private async void StartServerButton_Click(object sender, RoutedEventArgs e)
        {
            string port = ServerPortTextBox.Text;
            if (string.IsNullOrEmpty(port) || !int.TryParse(port, out _))
            {
                MessageBox.Show("Invalid port number.");
                return;
            }

            string url = $"http://localhost:{port}/";
            requestCountsPerMinute = new ConcurrentDictionary<DateTime, int>();
            server = new HttpServer(url, ServerLogsTextBox, ServerStatisticsDataGrid, this, requestCountsPerMinute);
            try
            {
                await Task.Run(() => server.Start());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting server: {ex.Message}");
            }
        }

        private async void SendRequestButton_Click(object sender, RoutedEventArgs e)
        {
            string url = RequestUrlTextBox.Text;
            string method = ((ComboBoxItem)RequestMethodComboBox.SelectedItem).Content.ToString();
            string body = RequestBodyTextBox.Text;

            if (method == "POST")
            {
                try
                {
                    JsonConvert.DeserializeObject(body);
                }
                catch (JsonReaderException ex)
                {
                    MessageBox.Show($"Invalid JSON: {ex.Message}", "Error");
                    return;
                }
            }

            try
            {
                string response = "";
                if (method == "GET")
                {
                    response = await client.GetAsync(url);
                }
                else if (method == "POST")
                {
                    response = await client.PostAsync(url, body);
                }

                ResponseTextBox.Text = response;
            }
            catch (Exception ex)
            {
                ResponseTextBox.Text = $"Error: {ex.Message}";
            }
        }

        public string GetSelectedMethod()
        {
            return LogFilterMethodComboBox.Text;
        }

        public string GetSelectedStatus()
        {
            return LogFilterStatusComboBox.Text;
        }

        public void UpdateGraph()
        {
            DateTime now = DateTime.Now;
            DateTime startOfMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);

            DateTime cutoff = DateTime.Now.AddMinutes(-maxPoints);
            foreach (var key in requestCountsPerMinute.Keys.Where(key => key < cutoff).ToList())
            {
                requestCountsPerMinute.TryRemove(key, out _);
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                requestLoadData.Clear();

                foreach (var pair in requestCountsPerMinute.OrderBy(k => k.Key))
                {
                    requestLoadData.Add(new DataPoint(DateTimeAxis.ToDouble(pair.Key), pair.Value));
                }

                plotModel.InvalidatePlot(true);
            });
        }
    }
}