using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AuroraGoogle
{
    public class Aurora : IDisposable
    {
        public struct Term
        {
            public string Name { get; set; }
            public string Id { get; set; }
        }

        public struct LoginResult
        {
            public bool Successful;
            public string Body;
        }

        public struct ScheduleSubject
        {
            public struct Block
            {
                public DayOfWeek Day;
                public DateTime StartHour;
                public TimeSpan Duration;
                public DateTime StartDate;
                public DateTime EndDate;
                public string Location;
            }
            public string Name { get; set; }
            public string Professors { get; set; }
            public string NRC { get; set; }
            public List<Block> Blocks;
        }

        CookieContainer cookie_container;
        HttpClientHandler client_handler;
        HttpClient client;

        string username;
        string password;

        Uri base_address = new Uri("https://pomelo.uninorte.edu.co");

        public Aurora(string user, string pass)
        {
            cookie_container = new CookieContainer();
            client_handler = new HttpClientHandler();
            client_handler.UseCookies = true;
            client_handler.CookieContainer = cookie_container;
            client = new HttpClient(client_handler);
            client.BaseAddress = base_address;
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.3; Win64; x64; rv:42.0) Gecko/20100101 Firefox/42.0");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
            client.DefaultRequestHeaders.Add("Connection", "keep-alive");
            client.DefaultRequestHeaders.Add("Host", "pomelo.uninorte.edu.co");
            client.DefaultRequestHeaders.Add("Referer", "https://pomelo.uninorte.edu.co/pls/prod/twbkwbis.P_ValLogin");

            username = user;
            password = pass;
        }

        DayOfWeek GetDayOfWeek(char letter)
        {
            switch (letter)
            {
                case 'L':
                    return DayOfWeek.Monday;
                case 'M':
                    return DayOfWeek.Tuesday;
                case 'I':
                    return DayOfWeek.Wednesday;
                case 'J':
                    return DayOfWeek.Thursday;
                case 'V':
                    return DayOfWeek.Friday;
                case 'S':
                    return DayOfWeek.Saturday;
            }

            return DayOfWeek.Sunday;
        }

        void ClearCookies()
        {
            var cookies = cookie_container.GetCookies(base_address);
            foreach (Cookie cookie in cookies)
                cookie.Expired = true;
        }

        void SaveCookies(HttpResponseMessage response)
        {
            IEnumerable<string> cookies;
            if (!response.Headers.TryGetValues("Set-Cookie", out cookies))
                return;

            foreach (string cookie in cookies)
                cookie_container.SetCookies(base_address, cookie);
        }

        bool IsLoginSuccess(string response)
        {
            return !response.Contains("Usuario o Clave invalido");
        }

        public async Task<LoginResult> TryLogin()
        {
            // username = sid; password = PIN
            var postData = new List<KeyValuePair<string, string>>();
            postData.Add(new KeyValuePair<string, string>("sid", username));
            postData.Add(new KeyValuePair<string, string>("PIN", password));

            HttpContent content = new FormUrlEncodedContent(postData);
            ClearCookies();
            cookie_container.Add(base_address, new Cookie("TESTID", "set"));
            var response = await client.PostAsync("/pls/prod/twbkwbis.P_ValLogin", content);

            string body = "";
            if (response.IsSuccessStatusCode)
            {
                SaveCookies(response);
                body = await response.Content.ReadAsStringAsync();
                if (IsLoginSuccess(body))
                    return new LoginResult { Successful = true, Body = body };
            }

            return new LoginResult { Successful = false, Body = body };
        }

        public List<Term> ParseScheduleTerms(string body)
        {
            List<Term> Terms = new List<Term>();
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(body);

            var options = doc.DocumentNode.SelectNodes("//select[@id='term_id']/option");
            foreach (HtmlNode option in options)
            {
                var text = option.NextSibling.InnerText;
                Terms.Add(new Term { Name = text, Id = option.GetAttributeValue("value", "0") });
            }
            return Terms;
        }

        public async Task<List<Term>> GetScheduleTerms()
        {
            var response = await client.GetAsync("/pls/prod/bwskfshd.P_CrseSchdDetl");
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                return await Task.Run(() => ParseScheduleTerms(body));
            }
            return null;
        }

        public List<ScheduleSubject> ParseSchedule(string html)
        {
            List<ScheduleSubject> subjects = new List<ScheduleSubject>();
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);

            var tables = doc.DocumentNode.SelectNodes("//table[@class='datadisplaytable'][position() mod 2 = 1]");
            foreach (HtmlNode table in tables)
            {
                ScheduleSubject subject = new ScheduleSubject();

                var caption = table.FirstChild;
                subject.Name = caption.InnerText;

                var names_td = table.SelectNodes("./tr[4]/td/text()");
                subject.Professors = "";
                foreach (HtmlNode node in names_td)
                    subject.Professors += node.InnerText.Replace("\n", "");

                var nrc_td = table.SelectSingleNode("./tr[2]/td");
                subject.NRC = nrc_td.InnerText;

                var blocks_html = table.NextSibling.NextSibling;
                var rows = blocks_html.SelectNodes("./tr[position() > 1]");

                subject.Blocks = new List<ScheduleSubject.Block>();

                foreach (HtmlNode row in rows)
                {
                    var days_td = row.SelectSingleNode("./td[3]");
                    var days = days_td.FirstChild.InnerText.Split(' ');
                    foreach (string day in days)
                    {
                        if (day.Trim() == "")
                            continue;

                        ScheduleSubject.Block block = new ScheduleSubject.Block();
                        block.Day = GetDayOfWeek(day.ToCharArray()[0]);
                        var hours_string = days_td.PreviousSibling.PreviousSibling.FirstChild.InnerText.Split('-');
                        block.StartHour = DateTime.Parse(hours_string[0].Trim());
                        block.Duration = DateTime.Parse(hours_string[1].Trim()) - block.StartHour;

                        var location_td = days_td.NextSibling.NextSibling;
                        block.Location = location_td.FirstChild.InnerHtml;

                        var dates_string = location_td.NextSibling.NextSibling.FirstChild.InnerHtml.Split('-');
                        // For some reason, some months are in spanish and others in english
                        dates_string[0] = dates_string[0].Replace("Ene", "Jan");
                        block.StartDate = DateTime.Parse(dates_string[0].Trim());

                        while (block.StartDate.DayOfWeek != block.Day)
                            block.StartDate = block.StartDate.AddDays(1);

                        block.EndDate = DateTime.Parse(dates_string[1].Trim());

                        while (block.EndDate.DayOfWeek != block.Day)
                            block.EndDate = block.EndDate.AddDays(-1);

                        subject.Blocks.Add(block);
                    }
                }

                subjects.Add(subject);
            }

            return subjects;
        }

        public async Task<List<ScheduleSubject>> GetScheduleForTerm(string term)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("term_in", term)
            });
            var response = await client.PostAsync("/pls/prod/bwskfshd.P_CrseSchdDetl", content);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                return await Task.Run(() => ParseSchedule(body));
            }

            return null;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    client.Dispose();
                    client_handler.Dispose();
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
