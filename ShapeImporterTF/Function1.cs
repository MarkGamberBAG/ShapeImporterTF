using System;
using System.Net;
using System.Security.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

/*
    Shape Importer timer function, the purpose of which is to hit
    the shape importer web app at a specific interval causing it
    to download and import shape data from whatever FTP site. This
    function can work against multiple sites if in a comma
    delimited list.
*/

namespace ShapeImporterTF
{
    public class ShapeImporterTimer
    {
        private readonly Microsoft.Extensions.Logging.ILogger _logger;

        public ShapeImporterTimer(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ShapeImporterTimer>();
        }

        // ========================================================================================

        [Function("ShapeImporterTimer")]
        public void Run([TimerTrigger("%TriggerTime%")] TimerInfo myTimer)
        {
            string? sAllURLs = Environment.GetEnvironmentVariable("TestURLs");
            if (!string.IsNullOrEmpty(sAllURLs))
            {
                string[] sURLs = sAllURLs.Split(',');
                foreach (string sURL in sURLs)
                {
                    HttpClient client = new HttpClient();
                    client.BaseAddress = new Uri(sURL);
                    SendGetToURL(sURL).Wait();
                }
            }
        }

        // ========================================================================================

        public async Task SendGetToURL(string sURL)
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage msg = await client.GetAsync(sURL);
            client.Dispose();
            if (msg.StatusCode != HttpStatusCode.OK)
                SLog("URL " + sURL + " returned a status code of " + msg.StatusCode.ToString(), LogEventLevel.Error);
            else
                SLog("URL " + sURL + " returned a status code of " + msg.StatusCode.ToString(), LogEventLevel.Information);
        }

        // ========================================================================================

        public void SLog(string sMessage, LogEventLevel logLevel, string sTags = "", string? sHost = "", string? sService = "")
        {
            if (string.IsNullOrEmpty(sHost)) sHost = Environment.GetEnvironmentVariable("LogHost");
            if (string.IsNullOrEmpty(sService)) sService = Environment.GetEnvironmentVariable("LogService");
            string? sAPIKey = Environment.GetEnvironmentVariable("DD_API_KEY");
            string[] sTagArray = new string[] { };
            if (!string.IsNullOrEmpty(sTags))
                sTagArray = sTags.Split(',');
            string? sMinimum = Environment.GetEnvironmentVariable("MinLevel");
            if (!string.IsNullOrEmpty(sMinimum)) sMinimum = sMinimum.ToLower();
            else sMinimum = "";
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)0xc00;
            var log = new LoggerConfiguration();
            log.WriteTo.DatadogLogs(sAPIKey, source: "csharp", host: sHost, service: sService, tags: sTagArray);
            if (sMinimum.Equals("verbose")) log.MinimumLevel.Verbose();
            else if (sMinimum.Equals("debug")) log.MinimumLevel.Debug();
            else if (sMinimum.Equals("error")) log.MinimumLevel.Error();
            else if (sMinimum.Equals("fatal")) log.MinimumLevel.Fatal();
            else if (sMinimum.Equals("info")) log.MinimumLevel.Information();
            else if (sMinimum.Equals("warning")) log.MinimumLevel.Warning();
            else log.MinimumLevel.Verbose();
            var logger = log.CreateLogger();
            logger.Write(logLevel, DateTime.Now.ToString() + " " + logLevel.ToString() + ": " + sMessage);
            logger.Dispose();
        }
    }
}
