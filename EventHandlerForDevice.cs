//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;

using Windows.ApplicationModel;
using Windows.Foundation;

using Windows.UI.Core;
using Windows.UI.Xaml;

using SDKTemplate;
using System.Diagnostics;


using System.Threading;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.Storage.Streams;
using CustomSerialDeviceAccess;
using Modbus_Device_NS;

namespace CustomSerialDeviceAccess
{
    /// <summary>
    /// The purpose of this class is to demonstrate the expected application behavior for app events 
    /// such as suspension and resume or when the device is disconnected. In addition to handling
    /// the SerialDevice, the app's state should also be saved upon app suspension (will not be demonstrated here).
    /// 
    /// This class will also demonstrate how to handle device watcher events.
    /// 
    /// For simplicity, this class will only allow at most one device to be connected at any given time. In order
    /// to make this class support multiple devices, make this class a non-singleton and create multiple instances
    /// of this class; each instance should watch one connected device.
    /// </summary>
    public class EventHandlerForDevice : IDisposable
    {
        /// <summary>
        /// Allows for singleton EventHandlerForDevice
        /// </summary>
        private static EventHandlerForDevice eventHandlerForDevice;

        /// <summary>
        /// Used to synchronize threads to avoid multiple instantiations of eventHandlerForDevice.
        /// </summary>
        private static Object singletonCreationLock = new Object();

        private String deviceSelector;
        private DeviceWatcher deviceWatcher;

        private DeviceInformation deviceInformation;
        private DeviceAccessInformation deviceAccessInformation;
        private SerialDevice device;

        private SuspendingEventHandler appSuspendEventHandler;
        private EventHandler<Object> appResumeEventHandler;

        private TypedEventHandler<EventHandlerForDevice, DeviceInformation> deviceCloseCallback;
        private TypedEventHandler<EventHandlerForDevice, DeviceInformation> deviceConnectedCallback;

     


        private TypedEventHandler<DeviceWatcher, DeviceInformation> deviceAddedEventHandler;
        private TypedEventHandler<DeviceWatcher, DeviceInformationUpdate> deviceRemovedEventHandler;
        private TypedEventHandler<DeviceAccessInformation, DeviceAccessChangedEventArgs> deviceAccessEventHandler;

        private Boolean watcherSuspended;
        private Boolean watcherStarted;
        private Boolean isEnabledAutoReconnect;
        private int serialtimeout_ms = 100;

        // A pointer back to the main page.  This is needed if you want to call methods in MainPage such
        // as NotifyUser()
        private MainPage rootPage = MainPage.Current;

        //---------------------- request data -----------------------
        // Track Read Operation
        private static CancellationTokenSource ReadCancellationTokenSource;
        private Object ReadCancelLock = new Object();

        private Boolean IsReadTaskPending;
        private uint ReadBytesCounter = 0;
        DataReader DataReaderObject = null;

        // Track Write Operation
        private static CancellationTokenSource WriteCancellationTokenSource;
        private Object WriteCancelLock = new Object();

        private Boolean IsWriteTaskPending;
        private uint WriteBytesCounter = 0;
        DataWriter DataWriteObject = null;

        bool WriteBytesAvailable = false;

        // Indicate if we navigate away from this page or not.
        private Boolean IsNavigatedAway;

        private Timer stateTimer;


        private TypedEventHandler<object, ModbusDeviceRequestData> ReadCallback;
        private TypedEventHandler<object, object> ConnectEventHandler;
        private TypedEventHandler<object, object> DisconnectEventHandler;

        //------------------------- request data end -----------------------------------------




        /// <summary>
        /// Enforces the singleton pattern so that there is only one object handling app events
        /// as it relates to the SerialDevice because this sample app only supports communicating with one device at a time. 
        ///
        /// An instance of EventHandlerForDevice is globally available because the device needs to persist across scenario pages.
        ///
        /// If there is no instance of EventHandlerForDevice created before this property is called,
        /// an EventHandlerForDevice will be created.
        /// </summary>
        public static EventHandlerForDevice Current
        {
            get
            {
                if (eventHandlerForDevice == null)
                {
                    lock (singletonCreationLock)
                    {
                        if (eventHandlerForDevice == null)
                        {
                            CreateNewEventHandlerForDevice();
                           
                        }
                    }
                }

                return eventHandlerForDevice;
            }
        }

        /// <summary>
        /// Creates a new instance of EventHandlerForDevice, enables auto reconnect, and uses it as the Current instance.
        /// </summary>
        public static void CreateNewEventHandlerForDevice()
        {
            eventHandlerForDevice = new EventHandlerForDevice();
            //**********************************
            ResetReadCancellationTokenSource();
            ResetWriteCancellationTokenSource();
            //**********************************
        }

        public TypedEventHandler<EventHandlerForDevice, DeviceInformation> OnDeviceClose
        {
            get
            {
                return deviceCloseCallback;
            }
            set
            {
                deviceCloseCallback = value;
            }
        }

        public TypedEventHandler<EventHandlerForDevice, DeviceInformation> OnDeviceConnected
        {
            get
            {
                return deviceConnectedCallback;
            }
            set
            {
                deviceConnectedCallback = value;
            }
        }


        public TypedEventHandler<object, ModbusDeviceRequestData> ReadCallback_Handler
        {
            get
            {
                return ReadCallback;
            }

            set
            {
                ReadCallback = value;
            }
        }

        public TypedEventHandler<object, object> ConnectEventHandler_Set
        {
            get
            {
                return ConnectEventHandler;
            }
            set
            {
                ConnectEventHandler = value;
            }
        }
        public TypedEventHandler<object, object> DisconnectEventHandler_Set
        {
            get
            {
                return DisconnectEventHandler;
            }
            set
            {
                DisconnectEventHandler = value;
            }
        }

        /* public TypedEventHandler<EventHandlerForDevice, byte[]> WriteCallback_Handler
         {
             get
             {
                 return WriteCallback;
             }

             set
             {
                 WriteCallback = value;
             }
         }
        */


        public Boolean IsDeviceConnected
        {
            get
            {
                return (Current.device != null);
            }
        }

        public SerialDevice Device
        {
            get
            {
                return Current.device;
            }
        }

        public int SerialTimeout_MS
        {
            get
            {
                return serialtimeout_ms;
            }
            set
            {
                serialtimeout_ms = value;
            }


        }

        /// <summary>
        /// This DeviceInformation represents which device is connected or which device will be reconnected when
        /// the device is plugged in again (if IsEnabledAutoReconnect is true);.
        /// </summary>
        public DeviceInformation DeviceInformation
        {
            get
            {
                return deviceInformation;
            }
        }

        /// <summary>
        /// Returns DeviceAccessInformation for the device that is currently connected using this EventHandlerForDevice
        /// object.
        /// </summary>
        public DeviceAccessInformation DeviceAccessInformation
        {
            get
            {
                return deviceAccessInformation;
            }
        }

        /// <summary>
        /// DeviceSelector AQS used to find this device
        /// </summary>
        public String DeviceSelector
        {
            get
            {
                return deviceSelector;
            }
        }

        /// <summary>
        /// True if EventHandlerForDevice will attempt to reconnect to the device once it is plugged into the computer again
        /// </summary>
        public Boolean IsEnabledAutoReconnect
        {
            get
            {
                return isEnabledAutoReconnect;
            }
            set
            {
                isEnabledAutoReconnect = value;
            }
        }

        /// <summary>
        /// This method opens the device using the WinRT Serial API. After the device is opened, save the device
        /// so that it can be used across scenarios.
        ///
        /// It is important that the FromIdAsync call is made on the UI thread because the consent prompt can only be displayed
        /// on the UI thread.
        /// 
        /// This method is used to reopen the device after the device reconnects to the computer and when the app resumes.
        /// </summary>
        /// <param name="deviceInfo">Device information of the device to be opened</param>
        /// <param name="deviceSelector">The AQS used to find this device</param>
        /// <returns>True if the device was successfully opened, false if the device could not be opened for well known reasons.
        /// An exception may be thrown if the device could not be opened for extraordinary reasons.</returns>
        public async Task<Boolean> OpenDeviceAsync(DeviceInformation deviceInfo, String deviceSelector)
        {
            device = await SerialDevice.FromIdAsync(deviceInfo.Id);
            Boolean successfullyOpenedDevice = false;
            NotifyType notificationStatus;
            String notificationMessage = null;

            // Device could have been blocked by user or the device has already been opened by another app.
            if (device != null)
            {
                successfullyOpenedDevice = true;

                deviceInformation = deviceInfo;
                this.deviceSelector = deviceSelector;

                notificationStatus = NotifyType.StatusMessage;
                notificationMessage = "Device " + deviceInformation.Id + " opened";

                // Notify registered callback handle that the device has been opened
                if (deviceConnectedCallback != null)
                {
                    deviceConnectedCallback(this, deviceInformation);
                }

                if (appSuspendEventHandler == null || appResumeEventHandler == null)
                {
                    RegisterForAppEvents();
                }

                // Register for DeviceAccessInformation.AccessChanged event and react to any changes to the
                // user access after the device handle was opened.
                if (deviceAccessEventHandler == null)
                {
                    RegisterForDeviceAccessStatusChange();
                }

                // Create and register device watcher events for the device to be opened unless we're reopening the device
                if (deviceWatcher == null)
                {
                    deviceWatcher = DeviceInformation.CreateWatcher(deviceSelector);

                    RegisterForDeviceWatcherEvents();
                }

                if (!watcherStarted)
                {
                    // Start the device watcher after we made sure that the device is opened.
                    StartDeviceWatcher();
                }

                if (ConnectEventHandler != null && EventHandlerForDevice.Current.IsDeviceConnected)
                {
                    ConnectEventHandler(this, null);
                }

            }
            else
            {
                successfullyOpenedDevice = false;

                notificationStatus = NotifyType.ErrorMessage;

                var deviceAccessStatus = DeviceAccessInformation.CreateFromId(deviceInfo.Id).CurrentStatus;

                if (deviceAccessStatus == DeviceAccessStatus.DeniedByUser)
                {
                    notificationMessage = "Access to the device was blocked by the user : " + deviceInfo.Id;
                }
                else if (deviceAccessStatus == DeviceAccessStatus.DeniedBySystem)
                {
                    // This status is most likely caused by app permissions (did not declare the device in the app's package.appxmanifest)
                    // This status does not cover the case where the device is already opened by another app.
                    notificationMessage = "Access to the device was blocked by the system : " + deviceInfo.Id;
                }
                else
                {
                    // Most likely the device is opened by another app, but cannot be sure
                    notificationMessage = "Unknown error, possibly opened by another app : " + deviceInfo.Id;
                }
            }

            MainPage.Current.NotifyUser(notificationMessage, notificationStatus);

            return successfullyOpenedDevice;
        }

        /// <summary>
        /// Closes the device, stops the device watcher, stops listening for app events, and resets object state to before a device
        /// was ever connected.
        /// </summary>
        public void CloseDevice()
        {
            if (IsDeviceConnected)
            {
                CloseCurrentlyConnectedDevice();
            }

            if (deviceWatcher != null)
            {
                if (watcherStarted)
                {
                    StopDeviceWatcher();

                    UnregisterFromDeviceWatcherEvents();
                }

                deviceWatcher = null;
            }

            if (deviceAccessInformation != null)
            {
                UnregisterFromDeviceAccessStatusChange();

                deviceAccessInformation = null;
            }

            if (appSuspendEventHandler != null || appResumeEventHandler != null)
            {
                UnregisterFromAppEvents();
            }

            deviceInformation = null;
            deviceSelector = null;

            deviceConnectedCallback = null;
            deviceCloseCallback = null;

            isEnabledAutoReconnect = true;

            if (DisconnectEventHandler != null)
            {
                DisconnectEventHandler(null, null);
            }

        }

        private EventHandlerForDevice()
        {
            watcherStarted = false;
            watcherSuspended = false;
            isEnabledAutoReconnect = true;
        }

        /// <summary>
        /// This method demonstrates how to close the device properly using the WinRT Serial API.
        ///
        /// When the SerialDevice is closing, it will cancel all IO operations that are still pending (not complete).
        /// The close will not wait for any IO completion callbacks to be called, so the close call may complete before any of
        /// the IO completion callbacks are called.
        /// The pending IO operations will still call their respective completion callbacks with either a task 
        /// cancelled error or the operation completed.
        /// </summary>
        private async void CloseCurrentlyConnectedDevice()
        {
            if (device != null)
            {
                // Notify callback that we're about to close the device
                if (deviceCloseCallback != null)
                {
                    deviceCloseCallback(this, deviceInformation);
                }

                // This closes the handle to the device
                device.Dispose();

                device = null;
                //**********************************************************************************************************************************
                CancelAllIoTasks();
                //**********************************************************************************************************************************
                // Save the deviceInformation.Id in case deviceInformation is set to null when closing the
                // device
                String deviceId = deviceInformation.Id;

                await rootPage.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal,
                    new DispatchedHandler(() =>
                    {
                        MainPage.Current.NotifyUser(deviceId + " is closed", NotifyType.StatusMessage);
                    }));
            }
        }

        /// <summary>
        /// Register for app suspension/resume events. See the comments
        /// for the event handlers for more information on the exact device operation performed.
        ///
        /// We will also register for when the app exists so that we may close the device handle.
        /// </summary>
        private void RegisterForAppEvents()
        {
            appSuspendEventHandler = new SuspendingEventHandler(EventHandlerForDevice.Current.OnAppSuspension);
            appResumeEventHandler = new EventHandler<Object>(EventHandlerForDevice.Current.OnAppResume);

            // This event is raised when the app is exited and when the app is suspended
            App.Current.Suspending += appSuspendEventHandler;

            App.Current.Resuming += appResumeEventHandler;
        }

        private void UnregisterFromAppEvents()
        {
            // This event is raised when the app is exited and when the app is suspended
            App.Current.Suspending -= appSuspendEventHandler;
            appSuspendEventHandler = null;

            App.Current.Resuming -= appResumeEventHandler;
            appResumeEventHandler = null;
        }

        /// <summary>
        /// Register for Added and Removed events.
        /// Note that, when disconnecting the device, the device may be closed by the system before the OnDeviceRemoved callback is invoked.
        /// </summary>
        private void RegisterForDeviceWatcherEvents()
        {
            deviceAddedEventHandler = new TypedEventHandler<DeviceWatcher, DeviceInformation>(this.OnDeviceAdded);

            deviceRemovedEventHandler = new TypedEventHandler<DeviceWatcher, DeviceInformationUpdate>(this.OnDeviceRemoved);

            deviceWatcher.Added += deviceAddedEventHandler;

            deviceWatcher.Removed += deviceRemovedEventHandler;
        }

        private void UnregisterFromDeviceWatcherEvents()
        {
            deviceWatcher.Added -= deviceAddedEventHandler;
            deviceAddedEventHandler = null;

            deviceWatcher.Removed -= deviceRemovedEventHandler;
            deviceRemovedEventHandler = null;
        }

        /// <summary>
        /// Listen for any changed in device access permission. The user can block access to the device while the device is in use.
        /// If the user blocks access to the device while the device is opened, the device's handle will be closed automatically by
        /// the system; it is still a good idea to close the device explicitly so that resources are cleaned up.
        /// 
        /// Note that by the time the AccessChanged event is raised, the device handle may already be closed by the system.
        /// </summary>
        private void RegisterForDeviceAccessStatusChange()
        {
            // Enable the following registration ONLY if the Serial device under test is non-internal.
            //

            //deviceAccessInformation = DeviceAccessInformation.CreateFromId(deviceInformation.Id);
            //deviceAccessEventHandler = new TypedEventHandler<DeviceAccessInformation, DeviceAccessChangedEventArgs>(this.OnDeviceAccessChanged);
            //deviceAccessInformation.AccessChanged += deviceAccessEventHandler;
        }

        private void UnregisterFromDeviceAccessStatusChange()
        {
            deviceAccessInformation.AccessChanged -= deviceAccessEventHandler;

            deviceAccessEventHandler = null;
        }

        private void StartDeviceWatcher()
        {
            watcherStarted = true;

            if ((deviceWatcher.Status != DeviceWatcherStatus.Started)
                && (deviceWatcher.Status != DeviceWatcherStatus.EnumerationCompleted))
            {
                deviceWatcher.Start();
            }
        }

        private void StopDeviceWatcher()
        {
            if ((deviceWatcher.Status == DeviceWatcherStatus.Started)
                || (deviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted))
            {
                deviceWatcher.Stop();
            }

            watcherStarted = false;
        }

        /// <summary>
        /// If a Serial object has been instantiated (a handle to the device is opened), we must close it before the app 
        /// goes into suspension because the API automatically closes it for us if we don't. When resuming, the API will
        /// not reopen the device automatically, so we need to explicitly open the device in the app (Scenario1_ConnectDisconnect).
        ///
        /// Since we have to reopen the device ourselves when the app resumes, it is good practice to explicitly call the close
        /// in the app as well (For every open there is a close).
        /// 
        /// We must stop the DeviceWatcher because it will continue to raise events even if
        /// the app is in suspension, which is not desired (drains battery). We resume the device watcher once the app resumes again.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private void OnAppSuspension(Object sender, SuspendingEventArgs args)
        {
            if (watcherStarted)
            {
                watcherSuspended = true;
                StopDeviceWatcher();
            }
            else
            {
                watcherSuspended = false;
            }

            CloseCurrentlyConnectedDevice();
        }

        /// <summary>
        /// When resume into the application, we should reopen a handle to the Serial device again. This will automatically
        /// happen when we start the device watcher again; the device will be re-enumerated and we will attempt to reopen it
        /// if IsEnabledAutoReconnect property is enabled.
        /// 
        /// See OnAppSuspension for why we are starting the device watcher again
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arg"></param>
        private void OnAppResume(Object sender, Object args)
        {
            if (watcherSuspended)
            {
                watcherSuspended = false;
                StartDeviceWatcher();
            }
        }

        /// <summary>
        /// Close the device that is opened so that all pending operations are canceled properly.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="deviceInformationUpdate"></param>
        private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate deviceInformationUpdate)
        {
            if (IsDeviceConnected && (deviceInformationUpdate.Id == deviceInformation.Id))
            {
                // The main reasons to close the device explicitly is to clean up resources, to properly handle errors,
                // and stop talking to the disconnected device.
                CloseCurrentlyConnectedDevice();
            }
        }

        /// <summary>
        /// Open the device that the user wanted to open if it hasn't been opened yet and auto reconnect is enabled.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="deviceInfo"></param>
        private async void OnDeviceAdded(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
           // Debug.WriteLine("OnDeviceAdded_Event");
            if ((deviceInformation != null) && (deviceInfo.Id == deviceInformation.Id) && !IsDeviceConnected && isEnabledAutoReconnect)
            {
                await rootPage.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal,
                    new DispatchedHandler(async () =>
                    {
                        await OpenDeviceAsync(deviceInformation, deviceSelector);

                        // Any app specific device intialization should be done here because we don't know the state of the device when it is re-enumerated.
                    }));
            }
        }

        /// <summary>
        /// Close the device if the device access was denied by anyone (system or the user) and reopen it if permissions are allowed again
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private async void OnDeviceAccessChanged(DeviceAccessInformation sender, DeviceAccessChangedEventArgs eventArgs)
        {
            if ((eventArgs.Status == DeviceAccessStatus.DeniedBySystem)
                || (eventArgs.Status == DeviceAccessStatus.DeniedByUser))
            {
                CloseCurrentlyConnectedDevice();
            }
            else if ((eventArgs.Status == DeviceAccessStatus.Allowed) && (deviceInformation != null) && isEnabledAutoReconnect)
            {
                await rootPage.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal,
                    new DispatchedHandler(async () =>
                    {
                        await OpenDeviceAsync(deviceInformation, deviceSelector);

                        // Any app specific device intialization should be done here because we don't know the state of the device when it is re-enumerated.
                    }));
            }
        }

        //------------------------------------------ request region------------------------------------------

        public enum req_type_t : int
        {
            req_wait = 0,
            req_send,
            req_read,
            req_timeout,
            req_OK,

        }

        private req_type_t req_status = req_type_t.req_wait;

        public void Dispose()
        {
            if (ReadCancellationTokenSource != null)
            {
                ReadCancellationTokenSource.Dispose();
                ReadCancellationTokenSource = null;
            }

            if (WriteCancellationTokenSource != null)
            {
                WriteCancellationTokenSource.Dispose();
                WriteCancellationTokenSource = null;
            }
        }

        public req_type_t IsRequestStatus
        {
            get
            {
                return (req_status);
            }
        }



        private static void ResetReadCancellationTokenSource()
        {
            // Create a new cancellation token source so that can cancel all the tokens again
            ReadCancellationTokenSource = new CancellationTokenSource();
        }

        private static void ResetWriteCancellationTokenSource()
        {
            // Create a new cancellation token source so that can cancel all the tokens again
            WriteCancellationTokenSource = new CancellationTokenSource();

        }

        /// <summary>
        /// It is important to be able to cancel tasks that may take a while to complete. Cancelling tasks is the only way to stop any pending IO
        /// operations asynchronously. If the Serial Device is closed/deleted while there are pending IOs, the destructor will cancel all pending IO 
        /// operations.
        /// </summary>
        /// 

        private void CancelReadTask()
        {
            lock (ReadCancelLock)
            {
                if (ReadCancellationTokenSource != null)
                {
                    if (!ReadCancellationTokenSource.IsCancellationRequested)
                    {
                        ReadCancellationTokenSource.Cancel();

                        // Existing IO already has a local copy of the old cancellation token so this reset won't affect it
                        ResetReadCancellationTokenSource();
                    }
                }
            }
        }

        private void CancelWriteTask()
        {
            lock (WriteCancelLock)
            {
                if (WriteCancellationTokenSource != null)
                {
                    if (!WriteCancellationTokenSource.IsCancellationRequested)
                    {
                        WriteCancellationTokenSource.Cancel();

                        // Existing IO already has a local copy of the old cancellation token so this reset won't affect it
                        ResetWriteCancellationTokenSource();
                    }
                }
            }
        }
        private void CancelAllIoTasks()
        {
            CancelReadTask();
            CancelWriteTask();
        }


        private async Task ReadAsync(CancellationToken cancellationToken, ModbusDeviceRequestData Arg)
        {
            Task<UInt32> loadAsyncTask;

            uint ReadBufferLength = 512;
            byte[] iv;
 
            lock (ReadCancelLock)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Device.ReadTimeout = new System.TimeSpan(10 * 10000);
                // Cancellation Token will be used so we can stop the task operation explicitly
                // The completion function should still be called so that we can properly handle a canceled task
                DataReaderObject.InputStreamOptions = InputStreamOptions.Partial;
                loadAsyncTask = DataReaderObject.LoadAsync(ReadBufferLength).AsTask(cancellationToken);
                stateTimer = new Timer(TimerProc, Arg, serialtimeout_ms, 0);
            }

            UInt32 bytesRead = await loadAsyncTask;
            Debug.WriteLine($"ReadAsync_!!!!!!!!!!!!_{bytesRead}");

            if(req_status == req_type_t.req_send)
            {
                CancelWriteTask();
            }

            if (bytesRead > 0)
            {
                Arg.buf = new byte[bytesRead];
                DataReaderObject.ReadBytes(Arg.buf);
                ReadBytesCounter += bytesRead;
                stateTimer.Dispose();
                if(ReadCallback_Handler != null)
                {
                    Arg.req_status = regstatus_t.read_ok;
                    ReadCallback_Handler(this, Arg);
                    req_status = req_type_t.req_wait;
                }
            }
        }

        private async Task WriteAsync(CancellationToken cancellationToken, byte[] buf)
        {
            Task<UInt32> storeAsyncTask;

            DataWriteObject.WriteBytes(buf);
            // Don't start any IO if we canceled the task
            lock (WriteCancelLock)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Cancellation Token will be used so we can stop the task operation explicitly
                // The completion function should still be called so that we can properly handle a canceled task
                storeAsyncTask = DataWriteObject.StoreAsync().AsTask(cancellationToken);
            }
            
            UInt32 bytesWritten = await storeAsyncTask;
            //Debug.WriteLine("WriteAsyncTask");
            if (bytesWritten > 0)
            {
                //  WriteBytesTextBlock.Text += InputString.Substring(0, (int)bytesWritten) + '\n';
                WriteBytesCounter += bytesWritten;
                req_status= req_type_t.req_read;
            }
        }

        async private void ReadRequest(ModbusDeviceRequestData Arg)
        {
            if (IsDeviceConnected)
            {
                try
                {
                    IsReadTaskPending = true;
                    DataReaderObject = new DataReader(Device.InputStream);
                    await ReadAsync(ReadCancellationTokenSource.Token, Arg);
                }

                catch (OperationCanceledException /*exception*/)
                {
                    // NotifyReadTaskCanceled();
                    Debug.WriteLine("RRRRRRRRRRRRRRREEEEEEEEEEEAAAAAAAAAAAADDD OperationCanceledException TOKEN");
                }
                catch (Exception exception)
                {
                   //MainPage.Current.NotifyUser(exception.Message.ToString(), NotifyType.ErrorMessage);
                    Debug.WriteLine(exception.Message.ToString());
                    Debug.WriteLine("RRRRRRRRRRRRRRREEEEEEEEEEEAAAAAAAAAAAADDD TOKEN");
                }

                finally
                {
                    IsReadTaskPending = false;
                    DataReaderObject.DetachStream();
                    DataReaderObject = null;
                   // Debug.WriteLine("FINALLY READ");
                }
            }
            else
            {
                Utilities.NotifyDeviceNotConnected();
            }
        }

        private async void WriteRequest(ModbusDeviceRequestData arg)
        {
            if (IsDeviceConnected)
            {
                try
                {
                    req_status = req_type_t.req_send;
                    Debug.WriteLine($"WriteRequest(byte[] {arg.buf})");
                    IsWriteTaskPending = true;
                    DataWriteObject = new DataWriter(Device.OutputStream);
                    await WriteAsync(WriteCancellationTokenSource.Token , arg.buf);                  
                }
                catch (OperationCanceledException /*exception*/)
                {
                   // NotifyWriteTaskCanceled();
                }
                catch (Exception exception)
                {
                   // MainPage.Current.NotifyUser(exception.Message.ToString(), NotifyType.ErrorMessage);
                    Debug.WriteLine(exception.Message.ToString());
                }
                finally
                {
                  //  Debug.WriteLine("________________________FINALLY WRITE");
                    IsWriteTaskPending = false;
                    DataWriteObject.DetachStream();
                    DataWriteObject = null;
                }
            }
            else
            {
                Utilities.NotifyDeviceNotConnected();
            }
        }

        public void WriteDataToCom(object sender, ModbusDeviceRequestData arg)
        {
          //  Debug.WriteLine("WriteData111");
            if ((req_status == req_type_t.req_wait) && (ReadCallback_Handler != null) && (arg!= null) && (IsDeviceConnected))
            {
                WriteRequest(arg);
                ReadRequest(arg);
         //Debug.WriteLine("WriteData");
            }
            else
            {
                if (ReadCallback_Handler != null)
                {
                    arg.req_status = regstatus_t.error;
                    ReadCallback_Handler(null, arg);
                }
            }
        }

        private void TimerProc(object state)
        {
            // do some work not connected with UI
            Debug.WriteLine("timer_callback");
            CancelAllIoTasks();
            req_status = req_type_t.req_wait;
            stateTimer.Dispose();
            if (ReadCallback_Handler != null)
            {
                ModbusDeviceRequestData MB_Req = (ModbusDeviceRequestData) state;
                MB_Req.req_status = regstatus_t.time_out;
                ReadCallback_Handler(this, (ModbusDeviceRequestData)state);
            }
        }
    }
}
