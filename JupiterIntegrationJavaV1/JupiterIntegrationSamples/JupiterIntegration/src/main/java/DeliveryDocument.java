import com.fasterxml.jackson.annotation.JsonPropertyOrder;
import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.dataformat.xml.XmlMapper;
import com.fasterxml.jackson.dataformat.xml.annotation.JacksonXmlProperty;
import com.fasterxml.jackson.dataformat.xml.annotation.JacksonXmlRootElement;

@JacksonXmlRootElement(localName = "DELIVERY")
@JsonPropertyOrder({"VERSION","DESCRIPTION","JOBCODE","ZIPFILEPARTS"})
public class DeliveryDocument
{
    @JacksonXmlProperty(localName = "VERSION")
    public String version;

    @JacksonXmlProperty(localName = "DESCRIPTION")
    public String description;

    @JacksonXmlProperty(localName = "JOBCODE")
    public String jobCode;

    @JacksonXmlProperty(localName = "ZIPFILEPARTS")
    public ZipFileParts zipFileParts;

    public DeliveryDocument() {
        zipFileParts = new ZipFileParts();
    }

    public static class ZipFileParts
    {
        @JacksonXmlProperty(isAttribute=true, localName = "bucket")
        public String bucket;

        @JacksonXmlProperty(isAttribute=true, localName = "MD5")
        public String MD5;

        @JacksonXmlProperty(localName = "ZIPPART")
        public String zipPart;
    }

    public String toXmlString() throws JsonProcessingException {
        ObjectMapper objectMapper = new XmlMapper();
        return objectMapper.writeValueAsString(this);
    }
}
