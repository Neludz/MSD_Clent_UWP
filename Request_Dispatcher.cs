using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Modbus_Device_NS;
using CustomSerialDeviceAccess;
using System.Diagnostics;
using System.Collections;
using System.Threading;
using System.ComponentModel;

namespace Request_Dispatcher_NS
{

    
    public class Request_Dispatcher
    {
        const int MAX_SIZE_WRITE_QUEUE = 10;
        Task Request_Task;
        private static CancellationTokenSource RequestCancellationTokenSource;

        // The EventWaitHandle used to demonstrate the difference
        // between AutoReset and ManualReset synchronization events.
        //***************************************************************************************************************************************
        private static EventWaitHandle ewh;

        private static Mutex mut = new Mutex();

        private  List<Modbus_Device> listOfModbus_Device;

        public EventHandler<ModbusDeviceRequestData> WriteEvent;
        Queue WriteQueue = new Queue();


        public  List<Modbus_Device> Modbus_Dev_List
        {
            get
            {
                if (listOfModbus_Device == null)
                {
                    if (listOfModbus_Device == null)
                    {
                         CreateNewList();
                    }                
                }
                return listOfModbus_Device;
            }
        }

        public int AddToWriteQueue (Modbus_Device dev, int reg_addr, string st_value)
        {
            if (WriteQueue.Count < MAX_SIZE_WRITE_QUEUE)
            {
                WriteQueue.Enqueue(dev.GetWriteRequest_16Auto(reg_addr, st_value));
                return 0;
            }
            return 1;
        }
       public AsyncOperation _asyncOperation;
        public async Task ReqDispather(CancellationToken cancellationToken)
        {
            _asyncOperation = AsyncOperationManager.CreateOperation(null);
            while (true)
            {                             
                ModbusDeviceRequestData Req_Data = new ModbusDeviceRequestData();

                if (listOfModbus_Device != null)
                {
                    foreach (Modbus_Device MB_Dev in listOfModbus_Device)
                    {
                        for (int i = 0; i < MB_Dev.Request_Count; i++)
                        {
                            await Task.Delay(500);
                            if (cancellationToken.IsCancellationRequested)  // проверяем наличие сигнала отмены задачи
                            {
                                Debug.WriteLine("Task chancel");
                                return;     //  выходим из метода и тем самым завершаем задачу
                            }
                            mut.WaitOne();
                            
                               Req_Data = MB_Dev.GetReadRequest_03_Span(i);
                                WriteEvent(this, Req_Data);
                        }
                    }
                }
            }

        }       

        public Modbus_Device GetModbusDeviceByName(string name)
        {
            return null;
        }
        private  void CreateNewList()
        {
            listOfModbus_Device = new List<Modbus_Device>();
        }

       public   void ReadData(Object sender, Object eventArgs)
        {
           // EventHandler<byte[]> handler = write_event;
            if (WriteEvent != null &&  Request_Task == null)
            {
              //  await Task.Delay(500);
                RequestCancellationTokenSource = new CancellationTokenSource();
              Request_Task = ReqDispather(RequestCancellationTokenSource.Token);
               // await Request_Task;
          //      WriteEvent(this, listOfModbus_Device[0].GetReadRequest_03_Span(0));
          //     Debug.WriteLine("000000000000000 = " + BitConverter.ToString(listOfModbus_Device[0].GetReadRequest_03_Span(0).buf));
          //    await Task.Delay(200);
          //     WriteEvent(this, listOfModbus_Device[0].GetReadRequest_03_Span(1));
          //     Debug.WriteLine("111111111111111 = " + BitConverter.ToString(listOfModbus_Device[0].GetReadRequest_03_Span(1).buf));

            }
        }

        public void StopReadData(Object sender, Object eventArgs)
        {
            if (Request_Task != null)
            {
                RequestCancellationTokenSource.Cancel();
                RequestCancellationTokenSource.Dispose();

                // _asyncOperation.PostOperationCompleted(null, null);
                Request_Task = null;
            }
        }


        public int WriteDataToDev(Modbus_Device dev, int reg_addr, string st_value)
        {
            // EventHandler<byte[]> handler = write_event;
            if (WriteEvent != null)
            {
                WriteEvent(this, dev.GetWriteRequest_16Auto(reg_addr, st_value));
                return 1;
            }
            return 0;
        }

        public void ReadHandler(object sender, ModbusDeviceRequestData Arg)
        {
            if (Arg != null)
            {
                Modbus_Device MB_Dev = (Modbus_Device)Arg.MB_Dev;
                MB_Dev.ParseRequest(Arg);
            }

           _asyncOperation.Post(ReleaseResource, null);
            if (Request_Task == null)
            {
                _asyncOperation.OperationCompleted();
            }
        }



        private void ReleaseResource(object state)
        {
            mut.ReleaseMutex();
        }

       async public Task <Modbus_Device> AddModbusDev (string config_name)
        {
            Modbus_Device dev = new Modbus_Device();
            await dev.Modbus_Device_Config(config_name);
            Modbus_Dev_List.Add(dev);
            return dev;
        }
    }
}
