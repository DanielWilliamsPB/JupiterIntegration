using JupiterIntegration;
using Microsoft.SqlServer.Server;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.Runtime.Remoting.Channels;
using System.Security.Cryptography.X509Certificates;


    class JupiterDownloadClient
    {
        static void Main(string[] args)
        {
            string server = ConfigurationManager.AppSettings["jupiterServer"];
            string region = ConfigurationManager.AppSettings["amazonRegionEndpoint"];
            
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            
            Download(server, region);
        }

        private static void Download(string server, string region)
        {
            Trace.Listeners.Clear();
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));


            DownloadWrapper wrapper = DownloadWrapperFactory.getInstance().getWrapper(server, region);

            wrapper.batchStatus = DownloadWrapper.STATUS_CLOSED;
            wrapper.companyStatus = DownloadWrapper.COMPANY_LIVE;
            
            wrapper.userName = ConfigurationManager.AppSettings["userName"];
            wrapper.password = ConfigurationManager.AppSettings["password"];
            wrapper.clientDirectory = ConfigurationManager.AppSettings["clientDirectory"];
            wrapper.unzip = true;
            wrapper.writeXml = true;

            if (wrapper.setUpForTransfer())
            {
                int[] availableVsTransferred = wrapper.Transfer();
                Trace.WriteLine("Transfer: " + availableVsTransferred[1] + " transferred of " + availableVsTransferred[0] + " available");
            }
            else
            {
                Trace.WriteLine("Setup: failure");
            }

            Trace.WriteLine("End");
        }

        private static void ChangeStatus(int delivery, int status, string server, string region)
        {

            DownloadWrapper wrapper = DownloadWrapperFactory.getInstance().getWrapper(server, region);
            wrapper.userName = "";
            wrapper.password = "";
            wrapper.updateBatchStatus(delivery, status);

        }


    }