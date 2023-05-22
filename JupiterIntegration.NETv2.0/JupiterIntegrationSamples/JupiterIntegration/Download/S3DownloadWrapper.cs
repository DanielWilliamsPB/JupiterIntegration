using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace JupiterIntegration
{
    public class S3DownloadWrapper : DownloadWrapper
    {
        private string keyId;
        private string secretKey;
        private string bucket;

        public S3DownloadWrapper(string server, string region)
        {
            jupiterServer = server;
            regionEndpoint = region;
        }

        protected override void getAccessFromRootNode(XmlElement root)
        {
            keyId = root.SelectSingleNode("//ns3:keyid", namespaceManager).InnerXml;
            secretKey = root.SelectSingleNode("//ns3:secretkey", namespaceManager).InnerXml;
            bucket = root.SelectSingleNode("//ns3:bucket", namespaceManager).InnerXml;
        }


        protected override bool performTheTransfer(string remoteFilePath, string localFile)
        {
            Trace.WriteLine(this.GetType().Name + ": " + System.Reflection.MethodBase.GetCurrentMethod().Name);

            bool result = false;
            AmazonS3Client client = new AmazonS3Client(keyId, secretKey, Amazon.RegionEndpoint.GetBySystemName(regionEndpoint));
            GetObjectRequest request = new GetObjectRequest();
            request.BucketName = bucket;
            request.Key = remoteFilePath;

            using (GetObjectResponse response = client.GetObject(request))
            {
                if (!File.Exists(localFile))
                {
                    response.WriteResponseStreamToFile(localFile);
                    result = true;
                }
            }
            return result;
        }

        protected override string getRemoteFilePath(string user, string batch, string delivery)
        {
            StringBuilder remoteFilePath = new StringBuilder();
            remoteFilePath.Append(user);
            remoteFilePath.Append("/");
            remoteFilePath.Append(batch);
            remoteFilePath.Append(".");
            remoteFilePath.Append(delivery);
            remoteFilePath.Append(".zip");
            return remoteFilePath.ToString();
        }
    }
}
