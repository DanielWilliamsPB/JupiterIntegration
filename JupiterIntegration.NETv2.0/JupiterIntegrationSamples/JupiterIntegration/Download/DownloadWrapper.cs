using HttpUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace JupiterIntegration
{
    public abstract class DownloadWrapper
    {
        public static readonly string JUPITER_ACCESS = "WorldShip/rs/BatchAvailableResponder";
        public static readonly string JUPITER_STATUS_UPDATER = "WorldShip/rs/BatchStatusChangeResponder";        
        public static readonly string BATCH_STATUS_REQUESTED = "batchStatusRequested";
        public static readonly string BATCH_CHANGE_ELAPSED_HOURS = "batchChangeElapsedHours";
        public static readonly string STATUS = "STATUS";
        public static readonly string DELIVERY = "DELIVERY";
        public static readonly string COMPANY_STATUS = "companyStatus";

        public static readonly int STATUS_CLOSED = 15;   
	    public static readonly int STATUS_DOWNLOADING = 25;   
	    public static readonly int STATUS_DOWNLOAD_ERROR = 30;   
	    public static readonly int STATUS_READY_TO_PRINT = 35;   	
	    public static readonly int STATUS_PRINTED = 40;   
	    public static readonly int STATUS_DISPATCHED = 45;

	    public static readonly int COMPANY_LIVE = 0;
	    public static readonly int COMPANY_UAT = 1;
        public static readonly int COMPANY_TEST = 2;

        public XmlDocument batchContent;

        public string jupiterServer { get; set; }
        public string regionEndpoint { get; set; }
        public string clientDirectory { get; set; }
        public string userName { get; set; }
        public string password { get; set; }
        public int batchStatus { get; set; }
        public int companyStatus { get; set; }
        public int batchStatusChangeHours { get; set; } = 24;

        public bool unzip { get; set; }
        public bool writeXml { get; set; }
        public bool isTransactionQuery { get; set; } = false;

        protected abstract bool performTheTransfer(string remoteFilePath, string localFile);
        protected abstract void getAccessFromRootNode(XmlElement root);
        protected abstract String getRemoteFilePath(String user, string batch, string delivery);

        public XmlNodeList batchItemList { get; set;  }
        protected XmlNamespaceManager namespaceManager;
        protected byte[] fileHash { get; set; }

        public bool setUpForTransfer()
        {
            bool result = false;
            RestClient client = new RestClient(@jupiterServer + JUPITER_ACCESS, HttpVerb.GET);
            client.userName = userName;
            client.password = password;

            Dictionary<string, string> headerMap = new Dictionary<string, string>();
            headerMap.Add(BATCH_STATUS_REQUESTED, batchStatus.ToString());
            headerMap.Add(COMPANY_STATUS, companyStatus.ToString());
            if (isTransactionQuery)
            {
                headerMap.Add(BATCH_CHANGE_ELAPSED_HOURS, batchStatusChangeHours.ToString());
            }
            client.headerMap = headerMap;

            try
            {
                String response = client.MakeRequest("?restletMethod=get");

                batchContent = new XmlDocument();
                batchContent.LoadXml(response);
                initializeNamespaces();

                XmlElement root = batchContent.DocumentElement;
                batchItemList = root.SelectNodes("//ns:BatchItem", namespaceManager);
                if (!isTransactionQuery) {
                    getAccessFromRootNode(root);
                }

                result = true;
            }
            catch (ApplicationException ae)
            {
                Trace.WriteLine(ae.GetBaseException().Message);
            }
            return result;
        }

        public int[] Transfer()
        {
            int[] result = { 0, 0 };
            if (batchItemList.Count == 0)
            {
                return result;
            }

            result[0] = batchItemList.Count;
            foreach (XmlNode node in batchItemList)
            {
                string delivery = node.SelectSingleNode("ns:deliveryUid", namespaceManager).InnerXml;
                string batch = node.SelectSingleNode("ns:batchUid", namespaceManager).InnerXml;
                try
                {
                    string remoteFilePath = getRemoteFilePath(userName, batch, delivery);
                    string transferDir = setupTransferFolder(delivery);
                    if (updateBatchStatus(Convert.ToInt32(delivery), STATUS_DOWNLOADING))
                    {
                        string localFile = transferDir + Path.DirectorySeparatorChar + Path.GetFileName(remoteFilePath);

                        if (performTheTransfer(remoteFilePath, localFile) &&
                            updateBatchStatus(Convert.ToInt32(delivery), STATUS_READY_TO_PRINT))
                        {
                            if (unzip)
                            {
                                unzipContent(localFile);
                            }
                            if (writeXml)
                            {
                                string localXmlFile = Path.Combine(Path.GetDirectoryName(localFile), Path.GetFileNameWithoutExtension(localFile) + ".xml");
                                writeBatchItem(node, localXmlFile);
                            }
                            result[1]++;
                        }
                    }

                }
                catch (ApplicationException ae)
                {
                    Trace.WriteLine(ae.GetBaseException().Message);
                    break;
                }
            }

            return result;
        }

        public void unzipContent(string localFile)
        {
            Trace.WriteLine(this.GetType().Name + ": " + System.Reflection.MethodBase.GetCurrentMethod().Name);
            ZipFile.ExtractToDirectory(localFile, Path.GetDirectoryName(localFile));
        }

        public bool updateBatchStatus(int delivery, int status)
        {
            Trace.WriteLine(this.GetType().Name + ": " + System.Reflection.MethodBase.GetCurrentMethod().Name);

            bool result = false;
            RestClient client = new RestClient(@jupiterServer + JUPITER_STATUS_UPDATER, HttpVerb.GET);
            client.userName = userName;
            client.password = password;

            Dictionary<string, string> headerMap = new Dictionary<string, string>();
            headerMap.Add(DELIVERY, delivery.ToString());
            headerMap.Add(STATUS, status.ToString());
            client.headerMap = headerMap;

            try
            {
                String response = client.MakeRequest("?restletMethod=get");

                batchContent = new XmlDocument();
                batchContent.LoadXml(response);
                initializeNamespaces();

                XmlElement root = batchContent.DocumentElement;
                result = (root.SelectSingleNode("//ns:status", namespaceManager).InnerXml.Equals(status.ToString()));
            }
            catch (ApplicationException ae)
            {
                throw new ApplicationException("Cannot update jupiter status: " + ae.Message);
            }
            return result;
        }

        protected string setupTransferFolder(string deliveryUid)
        {

            string transferDir = clientDirectory + Path.DirectorySeparatorChar + deliveryUid;
            if (Directory.Exists(transferDir))
            {
                throw new ApplicationException("Cannot initiate transfer, this batch already exists.\nPlease remove it to continue: " + deliveryUid);
            }
            else
            {
                Directory.CreateDirectory(transferDir);
            }
            return transferDir;
        }

        private void writeBatchItem(XmlNode node, String xmlFile)
        {
            using (XmlTextWriter writer = new XmlTextWriter(xmlFile, Encoding.UTF8))
            {
                writer.Formatting = Formatting.Indented;
                node.WriteTo(writer);
            }
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
                namespaceManager = new XmlNamespaceManager(batchContent.NameTable);
                namespaceManager.AddNamespace("ns", "x-schema:BatchContent.xsd");
                namespaceManager.AddNamespace("ns2", "x-schema:InventoryOrder.xsd");
                namespaceManager.AddNamespace("ns3", "x-schema:AccessDocument.xsd");
            }
        }


    }
}