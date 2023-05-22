import com.fasterxml.jackson.dataformat.xml.XmlMapper;
import org.apache.http.HttpEntity;
import org.apache.http.HttpStatus;
import org.apache.http.auth.AuthScope;
import org.apache.http.auth.UsernamePasswordCredentials;
import org.apache.http.client.CredentialsProvider;
import org.apache.http.client.methods.CloseableHttpResponse;
import org.apache.http.client.methods.HttpGet;
import org.apache.http.impl.client.BasicCredentialsProvider;
import org.apache.http.impl.client.CloseableHttpClient;
import org.apache.http.impl.client.HttpClientBuilder;
import org.apache.http.util.EntityUtils;
import org.apache.http.client.HttpResponseException;

import java.io.*;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.text.Format;
import java.text.SimpleDateFormat;
import java.util.Base64;
import java.util.Date;
import java.util.zip.ZipEntry;
import java.util.zip.ZipOutputStream;

import org.apache.log4j.Logger;
import software.amazon.awssdk.auth.credentials.AwsBasicCredentials;
import software.amazon.awssdk.auth.credentials.AwsCredentialsProvider;
import software.amazon.awssdk.auth.credentials.StaticCredentialsProvider;
import software.amazon.awssdk.regions.Region;
import software.amazon.awssdk.services.s3.model.*;
import software.amazon.awssdk.services.s3.S3Client;
import software.amazon.awssdk.services.sqs.SqsClient;
import software.amazon.awssdk.services.sqs.model.*;

public class DeliveryWrapper {
    private static Logger log = Logger.getLogger(DeliveryWrapper.class);

    private static String JUPITER_ACCESS = "WorldShip/rs/S3AccessResponder";
    private static String JUPITER_METHOD = "?restletMethod=get";

    public String jupiterServer;
    public String regionEndpoint;
    public String clientDirectory;
    public String deliveryDescription;
    public String jobCode;
    public String userName;
    public String password;

    protected String FILELIST_CHECKFILE = "dtp-rec.csv";
    protected String RECONCILIATION_FILE_HEADER = "DocName,Country,Postcode,Copies";
    protected String zipFile;
    protected String zipFileName;
    protected byte[] fileHash;
    protected JupiterS3Access s3access;
    protected DeliveryDocument deliveryDocument;

    public boolean SetupForTransfer() {
        log.info("Fetch Setup for Transfer");
        boolean result = false;

        try {
            Login();
            ZipFilesForTransfer();
            CreateDeliveryDocument();

            result = true;
        } catch (Exception e) {
            log.error(e.getMessage() == null ? e.toString() : e.getMessage());
        }

        return result;
    }

    private void ZipFilesForTransfer() throws IOException {
        log.info("Compressing local files from " + this.clientDirectory);
        Path path = Paths.get(this.clientDirectory);
        String reconFileName = this.clientDirectory + File.separator + this.FILELIST_CHECKFILE;

        if (!Files.exists(path)) {
            throw new FileNotFoundException("Directory not found: " + this.clientDirectory);
        }

        File[] fileList = new File(this.clientDirectory).listFiles();
        if (fileList != null && fileList.length > 0) {
            if (!new File(reconFileName).isFile()) {
                CreateReconciliationFile(fileList, reconFileName);
                fileList = new File(this.clientDirectory).listFiles();
            } else {
                log.info("Using existing reconciliation file: " + reconFileName);
            }
        } else {
            throw new FileNotFoundException("Directory empty: " + this.clientDirectory);
        }

        //create zip file
        this.zipFileName = createBaseName() + ".zip";
        this.zipFile = System.getProperty("java.io.tmpdir") + this.zipFileName;
        log.info("Creating zip file: " + this.zipFile);

        File zipFile = new File(this.zipFile);
        ZipOutputStream out = new ZipOutputStream(new FileOutputStream(zipFile));
        for (File file : fileList) {
            if (!file.isDirectory()) {
                ZipEntry zipEntry = new ZipEntry(file.getName());
                out.putNextEntry(zipEntry);
                byte[] bytes = Files.readAllBytes(file.toPath());
                out.write(bytes, 0, bytes.length);
                out.closeEntry();
            }
        }
        out.close();
    }

    private String createBaseName() {
        Date today = new Date();

        Format formatter = new SimpleDateFormat("yyMMdd-HHmmss");

        return this.userName + "-" + formatter.format(today);
    }

    private void CreateReconciliationFile(File[] fileList, String reconFileName) throws IOException {
        log.info("Creating reconciliation file: " + reconFileName);

        FileWriter reconFile = new FileWriter(reconFileName);
        BufferedWriter writer = new BufferedWriter(reconFile);
        writer.write(this.RECONCILIATION_FILE_HEADER);
        writer.newLine();
        for (File file : fileList) {
            String fileLine = "\"" + file.getName() + "\",,,";
            writer.write(fileLine);
            writer.newLine();
        }
        writer.flush();
        writer.close();
    }

    private void Login() {
        log.info("Login to Jupiter");

        String uri = this.jupiterServer + this.JUPITER_ACCESS + this.JUPITER_METHOD;
        log.info("Jupiter uri: " + uri);

        HttpGet request = new HttpGet(uri);

        CredentialsProvider provider = new BasicCredentialsProvider();
        provider.setCredentials(
                AuthScope.ANY,
                new UsernamePasswordCredentials(this.userName, this.password)
        );

        CloseableHttpClient httpClient = HttpClientBuilder.create()
                .setDefaultCredentialsProvider(provider)
                .build();

        try {
            CloseableHttpResponse response = httpClient.execute(request);

            HttpEntity entity = response.getEntity();
            if (response.getStatusLine().getStatusCode() == HttpStatus.SC_OK && entity != null) {
                try {
                    XmlMapper xmlMapper = new XmlMapper();
                    s3access = xmlMapper.readValue(EntityUtils.toString(entity), JupiterS3Access.class);
                    log.info(s3access.toString());
                } catch (Exception e) {
                    throw new HttpResponseException(HttpStatus.SC_NOT_FOUND, "s3 access details not found");
                }

            } else {
                throw new HttpResponseException(response.getStatusLine().getStatusCode(), response.getStatusLine().getReasonPhrase());
            }
        } catch (Exception e) {
            log.error(e.toString());
            throw new RuntimeException(e.toString());
        }
    }

    private void CreateDeliveryDocument() throws Exception {
        log.info("Generating Delivery Document");
        deliveryDocument = new DeliveryDocument();
        deliveryDocument.version = "16";
        deliveryDocument.description = this.deliveryDescription;
        deliveryDocument.jobCode = this.jobCode;
        deliveryDocument.zipFileParts = new DeliveryDocument.ZipFileParts();
        deliveryDocument.zipFileParts.MD5 = MD5Checksum.getMD5FileChecksum(this.zipFile);
        this.fileHash = MD5Checksum.createChecksum(this.zipFile);
        deliveryDocument.zipFileParts.bucket = this.s3access.bucket;
        deliveryDocument.zipFileParts.zipPart = this.zipFileName;

        log.info("DeliveryDocument: " + deliveryDocument.toXmlString());
    }

    public boolean Transfer() {
        log.info("Transferring");
        boolean result = false;
        try {
            if (UploadS3()) {
                result = PostSQSMessage();
            }

            result = true;
        } catch (Exception e) {
            log.error(e.getMessage() == null ? e.toString() : e.getMessage());
        } finally {
            File file = new File(this.zipFile);
            if (file.exists()) {
                log.info("Delete zip file: " + this.zipFile);
                file.delete();
            }
        }

        return result;
    }

    private boolean UploadS3() {
        log.info("S3 Upload Start");

        AwsCredentialsProvider credentials = StaticCredentialsProvider.create(AwsBasicCredentials.create(this.s3access.keyid, this.s3access.secretkey));
        S3Client s3Client;

        try {
            s3Client = S3Client
                    .builder()
                    .credentialsProvider(credentials)
                    .region(Region.of(this.regionEndpoint))
                    .build();

            PutObjectRequest request = PutObjectRequest
                    .builder()
                    .bucket(this.s3access.bucket)
                    .contentMD5(new String(Base64.getEncoder().encode(this.fileHash)))
                    .key(this.zipFileName)
                    .build();

            PutObjectResponse response = s3Client.putObject(request,new File(this.zipFile).toPath());

            if (response == null) throw new Exception("S3 PutObject Failed");

            log.info("S3 Upload Finished, s3 eTag: " + response.eTag());

            return true;

        } catch (Exception e) {
            log.error(e.toString());
        }

        return false;
    }

    private boolean PostSQSMessage() {
        log.info("SQS SendMessage Start");

        AwsCredentialsProvider credentials = StaticCredentialsProvider.create(AwsBasicCredentials.create(this.s3access.keyid, this.s3access.secretkey));

        try {
            SqsClient sqsClient = SqsClient
                    .builder()
                    .credentialsProvider(credentials)
                    .region(Region.of(this.regionEndpoint))
                    .build();

            SendMessageRequest request = SendMessageRequest
                    .builder()
                    .messageBody(this.deliveryDocument.toXmlString())
                    .queueUrl(this.s3access.postqueue)
                    .build();

            SendMessageResponse response = sqsClient.sendMessage(request);

            if (response == null) throw new Exception("SQS SendMessage Failed");

            log.info("SQS SendMessage Finished, SQS MessageId: " + response.messageId());

            return true;

        } catch (Exception e) {
            log.error(e.toString());
        }

        return false;
    }
}
