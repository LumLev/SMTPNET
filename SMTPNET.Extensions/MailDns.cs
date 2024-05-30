using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SMTPNET.MailDns
{



    public record MXRecord
    {
        public MXRecord() { }
        public int preference = -1;
        public string? mailServer;
        public string? dnsServer;

        public override string ToString()
        {
            return "Preference: " + preference.ToString().PadLeft(3) + " MailServer: " + mailServer;
        }
    }

    public ref struct MailDns
    {
        private byte[] data;
        private int position, id, length;
        private string name;
        private static string dnsServer { get; set; } = "8.8.8.8";

        private static int DNS_PORT = 53; 

      
        public MailDns(string primaryDnsServer = "8.8.8.8")
        {
            id = DateTime.Now.Millisecond * 60;
            data = new byte[4096];
            name = "";
        }

        public List<MXRecord> GetMXRecords(string host)
        {
            List<MXRecord> mxRecords = new();
                try
                {
                    mxRecords = GetMXRecordss(host,dnsServer);

                }
                catch { }           
            return mxRecords;
        }
        private int getNewId()
        {
            return ++id;
        }

        public List<MXRecord> GetMXRecordss(string host, string serverAddress)
        {

             UdpClient dnsClient = new UdpClient(serverAddress, DNS_PORT);
            dnsClient.Client.ReceiveTimeout = 5_000; // in milliseconds

            //preparing the DNS query packet.
            MakeQuery(getNewId(), host);
            try
            {
                dnsClient.Send(data);

                IPEndPoint? endpoint = null;
                data = dnsClient.Receive(ref endpoint);

                length = data.Length;
                return MakeResponse(serverAddress);
            }
            catch (Exception ex)
            {
                throw new IOException("DNS server timeout (details: " + ex.ToString());
            }
        }

        //for packing the information to the format accepted by server
        public void MakeQuery(int id, String name)
        {
            data = new byte[512];

            for (int i = 0; i < 512; ++i)
            {
                data[i] = 0;
            }

            data[0] = (byte)(id >> 8);
            data[1] = (byte)(id & 0xFF);
            data[2] = (byte)1; data[3] = (byte)0;
            data[4] = (byte)0; data[5] = (byte)1;
            data[6] = (byte)0; data[7] = (byte)0;
            data[8] = (byte)0; data[9] = (byte)0;
            data[10] = (byte)0; data[11] = (byte)0;

            string[] tokens = name.Split(new char[] { '.' });
            string label;

            position = 12;

            for (int j = 0; j < tokens.Length; j++)
            {

                label = tokens[j];
                data[position++] = (byte)(label.Length & 0xFF);
                byte[] b = Encoding.ASCII.GetBytes(label);

                for (int k = 0; k < b.Length; k++)
                {
                    data[position++] = b[k];
                }

            }

            data[position++] = (byte)0; data[position++] = (byte)0;
            data[position++] = (byte)15; data[position++] = (byte)0;
            data[position++] = (byte)1;

        }



        public MXRecord? GetFirstMX(string host)
        {

            UdpClient dnsClient = new UdpClient(dnsServer, DNS_PORT);
            dnsClient.Client.ReceiveTimeout = 5_000; // in milliseconds

            //preparing the DNS query packet.
            MakeQuery(getNewId(), host);
            try
            {
                dnsClient.Send(data);

                IPEndPoint? endpoint = null;
                data = dnsClient.Receive(ref endpoint);

                length = data.Length;
                return MakeSingleMXResponse(dnsServer);
            }
            catch (Exception ex)
            {
                throw new IOException("DNS server timeout (details: " + ex.ToString());
            }
        }

        public MXRecord? MakeSingleMXResponse(string dnsServerAddress)
        {

            MXRecord mxRecord;



            int qCount = ((data[4] & 0xFF) << 8) | (data[5] & 0xFF);
            if (qCount < 0)
            {
                throw new IOException("invalid question count");
            }

            int aCount = ((data[6] & 0xFF) << 8) | (data[7] & 0xFF);
            if (aCount < 0)
            {
                throw new IOException("invalid answer count");
            }

            position = 12;

            for (int i = 0; i < qCount; ++i)
            {
                name = "";
                position = proc(position);
                position += 4;
            }

            for (int i = 0; i < aCount; ++i)
            {

                name = "";
                position = proc(position);

                position += 10;

                int pref = (data[position++] << 8) | (data[position++] & 0xFF);

                name = "";
                position = proc(position);

                mxRecord = new MXRecord();

                mxRecord.preference = pref;
                mxRecord.mailServer = name;
                mxRecord.dnsServer = dnsServerAddress;

                if (mxRecord.mailServer == "localhost")
                {
                    continue;
                }

                return mxRecord;

            }

            return null;
        }



        //for unpacking the byte array
        public List<MXRecord> MakeResponse(string dnsServerAddress)
        {

            List<MXRecord> mxRecords = new();
            MXRecord mxRecord;

            int qCount = ((data[4] & 0xFF) << 8) | (data[5] & 0xFF);
            if (qCount < 0)
            {
                throw new IOException("invalid question count");
            }

            int aCount = ((data[6] & 0xFF) << 8) | (data[7] & 0xFF);
            if (aCount < 0)
            {
                throw new IOException("invalid answer count");
            }

            position = 12;

            for (int i = 0; i < qCount; ++i)
            {
                name = "";
                position = proc(position);
                position += 4;
            }

            for (int i = 0; i < aCount; ++i)
            {

                name = "";
                position = proc(position);

                position += 10;

                int pref = (data[position++] << 8) | (data[position++] & 0xFF);

                name = "";
                position = proc(position);

                mxRecord = new MXRecord();

                mxRecord.preference = pref;
                mxRecord.mailServer = name;
                mxRecord.dnsServer = dnsServerAddress;

                if (mxRecord.mailServer == "localhost")
                {
                    continue;
                }

                mxRecords.Add(mxRecord);

            }

            return mxRecords;
        }

        private int proc(int position)
        {

            int len = (data[position++] & 0xFF);

            if (len == 0)
            {
                return position;
            }

            int offset;

            do
            {
                if ((len & 0xC0) == 0xC0)
                {
                    if (position >= length)
                    {
                        return -1;
                    }
                    offset = ((len & 0x3F) << 8) | (data[position++] & 0xFF);
                    proc(offset);
                    return position;
                }
                else
                {
                    if ((position + len) > length)
                    {
                        return -1;
                    }
                    name += Encoding.ASCII.GetString(data[position..(position+len)]);
                    position += len;
                }

                if (position > length)
                {
                    return -1;
                }

                len = data[position++] & 0xFF;

                if (len != 0)
                {
                    name += ".";
                }
            } while (len != 0);

            return position;
        }
    }
}