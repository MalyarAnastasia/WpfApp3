using System;
using System.Collections.Generic;
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

        public HttpServer(string url, TextBox logTextBox, DataGrid statisticsDataGrid, MainWindow mainWindow)
        {
            this.url = url;
            this.logTextBox = logTextBox;
            this.statisticsDataGrid = statisticsDataGrid;
            this.mainWindow = mainWindow; 
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
            string requestMethod = context.Request.HttpMethod;
            string requestUrl = context.Request.RawUrl;
            string requestHeaders = string.Join("\n", context.Request.Headers.AllKeys.Select(key => $"{key}: {context.Request.Headers[key]}"));
            string requestBody = "";

            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
            {
                requestBody = await reader.ReadToEndAsync();
            }

            Log($"Request received: {requestMethod} {requestUrl}\nHeaders:\n{requestHeaders}\nBody:\n{requestBody}\n");

            string responseString = "";
            HttpStatusCode statusCode = HttpStatusCode.OK;

            try
            {
                switch (requestMethod)
                {
                    case "GET":
                        responseString = HandleGetRequest();
                        break;
                    case "POST":
                        responseString = HandlePostRequest(requestBody);
                        if (string.IsNullOrEmpty(responseString))
                        {
                            statusCode = HttpStatusCode.BadRequest; 
                        }
                        break;
                    default:
                        responseString = "Method not supported";
                        statusCode = HttpStatusCode.NotImplemented;
                        break;
                }
            }
            catch (Exception ex)
            {
                responseString = $"Error processing request: {ex.Message}";
                statusCode = HttpStatusCode.InternalServerError;
                Log($"Error: {ex}");
            }

            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();

            Log($"Response sent: {statusCode}\n{responseString}\n");

            UpdateStatistics(); 
        }


        private string HandleGetRequest()
        {
            TimeSpan uptime = DateTime.Now - startTime;
            return JsonConvert.SerializeObject(new
            {
                Status = "OK",
                RequestCount = requestCount,
                Uptime = uptime.ToString()
            });
        }

        private string HandlePostRequest(string requestBody)
        {
            try
            {
                dynamic data = JsonConvert.DeserializeObject(requestBody);

                if (data?.message != null)
                {
                    receivedMessages.Add(data.message.ToString());

                    string messageId = Guid.NewGuid().ToString();

                    return JsonConvert.SerializeObject(new { id = messageId, message = data.message });
                }
                else
                {
                    return null; 
                }
            }
            catch (JsonReaderException)
            {
                return null; 
            }
        }

        private void Log(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                logTextBox.AppendText(message + "\n");
                logTextBox.ScrollToEnd();
                WriteLogToFile(message);
                mainWindow?.UpdateGraph(); 
            });
        }

        private void UpdateStatistics()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var statistics = new[] {
                    new { Metric = "Total Requests", Value = requestCount.ToString() },
                    new { Metric = "GET Requests", Value = (requestCount - receivedMessages.Count).ToString() },
                    new { Metric = "POST Requests", Value = receivedMessages.Count.ToString() },
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