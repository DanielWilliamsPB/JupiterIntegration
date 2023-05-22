import org.apache.log4j.Logger;
import org.apache.log4j.PropertyConfigurator;

public class JupiterDeliveryClient {
    private static Logger log = Logger.getLogger(JupiterDeliveryClient.class);

    public static void main(String[] args) {
        PropertyConfigurator.configure("log4j.properties");

        DeliveryWrapper wrapper = new DeliveryWrapper();

        //https Jupiter Server
        wrapper.jupiterServer = "https://relayhubus.com";
        //Amazon region endpoint (e.g. eu-west-2, us-east-1)
        wrapper.regionEndpoint = "us-east-2";
        //Directory containing content for print
        wrapper.clientDirectory = "";
        //Narrative for client use
        wrapper.deliveryDescription = "";
        //Pre-agreed code allocated by Jupiter
        wrapper.jobCode = "";
        //delivery user details
        wrapper.userName = "";
        wrapper.password = "";

        if (wrapper.SetupForTransfer()) {
            boolean transferred = wrapper.Transfer();
            log.info("Transfer: " + (transferred ? "success": "failed"));
        } else {
            log.error("Setup: failed");
        }
    }
}
