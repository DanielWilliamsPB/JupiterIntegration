using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Security.Cryptography;
using System.IO;
using System.IO.Compression;
using System.Net;

using HttpUtils;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Diagnostics;
using Amazon.Runtime;
using System.Threading;
using Amazon;
using System.Configuration;

namespace JupiterIntegration
{
    public class S3DeliveryWrapper : DeliveryWrapper
    {
        private string keyId;
        private string secretKey;
        private string bucket;
        private string postQueue;

        public EventHandler<StreamTransferProgressArgs> transferProgressHandler { get; set; }

        public S3DeliveryWrapper(String server, string region)
        {
            jupiterServer = server;
            regionEndpoint = region;
        }

        protected override void getAccessFromRootNode(XmlElement root)
        {
            keyId = root.SelectSingleNode("//ns3:keyid", namespaceManager).InnerXml;
            secretKey = root.SelectSingleNode("//ns3:secretkey", namespaceManager).InnerXml;
            bucket = root.SelectSingleNode("//ns3:bucket", namespaceManager).InnerXml;
            postQueue = root.SelectSingleNode("//ns3:postqueue", namespaceManager).InnerXml;
        }

        public void threadedTransfer()
        {
            if (setUpForTransfer()) {
                Transfer();
            }
        }
        public override bool Transfer()
        {
            message = "Transferring";
            bool result = false;
            try
            {
                if (synchronousUpload())
                {
                    result = postSQSMessage();
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.GetBaseException().Message + ": " + e.Message);
            }
            finally
            {
                if (File.Exists(zipFile))
                {
                    File.Delete(zipFile);
                }
            }
            return result;
        }

        protected override void additionalZipFilePartsAttributes(XmlDocument document, XmlElement zipPartsElement)
        {
            if (bucket != null)
            {
                XmlAttribute bucketAttribute = document.CreateAttribute("bucket");
                bucketAttribute.Value = bucket;
                zipPartsElement.Attributes.Append(bucketAttribute);
            }
        }


        private bool synchronousUpload()
        {
            Trace.WriteLine(this.GetType().Name + ": " + System.Reflection.MethodBase.GetCurrentMethod().Name);

            AmazonS3Client client = new AmazonS3Client(keyId, secretKey, Amazon.RegionEndpoint.GetBySystemName(regionEndpoint));
            PutObjectRequest request = new PutObjectRequest();
            if (transferProgressHandler != null) {
                request.StreamTransferProgress += transferProgressHandler;
            }

            request.MD5Digest = Convert.ToBase64String(fileHash);
            request.BucketName = bucket;
            request.FilePath = zipFile;

            PutObjectResponse response = client.PutObject(request);
            return (response.HttpStatusCode == System.Net.HttpStatusCode.OK);
        }

        private bool postSQSMessage()
        {
            Trace.WriteLine(this.GetType().Name + ": postSQSMessage");
            message = "Finalising tranfer";

            bool result = true;
            AmazonSQSClient sqs = new AmazonSQSClient(keyId, secretKey, Amazon.RegionEndpoint.GetBySystemName(regionEndpoint));
            SendMessageRequest sendmessage = new SendMessageRequest();

            String md5Body = calculateMD5(deliveryXml.OuterXml);
            sendmessage.MessageBody = deliveryXml.OuterXml;
            sendmessage.QueueUrl = postQueue;

            try
            {
                SendMessageResponse response = sqs.SendMessage(sendmessage);
                if (!response.MD5OfMessageBody.Equals(md5Body))
                {
                    result = false;
                }

            }
            catch (Amazon.Runtime.AmazonServiceException ase)
            {
                Trace.WriteLine(ase.GetBaseException().Message);
                Trace.Write(ase.GetBaseException().StackTrace);
            }

            return result;
        }
    }
}