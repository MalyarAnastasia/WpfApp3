using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp3
{
    public class HttpServer
    {
        private HttpListener listener;
        private string url;
        private TextBox logTextBox;
        private List<string> receivedMessages = new List<string>();
        public int requestCount = 0;
        private DateTime startTime;
        private DataGrid statisticsDataGrid;
        private MainWindow mainWindow;
        private ConcurrentDictionary<DateTime, int> requestCountsPerMinute;

        public HttpServer(string url, TextBox logTextBox, DataGrid statisticsDataGrid, MainWindow mainWindow, ConcurrentDictionary<DateTime, int> requestCountsPerMinute)
        {
            this.url = url;
            this.logTextBox = logTextBox;
            this.statisticsDataGrid = statisticsDataGrid;
            this.mainWindow = mainWindow;
            this.requestCountsPerMinute = requestCountsPerMinute;
        }

        public async Task Start()
        {
            listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            startTime = DateTime.Now;
            Log($"Server started listening on {url}");

            while (listener.IsListening)
            {
                try
                {
                    HttpListenerContext context = await listener.GetContextAsync();
                    ProcessRequest(context);
                }
                catch (HttpListenerException ex)
                {
                    Log($"Error during request processing: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Log($"Unexpected error: {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            listener.Stop();
            listener.Close();
            Log("Server stopped.");
        }

        private async void ProcessRequest(HttpListenerContext context)
        {
            requestCount++;
            Console.WriteLine("ProcessRequest: RequestCount incremented");

            DateTime now = DateTime.Now;
            DateTime startOfMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);

            requestCountsPerMinute.AddOrUpdate(startOfMinute, 1, (key, oldValue) => oldValue + 1);

            string requestMethod = context.Request.HttpMethod;
            string requestUrl = context.Request.RawUrl;
            string requestHeaders = string.Join("\n", context.Request.Headers.AllKeys.Select(key => $"{key}: {context.Request.Headers[key]}"));
            string requestBody = "";

            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
            {
                requestBody = await reader.ReadToEndAsync();
            }

            Console.WriteLine($"ProcessRequest: RequestMethod = {requestMethod}, RequestUrl = {requestUrl}");

            Log($"Request received: {requestMethod} {requestUrl}\nHeaders:\n{requestHeaders}\nBody:\n{requestBody}\n", requestMethod, null);

            string responseString = "";
            HttpStatusCode statusCode = HttpStatusCode.OK;
            Console.WriteLine($"ProcessRequest: Initial StatusCode = {statusCode}");

            try
            {
                switch (requestMethod)
                {
                    case "GET":
                        responseString = HandleGetRequest(out statusCode);
                        Console.WriteLine($"ProcessRequest: After HandleGetRequest, StatusCode = {statusCode}");
                        break;
                    case "POST":
                        responseString = HandlePostRequest(requestBody, out statusCode);
                        Console.WriteLine($"ProcessRequest: After HandlePostRequest, StatusCode = {statusCode}");
                        break;
                    default:
                        responseString = "Method not supported";
                        statusCode = HttpStatusCode.NotImplemented;
                        Console.WriteLine($"ProcessRequest: Method not supported, StatusCode = {statusCode}");
                        break;
                }
            }
            catch (Exception ex)
            {
                responseString = $"Error processing request: {ex.Message}";
                statusCode = HttpStatusCode.InternalServerError;
                Console.WriteLine($"ProcessRequest: Exception, StatusCode = {statusCode}");
                Log($"Error: {ex}", requestMethod, statusCode.ToString());
            }

            Console.WriteLine($"ProcessRequest: Before null check, StatusCode = {statusCode}");
            if (responseString == null)
            {
                responseString = "";
                statusCode = HttpStatusCode.InternalServerError;
                Console.WriteLine($"ProcessRequest: responseString is null, StatusCode = {statusCode}");
            }
            Console.WriteLine($"ProcessRequest: Before send response, StatusCode = {statusCode}");

            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();

            Console.WriteLine($"ProcessRequest: Response sent, StatusCode = {statusCode}");
            Log($"Response sent: {statusCode}\n{responseString}\n", requestMethod, statusCode.ToString());

            UpdateStatistics(requestMethod);
            mainWindow?.UpdateGraph();
        }

        private string HandleGetRequest(out HttpStatusCode statusCode)
        {
            Console.WriteLine("HandleGetRequest: Started");
            try
            {
                TimeSpan uptime = DateTime.Now - startTime;
                var response = new
                {
                    Status = "OK",
                    RequestCount = requestCount,
                    Uptime = uptime.ToString()
                };
                string jsonResponse = JsonConvert.SerializeObject(response);
                statusCode = HttpStatusCode.OK;
                Console.WriteLine($"HandleGetRequest: Success, StatusCode = {statusCode}");
                return jsonResponse;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HandleGetRequest: Exception - {ex.Message}");
                statusCode = HttpStatusCode.InternalServerError;
                return null;
            }
        }

        private string HandlePostRequest(string requestBody, out HttpStatusCode statusCode)
        {
            Console.WriteLine("HandlePostRequest: Started");
            try
            {
                dynamic data = JsonConvert.DeserializeObject(requestBody);

                if (data?.message != null)
                {
                    receivedMessages.Add(data.message.ToString());
                    string messageId = Guid.NewGuid().ToString();
                    statusCode = HttpStatusCode.OK;
                    Console.WriteLine($"HandlePostRequest: Success, StatusCode = {statusCode}");
                    return JsonConvert.SerializeObject(new { id = messageId, message = data.message });
                }
                else
                {
                    Log("Error: 'message' property is missing in JSON");
                    statusCode = HttpStatusCode.BadRequest;
                    Console.WriteLine($"HandlePostRequest: Missing message, StatusCode = {statusCode}");
                    return null;
                }
            }
            catch (JsonReaderException ex)
            {
                Console.WriteLine($"HandlePostRequest: Invalid JSON format - {ex.Message}");
                Log($"Error: Invalid JSON format: {ex.Message}");
                statusCode = HttpStatusCode.BadRequest;
                Console.WriteLine($"HandlePostRequest: Invalid JSON format, StatusCode = {statusCode}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HandlePostRequest: Unexpected error HandlePostRequest : {ex.Message}");
                Log($"Unexpected error HandlePostRequest : {ex.Message}");
                statusCode = HttpStatusCode.InternalServerError;
                Console.WriteLine($"HandlePostRequest: Unexpected error, StatusCode = {statusCode}");
                return null;
            }

        }

        private void Log(string message, string requestMethod = null, string statusCode = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (ShouldLogMessage(requestMethod, statusCode))
                {
                    logTextBox.AppendText(message + "\n");
                    logTextBox.ScrollToEnd();
                }
                WriteLogToFile(message);
            });
        }

        private bool ShouldLogMessage(string requestMethod, string statusCode)
        {
            Console.WriteLine($"ShouldLogMessage called - method:{requestMethod}, status:{statusCode}");

            string selectedMethod = mainWindow.GetSelectedMethod();
            string selectedStatus = mainWindow.GetSelectedStatus();

            Console.WriteLine($"SelectedMethod: {selectedMethod}, SelectedStatus: {selectedStatus}");

            if (selectedMethod != "All" && requestMethod != selectedMethod)
            {
                Console.WriteLine($"Method Filtered: {requestMethod} != {selectedMethod}");
                return false;
            }

            if (selectedStatus != "All" && statusCode != selectedStatus)
            {
                Console.WriteLine($"Status Filtered: {statusCode} != {selectedStatus}");
                return false;
            }

            return true;
        }

        private void UpdateStatistics(string requestMethod)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                int getCount = requestCount - receivedMessages.Count;
                int postCount = receivedMessages.Count;

                var statistics = new[] {
                    new { Metric = "Total Requests", Value = requestCount.ToString() },
                    new { Metric = "GET Requests", Value = getCount.ToString() },
                    new { Metric = "POST Requests", Value = postCount.ToString() },
                    new { Metric = "Uptime", Value = (DateTime.Now - startTime).ToString() }
                };

                statisticsDataGrid.ItemsSource = statistics;
            });
        }

        private void WriteLogToFile(string logMessage)
        {
            string filePath = "logs.txt";
            try
            {
                using (StreamWriter writer = File.AppendText(filePath))
                {
                    writer.WriteLine($"{DateTime.Now}: {logMessage}");
                }
            }
            catch (Exception ex)
            {
                Log($"Error writing to log file: {ex.Message}");
            }
        }
    }
}