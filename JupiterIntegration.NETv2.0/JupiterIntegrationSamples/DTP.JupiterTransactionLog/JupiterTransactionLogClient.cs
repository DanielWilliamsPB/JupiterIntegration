using JupiterIntegration;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace DTP.JupiterTransactionLog
{
    class JupiterTransactionLogClient
    {
        static void Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            string server = ConfigurationManager.AppSettings["jupiterServer"];
            string region = ConfigurationManager.AppSettings["amazonRegionEndpoint"];

            DownloadTransactions(server, region);
        }

        private static void DownloadTransactions(string server, string region)
        {
            Trace.Listeners.Clear();
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

            DownloadWrapper wrapper = DownloadWrapperFactory.getInstance().getWrapper(server, region);

            wrapper.batchStatus = DownloadWrapper.STATUS_CLOSED;
            wrapper.companyStatus = DownloadWrapper.COMPANY_LIVE;
            wrapper.userName = ConfigurationManager.AppSettings["userName"];
            wrapper.password = ConfigurationManager.AppSettings["password"];
            wrapper.isTransactionQuery = true;
            wrapper.batchStatusChangeHours = 24;

            if (wrapper.setUpForTransfer())
            {
                Trace.WriteLine("Transferred content: " );

                StringWriter sw = new StringWriter();
                wrapper.batchContent.Save(sw);

                // Write the XML received to the console. More usually, parse the XML node list of wrapper.batchContent
                // and retrieve all data of interest for updating of internal systems where required.
                Trace.WriteLine(sw.ToString());
            }
            else
            {
                Trace.WriteLine("Setup: failure");
            }

            Trace.WriteLine("End");
        }
    }
}
