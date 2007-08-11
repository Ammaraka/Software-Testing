/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Types;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Communications.Caches;
using OpenSim.Framework.Console;
using OpenSim.Framework.Utilities;

namespace OpenSim.Region.Communications.Local
{
    public class CommunicationsLocal : CommunicationsManager
    {
        public LocalBackEndServices SandBoxServices = new LocalBackEndServices();
        public LocalUserServices UserServices;

        public CommunicationsLocal(NetworkServersInfo serversInfo, BaseHttpServer httpServer, AssetCache assetCache, bool accountsAuthenticate )
            : base(serversInfo, httpServer, assetCache)
        {
            UserServices = new LocalUserServices(this, serversInfo, accountsAuthenticate);
            UserServices.AddPlugin("OpenSim.Framework.Data.DB4o.dll");
            UserServer = UserServices;
            GridServer = SandBoxServices;
            InterRegion = SandBoxServices;
            httpServer.AddXmlRPCHandler("login_to_simulator", UserServices.XmlRpcLoginMethod);
        }

        internal void InformRegionOfLogin(ulong regionHandle, Login login)
        {
            this.SandBoxServices.AddNewSession(regionHandle, login);
        }

        public void do_create(string what)
        {
            switch (what)
            {
                case "user":
                    string tempfirstname;
                    string templastname;
                    string tempMD5Passwd;
                    uint regX = 1000;
                    uint regY = 1000;

                    tempfirstname = MainLog.Instance.CmdPrompt("First name");
                    templastname = MainLog.Instance.CmdPrompt("Last name");
                    tempMD5Passwd = MainLog.Instance.PasswdPrompt("Password");
                    regX = Convert.ToUInt32(MainLog.Instance.CmdPrompt("Start Region X"));
                    regY = Convert.ToUInt32(MainLog.Instance.CmdPrompt("Start Region Y"));

                    tempMD5Passwd = Util.Md5Hash(Util.Md5Hash(tempMD5Passwd) + ":" + "");

                    this.UserServices.AddUserProfile(tempfirstname, templastname, tempMD5Passwd, regX, regY);
                    break;
            }
        }

    }
}
