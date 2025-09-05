using System;
namespace gv.ampp.smpl.util {
    public class OxtelEncoderDecoder
    {
        public static char STX0 = 0x002;
        public static char STX1 = 0x003;
        public static char ACK0 = 0x004;
        public static char ACK1 = 0x005;
        public static char NAC = 0x007;
        static char stx = STX0;
        const static int[] lstab = new int[]
        {
        0x0000, 0xc0c1, 0xc181, 0x0140, 0xc301, 0x03c0,
        0x0280, 0xc241, 0xc601, 0x06c0, 0x0780, 0xc741,
        0x0500, 0xc5c1, 0xc481, 0x0440
        };
        const static int[] mstab = new int[]
        {
        0x0000, 0xcc01, 0xd801, 0x1400, 0xf001, 0x3c00,
        0x2800, 0xe401, 0xa001, 0x6c00, 0x7800, 0xb401,
        0x5000, 0x9c01, 0x8801, 0x4400
        };
        public static void do_crc(byte ch, ref int crcval)
        {
            int tmp;
            tmp = crcval ^ ch;
            crcval = mstab[(tmp >> 4) & 0xf] ^ lstab[tmp & 0xf]
            ^ (crcval >> 8);
        }

        public static byte[] getEncodedMessage(byte[] messagePtr)
        {
            int rem_crc = 0, i;
            byte ch;
            int len = messagePtr.Length;
            if (len == 0)
                return null;
            byte[] outMsg = new byte[len + 4];

            outMsg[0] = stx;
            for (i = 1; i < len; i++)
            {
                outMsg[i] = ch;
                do_crc(ch, &rem_crc);
            }
            *outMsg++ = ':';
            do_crc(':', &rem_crc);
            outMsg[i++] = (rem_crc & 0xff);
            outMsg[i++] = (rem_crc >> 8);
            if (stx == STX0)
                stx = STX1;
            else
                stx = STX0;
            return outMsg;
        }

        /*// Send a single command to an Imagestore, using printf style formatting.
        void remote_send(char* format,...)
        {
            UINT16 rem_crc = 0;
            INT ch;
            char message[128];
            char* messageptr = (char*)message;
            va_list argptr;
            va_start(argptr, format);
            vsprintf(message, format, argptr);
            va_end(argptr);
            rem_send_char(stx);
            while ((ch = *messageptr++) != 0)
            {
                rem_send_char(ch);
                do_crc(ch, &rem_crc);
            }
            rem_send_char(':');
            do_crc(':', &rem_crc);
            rem_send_char(rem_crc & 0xff);
            rem_send_char(rem_crc >> 8);
            if (stx == STX0)
                stx = STX1;
            else
                stx = STX0;
        }*/
    }
}