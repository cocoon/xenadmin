﻿/* Copyright (c) Citrix Systems Inc. 
 * All rights reserved. 
 * 
 * Redistribution and use in source and binary forms, 
 * with or without modification, are permitted provided 
 * that the following conditions are met: 
 * 
 * *   Redistributions of source code must retain the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer. 
 * *   Redistributions in binary form must reproduce the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer in the documentation and/or other 
 *     materials provided with the distribution. 
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND 
 * CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF 
 * MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR 
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, 
 * BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR 
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF 
 * SUCH DAMAGE.
 */

using System;
using XenAPI;
using XenAdmin.Diagnostics.Problems;
using XenAdmin.Core;
using System.IO;
using XenAdmin.Diagnostics.Problems.HostProblem;
using System.Text.RegularExpressions;
using System.Xml;
using System.Collections.Generic;
using XenAdmin.Actions;
using System.Linq;


namespace XenAdmin.Diagnostics.Checks
{
    class DiskSpaceForBatchUpdatesCheck : Check
    {
        private readonly long size = 0;

        public DiskSpaceForBatchUpdatesCheck(Host host, long size)
            : base(host)
        {
            this.size = size;
        }

        protected override Problem RunCheck()
        {
            if (!Host.IsLive)
                return new HostNotLiveWarning(this, Host);

            if (!Host.Connection.IsConnected)
                throw new EndOfStreamException(Helpers.GetName(Host.Connection));

            if (Helpers.CreamOrGreater(Host.Connection))
            {
                var action = new GetDiskSpaceRequirementsAction(Host, size, true, DiskSpaceRequirements.OperationTypes.autoupdate);

                try
                {
                    action.RunExternal(action.Session);
                }
                catch
                {
                    log.WarnFormat("Could not get disk space requirements");
                }

                if (action.Succeeded && action.DiskSpaceRequirements.AvailableDiskSpace < size)
                    return new HostOutOfSpaceProblem(this, Host, action.DiskSpaceRequirements);
            }

            return null;
            
        }

        public override string Description
        {
            get { return Messages.SERVER_SIDE_CHECK_AUTO_MODE_DESCRIPTION; }
        }

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    }
}