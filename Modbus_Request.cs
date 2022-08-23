using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Modbus_Request_NS
{
    public class Modbus_Request
    {
        private static Modbus_Request modbus_request;
        private static Object singletonCreationLock = new Object();

        public static Modbus_Request Current
        {
            get
            {
                if (modbus_request == null)
                {
                    lock (singletonCreationLock)
                    {
                        if (modbus_request == null)
                        {
                            CreateNewModbus_Request();
                        }
                    }
                }
                return modbus_request;
            }
        }

        public static void CreateNewModbus_Request()
        {
            modbus_request = new Modbus_Request();
        }


        public byte[] mb_request_03 (uint addr, uint start_reg, uint count_reg)
        {
            byte[] buf = new byte[10];

            return buf;
        }









    }
}
