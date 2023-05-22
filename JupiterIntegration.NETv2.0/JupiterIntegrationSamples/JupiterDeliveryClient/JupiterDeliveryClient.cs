using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JupiterIntegration;
using System.Security.Cryptography.X509Certificates;
using System.Diagnostics;
using System.Net;
using System.Configuration;

class JupiterDeliveryClient
    {
        static void Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            
            Trace.Listeners.Clear();
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));


            string server = ConfigurationManager.AppSettings["jupiterServer"];
            string region = ConfigurationManager.AppSettings["amazonRegionEndpoint"];
            DeliveryWrapper wrapper = DeliveryWrapperFactory.getInstance().getWrapper(server, region);

            //  ConfigurationManager.AppSettings["MySetting"]
            wrapper.clientDirectory = ConfigurationManager.AppSettings["clientDirectory"];
            wrapper.deliveryDescription = ConfigurationManager.AppSettings["deliveryDescription"];
            wrapper.jobCode = ConfigurationManager.AppSettings["jobCode"];
            wrapper.userName = ConfigurationManager.AppSettings["userName"];
            wrapper.password = ConfigurationManager.AppSettings["password"];

            if (wrapper.setUpForTransfer())
            {
                bool transferred = wrapper.Transfer();
                Trace.WriteLine("Transfer: " + (transferred ? "success" : "failure"));
        }
        else
            {
                Trace.WriteLine("Setup: failure");
        }

            Trace.WriteLine("End");
    }
    }
