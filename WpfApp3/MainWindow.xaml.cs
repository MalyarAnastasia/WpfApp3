using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using OxyPlot;
using OxyPlot.Series;
using System.Collections.ObjectModel; 

namespace WpfApp3
{
    public partial class MainWindow : Window
    {
        private HttpServer server;
        private HttpClientHelper client = new HttpClientHelper();
        private PlotModel plotModel;
        private ObservableCollection<DataPoint> requestLoadData = new ObservableCollection<DataPoint>();
        private double timeInterval = 0; 
        private int maxPoints = 60; 
        public MainWindow()
        {
            InitializeComponent();
            RequestMethodComboBox.SelectedIndex = 0; 
            InitializePlot();
        }

        private void InitializePlot()
        {
            plotModel = new PlotModel { Title = "Request Load" };
            var lineSeries = new LineSeries { Title = "Requests", ItemsSource = requestLoadData }; 
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
            server = new HttpServer(url, ServerLogsTextBox, ServerStatisticsDataGrid, this); 
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

        public void UpdateGraph()
        {
            timeInterval += 1;
            if (timeInterval > maxPoints)
            {
                requestLoadData.RemoveAt(0);
                timeInterval = maxPoints;
            }
            double requestCount = server?.requestCount ?? 0;
            requestLoadData.Add(new DataPoint(timeInterval, requestCount));

            plotModel.InvalidatePlot(true);
        }

    }
}