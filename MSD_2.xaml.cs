using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Diagnostics;
using Modbus_Device_NS;
using Request_Dispatcher_NS;
using Modbus_Device_NS;
// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace CustomSerialDeviceAccess
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    ///

    
    public sealed partial class MSD_2 : Page
    {
        Modbus_Device MSD_2_Dev;//= new Modbus_Device();

        //Modbus_Device MSD_2_Device = new Modbus_Device();
        Request_Dispatcher dis;
        public MSD_2()
        {
            this.InitializeComponent();
            SerialFrame.Navigate(typeof(Scenario1_ConnectDisconnect));
            //DataFrame.Navigate(typeof(Scenario1_ConnectDisconnect));
            //Debug.WriteLine("EventHandlerForDevice.Current.ReadEnd = ReadEnd1");


        }

        protected override void OnNavigatedTo(NavigationEventArgs eventArgs)
        {
            create(null, null);
            
        }


        protected override void OnNavigatedFrom(NavigationEventArgs eventArgs)
        {
    
            if (dis != null)
            {
                dis.StopReadData(null,null);
            }
            
            dis = null;
            MSD_2_Dev = null;
        }


       async  private void create(Object sender, RoutedEventArgs eventArgs)
        {
            dis = new Request_Dispatcher();

            dis.WriteEvent = EventHandlerForDevice.Current.WriteDataToCom;
            EventHandlerForDevice.Current.ReadCallback_Handler = dis.ReadHandler;

            EventHandlerForDevice.Current.ConnectEventHandler_Set = dis.ReadData;// this.connect;
            EventHandlerForDevice.Current.DisconnectEventHandler_Set = dis.StopReadData;  //this.disconnect;

            MSD_2_Dev = await dis.AddModbusDev("test.txt");
           

           // send_request(null, null);
        }
        private void  send_request(Object sender, RoutedEventArgs eventArgs)
        {
            if (dis != null)
            {
             //   dis.ReadData();
            }
        }
        private void connect(Object sender, Object eventArgs)
        {
            if (dis != null)
            {
                dis.ReadData(null,null);

            }
        }
        private void disconnect(Object sender, Object eventArgs)
        {
            stop_send_request(null, null);
        }


        private void stop_send_request(Object sender, RoutedEventArgs eventArgs)
        {
            if (dis != null)
            {
                dis.StopReadData(null,null);

            }
        }

        private void send_write_request(Object sender, RoutedEventArgs eventArgs)
        {
            if (dis != null)
            {
                dis.AddToWriteQueue(MSD_2_Dev, 29, "123");
               // dis.WriteDataToDev(MSD_2_Dev, 29, "123");
              //  ReadBytesTextBlock.Text += MSD_2_Dev.GetValueByRegAddr(0) + '\n';
              //  ReadBytesTextBlock.Text += MSD_2_Dev.GetValueByRegAddr(1) + '\n';
              //  ReadBytesTextBlock.Text += MSD_2_Dev.GetValueByRegAddr(120) + '\n';
               // ReadBytesTextBlock.Text += MSD_2_Dev.GetStValueByNumber(56) + '\n';
                ReadBytesTextBlock.Text += MSD_2_Dev.GetStValueByName("T1") + '\n';
               // ReadBytesTextBlock.Text += MSD_2_Dev.GetStValueByName("T11") + '\n';
            }
        }
    }
}
