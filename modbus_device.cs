using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using SDKTemplate;

using System.IO;
using System.Collections.ObjectModel;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using CustomSerialDeviceAccess;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;

namespace Modbus_Device_NS
{
    
    public enum regtype_t
    {
        reg_byte_0a,
        reg_byte_a0,
        reg_int16_ab,
        reg_int16_ba,
        reg_int32_abcd,
        reg_int32_cdab,
        reg_float_abcd,
        reg_float_cdab,
    }

    public enum regstatus_t
    {
        wait = 0,
        error,
        read_ok,
        send,
        time_out,
        not_connect
    }

    public enum regerror_t
    {
        error_no = 0,
        error_function,
        error_reg_address,
        error_value,
        error_crc,
        error_timeout,      
        error_other,
        error_dev_address
    }

    public class RequestSpan_t
    {
        public uint start_reg;
        public uint count_reg;
        public regerror_t span_error;
        public byte[] request ;
    }

    public class RegModbus_t
    {
        readonly public string reg_name;
        readonly public uint reg_number;
        readonly public regtype_t? reg_type;
        public int value;
        public string st_value;
        public regerror_t error_reg;
         
        public RegModbus_t(string Name, uint Number, regtype_t? type)
        {
            reg_name = Name;
            reg_number = Number;
            reg_type = type;           
        }
    }

    public class ModbusDeviceRequestData : EventArgs
    {
        public byte[] buf = null ;
        public int? span_index = null;
        public Modbus_Device MB_Dev;
        public regstatus_t req_status = regstatus_t.wait;
        public object UserData;

        /*   public ModbusDeviceEventArgs(byte[] data,int? index, Modbus_Device Device)
           {
               buf = data;
               span_index = index;
               MB_Dev = Device;
           }
        */
    }

    public class Modbus_Device 
    {
        private const int MAX_SPACE_SPAN = 5;
        private string name;
        private regerror_t device_error = regerror_t.error_no;
        private byte modbus_addr=0;
        private int max_frame_size;

        public regstatus_t? status;

        public List<RegModbus_t> listOfRegModbus;
        private List<RequestSpan_t> listOfRequestSpan;

        private static Object singletonCreationLock = new Object();
       
        public byte Modbus_Address
        {
            get
            {
                return modbus_addr;
            }
            set
            {
                lock (singletonCreationLock)
                {
                    modbus_addr = value;
                    CreateAllReadRequest_03();
                }
            }
        }

        public string Device_Name
        {
            get
            {
                return name;
            }
        }

        public int Request_Count
        {
            get
            {
                if (listOfRequestSpan != null)
                {
                    return listOfRequestSpan.Count;
                }
                return 0;
            }
        }


        async public Task Modbus_Device_Config(string config)
        {
            Windows.Storage.StorageFolder storageFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            Windows.Storage.StorageFile file = await storageFolder.GetFileAsync(config);         //("test.txt");
            string text = await Windows.Storage.FileIO.ReadTextAsync(file);

            Debug.WriteLine(Windows.Storage.ApplicationData.Current.LocalFolder.Path);
            Debug.WriteLine(text);

            string[] subs = Regex.Split(text, "\r\n|\r|\n");            
            string[] st_reg_data; 
            string st_line;

            bool reg_start_flag = false;

            listOfRegModbus = new List<RegModbus_t>();

            foreach (var sub in subs)
            {
                int offset = sub.TakeWhile(c => char.IsWhiteSpace(c)).Count();
                st_line = sub.Remove(0, offset);
 
               // Debug.WriteLine($"Substring: {st_line}");
              
                if (st_line.Length<=1)
                {
                    continue;
                }
                else if (st_line.StartsWith("device"))
                {
                    st_line = st_line.Remove(0, "device".Length);
                    st_reg_data = find_words_in_string(1, st_line);
                    if (st_reg_data != null)
                    {               
                       // Debug.WriteLine("device " + st_reg_data[0]);
                        name =st_reg_data[0];
                    }
                }
                else if (st_line.StartsWith("default modbus address"))
                {
                    st_line = st_line.Remove(0, "default modbus address".Length);
                    st_reg_data = find_words_in_string(1, st_line);
                    if (st_reg_data != null)
                    {                      
                        //Debug.WriteLine("default modbus address " + st_reg_data[0]);
                        if (modbus_addr == 0)
                        {
                            modbus_addr = Convert.ToByte(st_reg_data[0]);   
                        }
                    }
                }
                else if (st_line.StartsWith("max frame size (byte)"))
                {
                    st_line = st_line.Remove(0, "max frame size (byte)".Length);
                    st_reg_data = find_words_in_string(1, st_line);
                    if (st_reg_data != null)
                    {
                        //Debug.WriteLine("default modbus address " + st_reg_data[0]);
                        if (max_frame_size == 0)
                        {
                            max_frame_size = Convert.ToInt32(st_reg_data[0]);
                        }
                    }
                }                
                else if (!char.IsLetter(st_line[0]))
                {                  
                   // Debug.WriteLine("(st_line.StartsWith( = )");
                }
                else if (st_line.StartsWith("modbus register"))
                {
                    reg_start_flag = true;
                }
                else if (reg_start_flag == true)
                {
                    st_reg_data = find_words_in_string(3, st_line);
                    if (st_reg_data != null)
                    {
                        Debug.WriteLine("First {0} second {1} third {2}", st_reg_data[0], st_reg_data[1], st_reg_data[2]);
                        RegModbus_t reg_item = new RegModbus_t(st_reg_data[0], uint.Parse(st_reg_data[1]), check_reg_type(st_reg_data[2]));
                        //reg_item.error_reg = regerror_t.error_no;
                        listOfRegModbus.Add(reg_item);
                    }                   
                }
                else
                {
                    continue;
                }
            }
            create_request_span();
            CreateAllReadRequest_03();
        }

        private string[]  find_words_in_string(int word_number, string st)
        {
            int index = 0;
            var regex_pattern = new Regex(@"\b\w*\b");
            Match m;
            string[] st_buf = new string[word_number];
            index = 0;;
            for (int i = 0; i < word_number; i++)
            {
                if (st.Length > index)
                {
                    m = regex_pattern.Match(st, index);
                    if (m.Success && (st_buf.Count() > i))
                    {
                        st_buf[i] = m.Value;
                       // Debug.WriteLine("Found '{0}' at position {1}.", st_buf[i], m.Index);
                        index = m.Index + st_buf[i].Length + 1;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
            return st_buf;
        }

        public regtype_t? check_reg_type(string st)
        {
            foreach (regtype_t regtype_t in (regtype_t[])Enum.GetValues(typeof(regtype_t)))
            {
                if (String.Equals(st, regtype_t.ToString()))
                {
                    return regtype_t;
                }
            }
            return regtype_t.reg_int16_ab;
        }

        public uint check_reg_count(regtype_t? reg_type)
        {

            if ( reg_type >= regtype_t.reg_int32_abcd )
            {
                return 2;   //add 2 reg
            }
            return 1;       //add 1 reg
        }

        public void create_request_span()
        {
            uint delta_item, delta_max, current_reg;
            
            //RequestSpan_t span_item = new RequestSpan_t;
            listOfRegModbus.Sort((RegModbus_t x, RegModbus_t y) =>
            {
              return x.reg_number.CompareTo(y.reg_number);
            });

            listOfRequestSpan = new List<RequestSpan_t>();
            RequestSpan_t span_item = new RequestSpan_t();
            span_item.start_reg = listOfRegModbus[0].reg_number;
            span_item.count_reg = check_reg_count(listOfRegModbus[0].reg_type);
            current_reg = listOfRegModbus[0].reg_number;

            foreach (RegModbus_t reg_item  in listOfRegModbus)
            {
                delta_item = reg_item.reg_number - current_reg;
                delta_max = reg_item.reg_number - span_item.start_reg;
                if (delta_max > max_frame_size || delta_item > MAX_SPACE_SPAN)
                {
                    listOfRequestSpan.Add(span_item);
                    span_item = new RequestSpan_t();
                    span_item.start_reg = reg_item.reg_number;
                    span_item.count_reg = check_reg_count(reg_item.reg_type);
                    current_reg = span_item.start_reg;
                }
                else
                {
                    current_reg = reg_item.reg_number;
                    span_item.count_reg = delta_max + check_reg_count(reg_item.reg_type);
                }              
            }
            listOfRequestSpan.Add(span_item);
        }

        public void CreateAllReadRequest_03()
        {
            RequestSpan_t span_item = new RequestSpan_t();
            lock (singletonCreationLock)
            {
                for (int i= 0; i < listOfRequestSpan.Count; i++)
                {
                    span_item = listOfRequestSpan[i];
                    span_item.request = new byte[8];
                    span_item.request[0] = modbus_addr;        //address
                    span_item.request[1] = 3;                 //function
                    span_item.request[2] = (byte)((listOfRequestSpan[i].start_reg >> 8) & 0xFF);         //most-significant byte to register
                    span_item.request[3] = (byte)(listOfRequestSpan[i].start_reg & 0xFF);              // least - significant byte to register
                    span_item.request[4] = (byte)((listOfRequestSpan[i].count_reg >> 8) & 0xFF);         //most-significant byte to register
                    span_item.request[5] = (byte)(listOfRequestSpan[i].count_reg & 0xFF);              // least - significant byte to register

                    UInt16 res_CRC = CRC_Calc(span_item.request, 6);
                    span_item.request[6] = (byte)(res_CRC & 0xFF);
                    span_item.request[7] = (byte)((res_CRC >> 8) & 0xFF);                     
                    listOfRequestSpan[i] = span_item;
                }
            }
        }

        public ModbusDeviceRequestData GetReadRequest_03_Span(int index)
        {
            if (listOfRequestSpan == null || listOfRegModbus == null || listOfRequestSpan.Count <= index)
            {
                return null;
            }
            ModbusDeviceRequestData Req_Data = new ModbusDeviceRequestData();
            Req_Data.MB_Dev = this;
            Req_Data.buf = listOfRequestSpan[index].request;
            Req_Data.span_index = index;
            return Req_Data;
        }

        public regerror_t ParseRequest(ModbusDeviceRequestData Arg)
        {
            regerror_t error = (InvalidFrame(Arg));
            if (error == regerror_t.error_no)
            {
                error = FrameParse(Arg);
            }
            device_error = error;
            if (Arg.span_index != null)
            {
                listOfRequestSpan[(int)Arg.span_index].span_error = error;
            }           
            return error;
        }

        public regerror_t InvalidFrame(ModbusDeviceRequestData Arg)
        {
            if (Arg.req_status == regstatus_t.time_out || Arg.req_status == regstatus_t.error)
            {
                return regerror_t.error_timeout;
            }
            if (Arg.buf == null || Arg.buf?.Length<5)
            {
                return regerror_t.error_other;
            }
            if (modbus_addr!= Arg.buf[0])
            {
                return regerror_t.error_dev_address;
            }
            int Len_Buf = Arg.buf.Length;
            UInt16 CRC = CRC_Calc(Arg.buf, Len_Buf - 2);
            UInt16 CRC_Buf = (UInt16)(Arg.buf[Len_Buf - 2] | (Arg.buf[Len_Buf - 1] << 8));
            if (CRC != CRC_Buf)
            {
                return regerror_t.error_crc;            
            }
            if (listOfRequestSpan == null || listOfRegModbus == null)
            {
                return regerror_t.error_other;
            }
            if(Arg.span_index!= null)
            {
                if (listOfRequestSpan.Count <= Arg.span_index)
                {
                    return regerror_t.error_other;
                }
            }
            return regerror_t.error_no;
        }

        private regerror_t FrameParse(ModbusDeviceRequestData Arg)
        {
            int len_reg, find_index;

            switch (Arg.buf[1])
            {
                case 03:
                    if (Arg.span_index != null)
                    {
                        len_reg = Arg.buf[2] / 2;
                        if (len_reg != listOfRequestSpan[(int)Arg.span_index].count_reg)
                        {
                            return regerror_t.error_reg_address;
                        }
                        for (int i = 0; i < (len_reg); i++)
                        {
                            find_index = listOfRegModbus.FindIndex(x => x.reg_number == (listOfRequestSpan[(int)Arg.span_index].start_reg + i));
                            if (find_index >= 0)
                            {
                                if (check_reg_count(listOfRegModbus[find_index].reg_type) == 1)
                                {
                                    listOfRegModbus[find_index].value = ((Arg.buf[3 + i * 2] << 8) | Arg.buf[4 + i * 2]) & 0xFFFF;
                                }
                                else if (check_reg_count(listOfRegModbus[find_index].reg_type) == 2)
                                {
                                    listOfRegModbus[find_index].value = (Arg.buf[5 + i * 2] << 24) | (Arg.buf[6 + i * 2] << 16) | (Arg.buf[3 + i * 2] << 8) | Arg.buf[4 + i * 2];
                                }
                                listOfRegModbus[find_index].st_value = ValueToStringByType(listOfRegModbus[find_index].reg_type, listOfRegModbus[find_index].value);
                            }
                            else
                            {
                                return regerror_t.error_reg_address;
                            }
                        }
                        return regerror_t.error_no;
                    }
                    break;
                    
                case 06:
                    return regerror_t.error_no;
                case 16:
                    return regerror_t.error_no;
                case 0x81:
                    return regerror_t.error_function;
                case 0x82:
                    return regerror_t.error_reg_address;
                case 0x83:
                    return regerror_t.error_value;
                default:
                    break;
            }
            return regerror_t.error_other;
        }

        public ModbusDeviceRequestData GetWriteRequest_16Auto(int reg_addr, string value)
        {
            int find_index = listOfRegModbus.FindIndex(x => x.reg_number == reg_addr);
            if (find_index < 0)
            {
                return null;
            }
            regtype_t? regtype = listOfRegModbus[find_index].reg_type;

            byte[] val_buf = ValueToArrayByType(regtype, value);
            byte[] buf = new byte[9 + val_buf.Length];

            buf[0] = modbus_addr;                           //address
            buf[1] = 16;                                    //function
            buf[2] = (byte)((reg_addr >> 8) & 0xFF);        //most-significant byte to register
            buf[3] = (byte)(reg_addr & 0xFF);               // least - significant byte to register
            buf[4] = 0;                                     //most-significant byte to register
            buf[5] = (byte) (check_reg_count(regtype));     // least - significant byte to register
            buf[6] = (byte)(val_buf.Length);

            Array.Copy(val_buf, 0, buf, 7, val_buf.Length);

            UInt16 res_CRC = CRC_Calc(buf, (buf.Length-2));
            buf[buf.Length - 2] = (byte)(res_CRC & 0xFF);
            buf[buf.Length - 1] = (byte)((res_CRC >> 8) & 0xFF);
            ModbusDeviceRequestData Req_Data = new ModbusDeviceRequestData();
            Req_Data.MB_Dev = this;
            Req_Data.buf = buf;
            return Req_Data;
        }

        private byte[] ValueToArrayByType(regtype_t? type, string value)
        {
            byte[] buf = new byte[check_reg_count(type) * 2];
            switch (type)
            {
                case regtype_t.reg_int16_ab:
                    Int16 value_16 = Convert.ToInt16(value);
                    buf[0] = (byte)((value_16 >> 8) & 0xFF);
                    buf[1] = (byte)(value_16 & 0xFF);
                    break;
                case regtype_t.reg_int32_abcd:
                    Int32 value_32 = (Convert.ToInt32(value));
                    buf[0] = (byte)((value_32 >> 24) & 0xFF);
                    buf[1] = (byte)((value_32 >> 16) & 0xFF);
                    buf[3] = (byte)((value_32 >> 8) & 0xFF);
                    buf[4] = (byte)(value_32 & 0xFF);
                    break;
                case regtype_t.reg_float_abcd:
                    float value_float = (Convert.ToSingle(value));
                    buf = BitConverter.GetBytes(value_float);
                    break;
                default:
                    return null;
            }
            return buf;
        }

        private string ValueToStringByType (regtype_t? type, int value)
        {           
            string st_value;
            switch (type)
            {
                case regtype_t.reg_int16_ab:      
                    st_value = value.ToString();
                    break;
                case regtype_t.reg_int16_ba:
                    uint value_ba = (((uint)value & 0xFF00FF00) >> 8) | (((uint)value & 0x00FF00FF) << 8) ;
                    st_value = value_ba.ToString();
                    break;
                case regtype_t.reg_int32_abcd:
                    st_value = value.ToString();
                    break;
                case regtype_t.reg_float_abcd:
                    byte[] buf = BitConverter.GetBytes(value);
                    float value_float  = BitConverter.ToSingle(buf, 0); 
                    st_value = value_float.ToString();
                    break;
                default:
                    return "not find";
            }
            return st_value;
        }

        public int? GetValueByRegAddr(int reg_addr)
        {
            int find_index = listOfRegModbus.FindIndex(x => x.reg_number == reg_addr);
            if (find_index < 0)
            {
                return null;
            }
            return listOfRegModbus[find_index].value;
        }

        public int? GetRegAddrByName(string st_name)
        {
            int find_index = listOfRegModbus.FindIndex(x => x.reg_name == st_name);
            if (find_index < 0)
            {
                return null;
            }          
            return find_index;
        }

        public string GetStringByRegAddr_Type(int reg_addr, regtype_t? type)
        {
            int find_index = listOfRegModbus.FindIndex(x => x.reg_number == reg_addr);
            if (find_index < 0)
            {
                return "not found";
            }
            return ValueToStringByType(listOfRegModbus[find_index].reg_type, listOfRegModbus[find_index].value);
        }

        public string GetStValueByName(string st_name)
        {
            int find_index = listOfRegModbus.FindIndex(x => x.reg_name == st_name);
            if (find_index < 0)
            {
                return "not found";
            }
            return listOfRegModbus[find_index].st_value;
        }

        public string GetStValueByNumber(int reg_addr)
        {
            int find_index = listOfRegModbus.FindIndex(x => x.reg_number == reg_addr);
            if (find_index < 0)
            {
                return "not found";
            }
            return listOfRegModbus[find_index].st_value;
        }

        public byte[] GetWriteRequest_06(int reg_addr, int value_16)
        {
            int find_index = listOfRegModbus.FindIndex(x => x.reg_number == reg_addr);
            if (find_index < 0)
            {
                return null;
            }
            byte[] buf = new byte[8];
            buf[0] = modbus_addr;                           //address
            buf[1] = 6;                                     //function
            buf[2] = (byte)((reg_addr >> 8) & 0xFF);        //most-significant byte to register
            buf[3] = (byte)(reg_addr & 0xFF);               // least - significant byte to register
            buf[4] = (byte)((value_16 >> 8) & 0xFF);        //most-significant byte to register
            buf[5] = (byte)(value_16 & 0xFF);               // least - significant byte to register

            UInt16 res_CRC = CRC_Calc(buf, 6);
            buf[6] = (byte)(res_CRC & 0xFF);
            buf[7] = (byte)((res_CRC >> 8) & 0xFF);           
            return buf;
        }

        UInt16 CRC_Calc(byte[] Buf, int len)
       {     
            UInt16 res_CRC = 0xffff;
            int count = 0;
            int count_crc;
            byte dt;
            while (count < len)
            {
                count_crc = 0;
                dt = (byte)(Buf[count] & 0xff);
                res_CRC ^= (UInt16)(dt & 0xff);
                while (count_crc < 8)
                {
                    if ((res_CRC & 0x0001) < 1)
                    {
                        res_CRC = (UInt16)((res_CRC >> 1) & 0x7fff);
                    }
                    else
                    {
                        res_CRC = (UInt16)((res_CRC >> 1) & 0x7fff);
                        res_CRC ^= 0xa001;
                    }
                    count_crc++;
                }
                count++;
            }
            return res_CRC;
       }
    }
}
