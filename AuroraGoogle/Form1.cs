using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using HtmlAgilityPack;

namespace AuroraGoogle
{
    public partial class Form1 : Form
    {
        Aurora aurora;

        List<Aurora.ScheduleSubject> schedule;

        public Form1()
        {
            InitializeComponent();
        }

        void ShowLoading()
        {

        }

        void HideLoading()
        {

        }

        void UpdateTermsBox(List<Aurora.Term> terms)
        {
            terms_box.DataSource = terms;
            terms_box.DisplayMember = "Name";
            terms_box.ValueMember = "Id";
        }

        async void InitiateRequest()
        {
            ShowLoading();

            var result = await aurora.TryLogin();
            if (result.Successful)
            {
                MessageBox.Show("Login success!");
                var terms = await aurora.GetScheduleTerms();
                UpdateTermsBox(terms);
                button2.Enabled = true;
            }
            else
                MessageBox.Show("Login failure!");

            HideLoading();
        }

        async void GetSchedule(string term)
        {
            schedule = await aurora.GetScheduleForTerm(term);
            button4.Enabled = true;
            foreach (Aurora.ScheduleSubject subject in schedule)
                MessageBox.Show("Prof. " + subject.Professors + " gives class " + subject.Name + " (" + subject.NRC + ") - " + subject.Blocks.Count + " blocks");
        }

        async void ExportScheduleToGoogle()
        {
            if (schedule == null)
                return;

            GoogleCalendar calendar = new GoogleCalendar();
            await calendar.Initialize();

            List<Task<Google.Apis.Calendar.v3.Data.Event>> tasks = new List<Task<Google.Apis.Calendar.v3.Data.Event>>();
            foreach (Aurora.ScheduleSubject subject in schedule)
                tasks.AddRange(calendar.PublishSchedule(subject));

            await Task.WhenAll(tasks);

            MessageBox.Show("Finished importing to Google Calendar");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (aurora != null)
            {
                MessageBox.Show("You must first log out");
                return;
            }

            aurora = new Aurora(username_text.Text, password_text.Text);
            InitiateRequest();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string term = terms_box.SelectedValue as string;
            GetSchedule(term);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            button2.Enabled = false;
            button4.Enabled = false;
            terms_box.DataSource = null;
            aurora.Dispose();
            aurora = null;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            ExportScheduleToGoogle();
        }
    }
}
