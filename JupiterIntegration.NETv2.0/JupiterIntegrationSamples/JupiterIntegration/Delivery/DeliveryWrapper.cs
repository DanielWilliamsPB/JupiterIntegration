using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.Threading.Tasks;
using System.IO.Compression;
using System.Security.Cryptography;
using HttpUtils;
using System.Diagnostics;

namespace JupiterIntegration
{
    public abstract class DeliveryWrapper
    {
        public static readonly string JUPITER_ACCESS = "WorldShip/rs/S3AccessResponder";

        public string jupiterServer { get; set; }
        public string regionEndpoint { get; set; }
        public string clientDirectory { get; set; }
        public string deliveryDescription { get; set; }
        public string routingMetric { get; set; }
        public string jobCode { get; set; }
        public string userName { get; set; }
        public string password { get; set; }
        public String message { get; protected set; }


        protected string FILELIST_CHECKFILE = "dtp-rec.csv";
        protected string zipFile;
        protected XmlDocument deliveryXml;
        protected XmlDocument accessXml;
        protected byte[] fileHash { get; set; }
        protected XmlNamespaceManager namespaceManager;

        public abstract bool Transfer();
        protected abstract void getAccessFromRootNode(XmlElement element);
        protected abstract void additionalZipFilePartsAttributes(XmlDocument document, XmlElement zipPartsElement);

        private string RECONCILATION_FILE_HEADER = "DocName,Country,Postcode,Copies";
        private RestClient client = null;
        
        public bool setUpForTransfer()
        {
            bool result = false;
            try
            {
                login();
                zipForTransfer(client.userName);
                XMLCreation();

                result = true;
            }
            catch (ApplicationException ae)
            {
                Trace.WriteLine(ae.GetBaseException().Message);
            }
            return result;
        }

        public void login()
        {
            client = new RestClient(@jupiterServer + JUPITER_ACCESS, HttpVerb.GET);
            client.userName = userName;
            client.password = password;

            String response = client.MakeRequest("?restletMethod=get");
            accessXml = new XmlDocument();
            accessXml.LoadXml(response);
            initializeNamespaces();
            getAccessFromRootNode(accessXml.DocumentElement);

        }

        protected void zipForTransfer(string userName)
        {
            Trace.WriteLine(this.GetType().Name + ": " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            message = "Compressing local files";

            if (!Directory.Exists(clientDirectory))
            {
                throw new ApplicationException("Directory not found: " + clientDirectory);
            }

            String[] fileList = Directory.GetFiles(clientDirectory, "*", SearchOption.TopDirectoryOnly);
            if (!File.Exists(clientDirectory + Path.DirectorySeparatorChar + FILELIST_CHECKFILE))
            {
                createReconciliationFile(fileList);
                fileList = Directory.GetFiles(clientDirectory, "*", SearchOption.TopDirectoryOnly);
                if (fileList.Length == 1)
                {
                    throw new ApplicationException("No data found in :" + clientDirectory);
                }
            }

            zipFile = Path.GetTempPath() + createBaseName(userName) + ".zip";
            ZipFile.CreateFromDirectory(clientDirectory, zipFile);
        }

        protected XmlDocument XMLCreation()
        {
            message = "generating XML";
            Trace.WriteLine(this.GetType().Name + ": " + System.Reflection.MethodBase.GetCurrentMethod().Name);

            deliveryXml = new XmlDocument();
            XmlElement delivery = deliveryXml.CreateElement("DELIVERY");

            XmlElement versionElement = deliveryXml.CreateElement("VERSION");
            versionElement.AppendChild(deliveryXml.CreateTextNode("16"));
            delivery.AppendChild(versionElement);

            XmlElement descriptionElement = deliveryXml.CreateElement("DESCRIPTION");
            descriptionElement.AppendChild(deliveryXml.CreateTextNode(deliveryDescription));
            delivery.AppendChild(descriptionElement);

            if (routingMetric != null && routingMetric.Length > 0)
            {
                XmlElement routingMetricElement = deliveryXml.CreateElement("ROUTINGMETRIC");
                routingMetricElement.AppendChild(deliveryXml.CreateTextNode(routingMetric));
                delivery.AppendChild(routingMetricElement);
            }

            XmlElement jobCodeElement = deliveryXml.CreateElement("JOBCODE");
            jobCodeElement.AppendChild(deliveryXml.CreateTextNode(jobCode));
            delivery.AppendChild(jobCodeElement);

            XmlElement zipPartsElement = deliveryXml.CreateElement("ZIPFILEPARTS");
            XmlAttribute md5Attribute = deliveryXml.CreateAttribute("MD5");

            md5Attribute.Value = calculateMD5ForFile(zipFile);
            zipPartsElement.Attributes.Append(md5Attribute);
            additionalZipFilePartsAttributes(deliveryXml, zipPartsElement);

            XmlElement zipElement = deliveryXml.CreateElement("ZIPPART");
            zipElement.AppendChild(deliveryXml.CreateTextNode(Path.GetFileName(zipFile)));
            zipPartsElement.AppendChild(zipElement);

            delivery.AppendChild(zipPartsElement);
            deliveryXml.AppendChild(delivery);
            return deliveryXml;
        }

        protected string calculateMD5(string input)
        {
            message = "Calculating hash";
            MD5 md5Hash = MD5.Create();
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            StringBuilder sBuilder = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            return sBuilder.ToString();
        }

        protected string calculateMD5ForFile(string zipFileName)
        {
            MD5 md5Hash = MD5.Create();
            StringBuilder sBuilder = new StringBuilder();

            using (var stream = File.OpenRead(zipFileName))
            {
                fileHash = md5Hash.ComputeHash(stream);
                for (int i = 0; i < fileHash.Length; i++)
                {
                    sBuilder.Append(fileHash[i].ToString("x2"));
                }
            }

            return sBuilder.ToString();
        }

        private void initializeNamespaces()
        {
            if (namespaceManager == null)
            {
                namespaceManager = new XmlNamespaceManager(accessXml.NameTable);
                namespaceManager.AddNamespace("ns", "x-schema:BatchContent.xsd");
                namespaceManager.AddNamespace("ns2", "x-schema:InventoryOrder.xsd");
                namespaceManager.AddNamespace("ns3", "x-schema:AccessDocument.xsd");
            }
        }

        private void createReconciliationFile(string[] fileList)
        {
            message = "creating reconciliation file";
            using (StreamWriter file = new StreamWriter(@clientDirectory + Path.DirectorySeparatorChar + FILELIST_CHECKFILE))
            {
                file.WriteLine(RECONCILATION_FILE_HEADER);
                foreach (string deliveryFile in fileList)
                {
                    string fileLine = "\"" + Path.GetFileName(deliveryFile) + "\",,,";
                    file.WriteLine(fileLine);
                }
            }
        }

        private String createBaseName(string userName)
        {
            DateTime today = new DateTime(DateTime.Now.Ticks);
            return userName + "-" + today.ToString("yyMMdd-HHmmss");
        }
    }
}