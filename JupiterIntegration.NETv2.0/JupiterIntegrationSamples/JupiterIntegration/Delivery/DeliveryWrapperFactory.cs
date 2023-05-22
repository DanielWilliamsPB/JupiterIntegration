using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JupiterIntegration
{

    public class DeliveryWrapperFactory
    {
        private static DeliveryWrapperFactory instance;
        public static DeliveryWrapperFactory getInstance()
        {
            if (instance == null)
            {
                instance = new DeliveryWrapperFactory();
            }
            return instance;
        }

        public DeliveryWrapper getWrapper(string server, string region)
        {
            return new S3DeliveryWrapper(server, region);
        }
    }
}