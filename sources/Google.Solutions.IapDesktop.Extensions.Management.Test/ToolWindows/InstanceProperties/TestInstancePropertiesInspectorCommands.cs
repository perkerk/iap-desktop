﻿//
// Copyright 2023 Google LLC
//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//

using Google.Solutions.IapDesktop.Application.Windows;
using Google.Solutions.IapDesktop.Core.ProjectModel;
using Google.Solutions.IapDesktop.Extensions.Management.ToolWindows.InstanceProperties;
using Google.Solutions.Mvvm.Binding.Commands;
using Moq;
using NUnit.Framework;

namespace Google.Solutions.IapDesktop.Extensions.Management.Test.ToolWindows.InstanceProperties
{
    [TestFixture]
    public class TestInstancePropertiesInspectorCommands
    {
        //---------------------------------------------------------------------
        // ContextMenuOpen.
        //---------------------------------------------------------------------

        [Test]
        public void ContextMenuOpen_WhenApplicable()
        {
            var toolWindowHost = new Mock<IToolWindowHost>();
            var commands = new InstancePropertiesInspectorCommands(toolWindowHost.Object);

            Assert.AreEqual(
                CommandState.Enabled,
                commands.ContextMenuOpen.QueryState(new Mock<IProjectModelInstanceNode>().Object));
        }

        [Test]
        public void ContextMenuOpen_WhenNotApplicable()
        {
            var toolWindowHost = new Mock<IToolWindowHost>();
            var commands = new InstancePropertiesInspectorCommands(toolWindowHost.Object);

            Assert.AreEqual(
                CommandState.Unavailable,
                commands.ContextMenuOpen.QueryState(new Mock<IProjectModelCloudNode>().Object));
            Assert.AreEqual(
                CommandState.Unavailable,
                commands.ContextMenuOpen.QueryState(new Mock<IProjectModelProjectNode>().Object));
            Assert.AreEqual(
                CommandState.Unavailable,
                commands.ContextMenuOpen.QueryState(new Mock<IProjectModelZoneNode>().Object));
        }

        //---------------------------------------------------------------------
        // ToolbarOpen.
        //---------------------------------------------------------------------

        [Test]
        public void ToolbarOpen_WhenApplicable()
        {
            var toolWindowHost = new Mock<IToolWindowHost>();
            var commands = new InstancePropertiesInspectorCommands(toolWindowHost.Object);

            Assert.AreEqual(
                CommandState.Enabled,
                commands.ToolbarOpen.QueryState(new Mock<IProjectModelInstanceNode>().Object));
        }

        [Test]
        public void ToolbarOpen_WhenNotApplicable()
        {
            var toolWindowHost = new Mock<IToolWindowHost>();
            var commands = new InstancePropertiesInspectorCommands(toolWindowHost.Object);

            Assert.AreEqual(
                CommandState.Disabled,
                commands.ToolbarOpen.QueryState(new Mock<IProjectModelCloudNode>().Object));
            Assert.AreEqual(
                CommandState.Disabled,
                commands.ToolbarOpen.QueryState(new Mock<IProjectModelProjectNode>().Object));
            Assert.AreEqual(
                CommandState.Disabled,
                commands.ToolbarOpen.QueryState(new Mock<IProjectModelZoneNode>().Object));
        }

        //---------------------------------------------------------------------
        // WindowMenuOpen.
        //---------------------------------------------------------------------

        [Test]
        public void WindowMenuOpen_IsEnabled()
        {
            var toolWindowHost = new Mock<IToolWindowHost>();
            var context = new Mock<IMainWindow>();

            var commands = new InstancePropertiesInspectorCommands(toolWindowHost.Object);

            Assert.AreEqual(
                CommandState.Enabled,
                commands.WindowMenuOpen.QueryState(context.Object));
        }
    }
}
