using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp3
{
    public class HttpClientHelper
    {
        private readonly HttpClient client = new HttpClient();

        public async Task<string> GetAsync(string url)
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode(); 
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException e)
            {
                return $"Error: {e.Message}";
            }
        }

        public async Task<string> PostAsync(string url, string jsonContent)
        {
            try
            {
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException e)
            {
                return $"Error: {e.Message}";
            }
        }
    }
}