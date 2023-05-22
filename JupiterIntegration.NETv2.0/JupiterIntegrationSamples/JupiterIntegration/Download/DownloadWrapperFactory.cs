using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JupiterIntegration
{
    public class DownloadWrapperFactory
    {
        private static DownloadWrapperFactory instance;
        public static DownloadWrapperFactory getInstance()
        {
            if (instance == null)
            {
                instance = new DownloadWrapperFactory();
            }
            return instance;
        }

        public DownloadWrapper getWrapper(string server, string region)
        {
            return new S3DownloadWrapper(server, region);
        }
    }
}