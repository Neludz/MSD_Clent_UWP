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
using Windows.UI.Xaml.Controls;
using CustomSerialDeviceAccess;

namespace SDKTemplate
{
    public partial class MainPage : Page
    {
        public const string FEATURE_NAME = "CustomSerialDeviceAccess";

        List<Scenario> scenarios = new List<Scenario>
        {
            new Scenario() { Title="MSD_2", ClassType=typeof(MSD_2)},
            new Scenario() { Title="Connect/Disconnect", ClassType=typeof(Scenario1_ConnectDisconnect)},
            new Scenario() { Title="Configure Device", ClassType=typeof(Scenario2_ConfigureDevice)},
            new Scenario() { Title="Read/Write", ClassType=typeof(Scenario3_ReadWrite)},
            new Scenario() { Title="Events", ClassType=typeof(Scenario4_Events)}
        };
    }

    public class Scenario
    {
        public string Title { get; set; }
        public Type ClassType { get; set; }
    }
}
