using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AuroraGoogle
{
    public class GoogleCalendar
    {
        static string[] Scopes = { CalendarService.Scope.Calendar };
        static string ApplicationName = "Schedule Importer";

        CalendarService service;
        UserCredential credential;

        public GoogleCalendar()
        {
        }

        public List<Task<Event>> PublishSchedule(Aurora.ScheduleSubject subject)
        {
            var tasks = new List<Task<Event>>();
            foreach (Aurora.ScheduleSubject.Block block in subject.Blocks)
            {
                var start_date = new DateTime(block.StartDate.Year, block.StartDate.Month, block.StartDate.Day, block.StartHour.Hour, block.StartHour.Minute, block.StartHour.Second);
                var end_hour = block.StartHour.Add(block.Duration);
                var description = "Class in " + block.Location + " with " + subject.Professors;
                var until = block.EndDate.Year.ToString() + block.EndDate.Month.ToString("00") + block.EndDate.Day.ToString("00") +
                    "T" + end_hour.Hour.ToString("00") + end_hour.Minute.ToString("00") + end_hour.Second.ToString("00") + "Z";
                var ev = new Event()
                {
                    Summary = subject.Name,
                    Description = description,
                    Start = new EventDateTime() {
                        DateTime = start_date,
                        TimeZone = "America/Bogota"
                    },
                    End = new EventDateTime() {
                        DateTime = start_date.Add(block.Duration),
                        TimeZone = "America/Bogota"
                    },
                    Recurrence = new String[] {
                        "RRULE:FREQ=WEEKLY;UNTIL=" + until
                    },
                    Location = "Universidad del Norte, Barranquilla, Colombia"
                };

                tasks.Add(service.Events.Insert(ev, "primary").ExecuteAsync());
            }
            return tasks;
        }

        public async Task Initialize()
        {
            using (var stream =
                new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
            {
                string credPath = System.Environment.GetFolderPath(
                    System.Environment.SpecialFolder.Personal);
                credPath = Path.Combine(credPath, ".credentials");

                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true));
            }

            // Create Google Calendar API service.
            service = new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });
        }
    }
}
