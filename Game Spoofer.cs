using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Xbox_Achievement_Unlocker
{
    class Program
    {
        private static Stopwatch stopwatch;
        private static HttpClient client;
        private static bool active;
        private static string xauthtoken;
        private static string xuid;

        static async Task Main(string[] args)
        {
            stopwatch = new Stopwatch();
            client = new HttpClient(new HttpClientHandler()
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            });

            while (true)
            {
                Console.Clear();
                Console.WriteLine("Xbox Achievement Unlocker");
                Console.WriteLine("1. Login");
                Console.WriteLine("2. Start Spoofing");
                Console.WriteLine("3. Stop Spoofing");
                Console.WriteLine("4. Exit");

                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        await Login();
                        break;

                    case "2":
                        if (string.IsNullOrEmpty(xauthtoken) || string.IsNullOrEmpty(xuid))
                        {
                            Console.WriteLine("Please login first.");
                        }
                        else
                        {
                            Console.WriteLine("Enter the Title ID to spoof:");
                            string titleId = Console.ReadLine();
                            await StartSpoofing(titleId);
                        }
                        break;

                    case "3":
                        StopSpoofing();
                        break;

                    case "4":
                        return;

                    default:
                        Console.WriteLine("Invalid choice. Please try again.");
                        break;
                }
            }
        }

        private static async Task Login()
        {
            Console.WriteLine("Enter Xbox username:");
            string username = Console.ReadLine();
            Console.WriteLine("Enter Xbox password:");
            string password = Console.ReadLine();

            try
            {
                // Perform login and token retrieval
                (xauthtoken, xuid) = await Authenticate(username, password);
                Console.WriteLine("Login successful.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Login failed: " + ex.Message);
            }
        }

        private static async Task<(string, string)> Authenticate(string username, string password)
        {
            // Example code for authentication; the actual implementation will vary
            var loginRequestBody = new
            {
                Username = username,
                Password = password
            };

            var loginResponse = await client.PostAsync("https://login.xbox.com/authenticate", 
                new StringContent(JsonConvert.SerializeObject(loginRequestBody), Encoding.UTF8, "application/json"));

            if (!loginResponse.IsSuccessStatusCode)
            {
                throw new Exception("Failed to authenticate with Xbox Live.");
            }

            var loginResponseBody = await loginResponse.Content.ReadAsStringAsync();
            dynamic loginData = JObject.Parse(loginResponseBody);

            string authToken = loginData.authToken;
            string userXuid = loginData.xuid;

            return (authToken, userXuid);
        }

        static async Task StartSpoofing(string titleId)
        {
            Console.WriteLine("Starting spoofing...");

            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("x-xbl-contract-version", "2");
            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
            client.DefaultRequestHeaders.Add("accept", "application/json");
            client.DefaultRequestHeaders.Add("Authorization", xauthtoken);
            client.DefaultRequestHeaders.Add("accept-language", "en-GB");

            StringContent requestbody = new StringContent("{\"pfns\":null,\"titleIds\":[\"" + titleId + "\"]}");
            var response = await client.PostAsync($"https://titlehub.xboxlive.com/users/xuid({xuid})/titles/batch/decoration/GamePass,Achievement,Stats", requestbody);
            var jsonResponse = (dynamic)JObject.Parse(await response.Content.ReadAsStringAsync());

            Console.WriteLine("Currently Spoofing: " + jsonResponse.titles[0].name.ToString());
            active = true;
            Task.Run(() => Spoofing(titleId));

            DisplaySpoofingTime();
        }

        public static async Task Spoofing(string titleId)
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("x-xbl-contract-version", "3");
            client.DefaultRequestHeaders.Add("accept", "application/json");
            client.DefaultRequestHeaders.Add("Authorization", xauthtoken);

            var requestbody = new StringContent("{\"titles\":[{\"expiration\":600,\"id\":" + titleId + ",\"state\":\"active\",\"sandbox\":\"RETAIL\"}]}", Encoding.UTF8, "application/json");
            stopwatch.Start();
            await client.PostAsync($"https://presence-heartbeat.xboxlive.com/users/xuid({xuid})/devices/current", requestbody);
            var i = 0;

            while (active)
            {
                if (i == 60)
                {
                    await client.PostAsync($"https://presence-heartbeat.xboxlive.com/users/xuid({xuid})/devices/current", requestbody);
                    i = 0;
                }
                else
                {
                    if (!active)
                    {
                        break;
                    }
                    i++;
                }
                Thread.Sleep(1000);
            }

            Console.WriteLine("Spoofing stopped.");
        }

        private static void StopSpoofing()
        {
            active = false;
            stopwatch.Stop();
            stopwatch.Reset();
            Console.WriteLine("Spoofing stopped. Currently Spoofing: N/A");
        }

        private static void DisplaySpoofingTime()
        {
            Task.Run(() =>
            {
                while (active)
                {
                    Console.Clear();
                    Console.WriteLine("Currently Spoofing...");
                    Console.WriteLine($"Elapsed Time: {stopwatch.Elapsed:hh\\:mm\\:ss}");
                    Thread.Sleep(1000);
                }
            });
        }
    }
}
