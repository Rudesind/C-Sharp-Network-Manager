using System;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Management;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Diagnostics;

/// <summary>
/// Description: This class is used for setting and changing the network settings of a local machine.
/// A reference to System.Managment is needed to use this class. 
/// Author: Zach Nybo (4/8/2016)
/// 
/// Use the ErrorMsg variable of this class to access the friendly message of any errors encountered. 
/// If a method return's 1, -1, or false ErrorMsg can be used to display the cause and help troubleshoot the issue.
/// </summary>
public class URMNetworkMgr
{
    //This class uses WMI.

    //***Declarations***\\
    /// <summary>
    /// Holds the the friendly message for any error encountered.
    /// </summary>
    public String ErrorMsg = string.Empty;

    //***Methods***\\
    //These methods assume you are working on a computer with a single NIC.
    /// <summary>
    /// Sets the IP address of the local machine. Returns false if an error occurs.
    /// </summary>
    /// <param name="IPAddr">The new IP Address.</param>
    /// <param name="SubnetMask">The new subnet mask.</param>
    /// <param name="Gateway">(Optional)The defualt gateway for the machine. Pass null for defualt.</param>
    /// <returns></returns>
    public bool SetIpAddress(String IPAddr, String SubnetMask, String[] Gateway)
    {
        //Prepare error handle variables.
        this.ErrorMsg = string.Empty;
        String ReturnValue = string.Empty;

        //Declare management objects as null.
        ManagementClass NetConfig = null;
        ManagementObjectCollection NetCol = null;
        ManagementBaseObject StaticParams = null;
        ManagementBaseObject GatewayParams = null;
        ManagementBaseObject ErrorCode = null;
        
        //Set Objects
        try
        {
            //Sets netconfig object to all local machine network information.
            NetConfig = new ManagementClass("Win32_NetworkAdapterConfiguration");
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error creating network adapter configuration: {0}", err.Message);
            return (false);
        }

        try 
        {
            //Gets all the specific network instances(this can and will include non network adapters.
            NetCol = NetConfig.GetInstances();
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error creating network instance collection: {0}", err.Message);
            return (false);
        }

        //Cycles instances.
        foreach (ManagementObject NetAdapter in NetCol)
        {
            //This string is specifically used to avoid a network instance on Windows Embedded machines, Microsoft TV/Video Connection.
            String Caption = (String)NetAdapter["Caption"];
            //Checks a NetworkAdapterConfiguration property. Property checks if Internet Protocol is enabled on the network instance.
            if ((bool)NetAdapter["IPEnabled"] && !Caption.Contains("Microsoft TV/Video Connection"))
            {
                //If enabled, instance is a network device. Also be careful of the possiblity of "fake" adapters.
                //Get Parameters.
                try
                {
                    //StaticParams holds the parameters for the EnableStatic method.
                    StaticParams = NetAdapter.GetMethodParameters("EnableStatic");
                    //GatewayParams holds the parameters for the SetGateway method.
                    GatewayParams = NetAdapter.GetMethodParameters("SetGateways");
                }
                catch (Exception err)
                {
                    this.ErrorMsg = string.Format("Error creating parameters: {0}", err.Message);
                    return (false);
                }

                //Set Parameters.
                try
                {
                    //IPAddress and SubnetMask are string arrays so the parameters are passed as a new array.
                    StaticParams["IPAddress"] = new String[] { IPAddr };
                    StaticParams["SubnetMask"] = new String[] { SubnetMask };
                    //If gateway is null, generate defualt gateway.
                    if (Gateway == null)
                    {
                        //Creates a new string array based on the results of the method call.
                        Gateway = new String[] {DefualtGateway(IPAddr, SubnetMask)};
                        //If null the gateway creation failed.
                        if (Gateway == null)
                        {
                            this.ErrorMsg = string.Format("Error retrieving defualt gateway with IP {0} and Subnet {1}", IPAddr, SubnetMask);
                            return (false);
                        }
                    }
                    //Set DefaultIPGateway directly to the parameter.
                    GatewayParams["DefaultIPGateway"] = Gateway;      
                        
                }
                catch (Exception err)
                {
                    this.ErrorMsg = string.Format("Error initializing parameters: {0}", err.Message);
                    return (false);
                }

                //Invoke Methods.
                try
                {
                    //Call the NetworkAdapterConfiguration method EnableStatic on the current network instance using the parameter object.
                    ErrorCode = NetAdapter.InvokeMethod("EnableStatic", StaticParams, null);
                    //The return value of the executed command is returned from the Errorcode object into ReturnValue.
                    ReturnValue = ErrorCode.GetPropertyValue("ReturnValue").ToString();
                    //If no errors are found, set the gateway as well.
                    if (ReturnValue == "0" || ReturnValue == "1")
                    {
                        ErrorCode = NetAdapter.InvokeMethod("SetGateways", GatewayParams, null);
                        ReturnValue = ErrorCode.GetPropertyValue("ReturnValue").ToString();
                    }
                }
                catch (Exception err)
                {
                    this.ErrorMsg = string.Format("Error invoking methods: {0}", err.Message);
                    return (false);
                }    
            }
        }

        //Non Exceptional Errors.
        switch (ReturnValue)
        {
            case "0":
                return true;
            
            case "1":
                this.ErrorMsg = string.Format("Restart required");
                return true;

            case "66":
                this.ErrorMsg = string.Format("Invalid subnet mask {0}", SubnetMask);
                return false;

            case "70":
                this.ErrorMsg = string.Format("Invalid IP Address {0}", IPAddr);
                return false;

            default:
                this.ErrorMsg = string.Format("Unexpected error, IP address not set: Code {0}", ReturnValue);
                return false;
        }                                        
    }
    
    /// <summary>
    /// Sets or overwrites the DNS servers for the local machine.
    /// </summary>
    /// <param name="DNSservers">An array containing the IP address's of the servers to use.</param>
    /// <param name="Replace">Set to true if the current DNS servers should be replaced.</param>
    /// <returns></returns>
    public bool SetDNSServers(String[] DNSservers, bool Replace)
    {
        //Method closely resembles SetIpAddress, only changes will be highlighted.
        this.ErrorMsg = string.Empty;
        String ReturnValue = string.Empty;

        //Declarations
        ManagementClass NetConfig = null;
        ManagementObjectCollection NetCol = null;
        ManagementBaseObject StaticParams = null;
        ManagementBaseObject ErrorCode = null;

        //Set Objects.
        try
        {
            NetConfig = new ManagementClass("Win32_NetworkAdapterConfiguration");
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error creating network adapter configuration object: {0}", err.Message);
            return (false);
        }

        try
        {
            NetCol = NetConfig.GetInstances();
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error creating network instance collection object: {0}", err.Message);
            return (false);
        }

        //Cycle instances.
        foreach (ManagementObject NetAdapter in NetCol)
        {
            String Caption = (String)NetAdapter["Caption"];
            if ((bool)NetAdapter["IPEnabled"] && !Caption.Contains("Microsoft TV/Video Connection"))
            {
                //Get Parameters.
                try
                {
                    //Gets SetDNSServerSearchOrder method parameters.
                    StaticParams = NetAdapter.GetMethodParameters("SetDNSServerSearchOrder");
                }
                catch (Exception err)
                {
                    this.ErrorMsg = string.Format("Error creating parameters: {0}", err.Message);
                    return (false);
                }

                //Set Parameters.
                try
                {
                    //Checks if the parameter Replace is true or not.
                    if (Replace)
                    {
                        //If true, the current DNS servers are replaced by the new ones.
                        StaticParams["DNSServerSearchOrder"] = DNSservers;
                    }
                    else
                    {
                        //If false, the new DNS servers are added on to the current ones.
                        //Creates a new array containing the current DNS servers.
                        String[] CrntServers = (String[])NetAdapter["DNSServerSearchOrder"];
                        //Creates a dynamic array type List to hold both arrays.
                        List<String> list = new List<String>(CrntServers.Length + DNSservers.Length);
                        //Adds the current and new arrays to the list
                        list.AddRange(CrntServers);
                        list.AddRange(DNSservers);
                        //Sets parameter to the list as an array.
                        StaticParams["DNSServerSearchOrder"] = list.ToArray();
                        
                    }      
                    
                }
                catch (Exception err)
                {
                    this.ErrorMsg = string.Format("Error initializing parameters: {0}", err.Message);
                    return (false);
                }

                //Invoke Methods.
                try
                {
                    //Invoke the SetDNSServerSearchOrder at the new parameters.
                    ErrorCode = NetAdapter.InvokeMethod("SetDNSServerSearchOrder", StaticParams, null);

                    ReturnValue = ErrorCode.GetPropertyValue("ReturnValue").ToString();
                }
                catch (Exception err)
                {
                    this.ErrorMsg = string.Format("Error invoking method: {0}", err.Message);
                    return (false);
                }
            }
        }

        //Non Exceptional Errors.
        switch (ReturnValue)
        {
            case "0":
                return true;

            case "1":
                this.ErrorMsg = string.Format("Restart required");
                return true;

            case "70":
                this.ErrorMsg = string.Format("Invalid IP Address:");
                for (int i = 0; i < DNSservers.Length; i++)
                {
                    this.ErrorMsg += string.Format(" {0}", DNSservers[i]);
                }
                return false;

            default:
                this.ErrorMsg = string.Format("Unexpected error, DNS not set: Code {0}", ReturnValue);
                return false;
        }

    }

    /// <summary>
    /// Sets the local machine's hostname. A Reboot is required to complete change.
    /// </summary>
    /// <param name="Hostname">The new name to use.</param>
    /// <param name="Reboot">Set to true if reboot is desired after change.</param>
    /// <returns></returns>
    public bool SetHostname(String Hostname, bool Reboot)
    {
        //Method closely resembles SetIpAddress, only changes will be highlighted.
        this.ErrorMsg = string.Empty;
        String ReturnValue = string.Empty;

        //Declarations
        ManagementObject ComSys = null;
        ManagementBaseObject StaticParams = null;
        ManagementBaseObject ErrorCode = null;

        //Set Objects.
        try
        {
            //Creates Management object of the Win32 class ComputerSystem where the name is equal to the local machine name.
            ComSys = new ManagementObject("Win32_ComputerSystem.Name='" + Environment.MachineName + "'");;
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error creating computer system object: {0}", err.Message);
            return (false);
        }

        //Get Parameters.
        try
        {
            //Gets parameters of the ComputerSystem method Rename.
            StaticParams = ComSys.GetMethodParameters("Rename");
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error creating paramaters object: {0}", err.Message);
            return (false);
        }

        //Set the parameters.
        try
        {
            //Sets the name parameter.
            StaticParams["Name"] = Hostname;
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error initializing parameters: {0}", err.Message);
            return (false);
        }

        //Invoke Methods. 
        try
        {
            //Call the ComputerSystem method Rename on the ComputerSystem object with parameter object.
            ErrorCode = ComSys.InvokeMethod("Rename", StaticParams, null);
            //The return value of the excited command is returned from the Errorcode object into FndErrs
            ReturnValue = ErrorCode.GetPropertyValue("ReturnValue").ToString();
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error invoking method: {0}", err.Message);
            return (false);
        }

        //Non Exception Errors.
        switch (ReturnValue)
        {
            case "0":
                //Check Reboot.
                if (Reboot)
                {
                    //If true call restart.
                    if (!Restart())
                    {
                        return false;
                    }
                }
                return true;

            case "1":
                if (Reboot)
                {
                    if (!Restart())
                    {
                        return false;
                    }
                }
                this.ErrorMsg = string.Format("Restart required");
                return true;

            case "5":
                this.ErrorMsg = string.Format("Error could not rename computer, access is denied");
                return false;

            default:
                this.ErrorMsg = string.Format("Unexpected error, hostname not changed: {0}", ReturnValue);
                return false;
        }  
    }

    /// <summary>
    /// Enables dynamic IP resolution.
    /// </summary>
    /// <returns></returns>
    public bool EnableDHCP()
    {
        //Method closely resembles SetIpAddress, only changes will be highlighted.
        this.ErrorMsg = string.Empty;
        String ReturnValue = string.Empty;

        ManagementClass NetConfig = null;
        ManagementObjectCollection NetCol = null;
        ManagementBaseObject ErrorCode = null;

        //Objects
        try
        {
            NetConfig = new ManagementClass("Win32_NetworkAdapterConfiguration");
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error creating network adapter configuration object: {0}", err.Message);
            return (false);
        }

        try
        {
            NetCol = NetConfig.GetInstances();
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error creating network instance collection object: {0}", err.Message);
            return (false);
        }

        //Cycles instances.
        foreach (ManagementObject NetAdapter in NetCol)
        {
            String Caption = (String)NetAdapter["Caption"];
            if ((bool)NetAdapter["IPEnabled"] && !Caption.Contains("Microsoft TV/Video Connection"))
            {
                //Invoke Methods.
                try
                {
                    //Invoke EnableDHCP method with no parameters.
                    ErrorCode = NetAdapter.InvokeMethod("EnableDHCP", null, null);
                    ReturnValue = ErrorCode.GetPropertyValue("ReturnValue").ToString();
                }
                catch (Exception err)
                {
                    this.ErrorMsg = string.Format("Error enabling DHCP: {0}", err.Message);
                    return (false);
                }
            }
        }

        //Non Exceptional Errors.
        switch (ReturnValue)
        {
            case "0":
                return true;

            case "1":
                this.ErrorMsg = string.Format("Restart required");
                return true;

            default:
                this.ErrorMsg = string.Format("Unexpected error, DHCP not enabled: {0}", ReturnValue);
                return false;
        }      
    }

    /// <summary>
    /// Enables dynamic DNS resolution.
    /// </summary>
    /// <returns></returns>
    public bool EnableDNS()
    {
        //Method closely resembles SetIpAddress, only changes will be highlighted.
        this.ErrorMsg = string.Empty;
        String ReturnValue = string.Empty;

        //Declarations
        ManagementClass NetConfig = null;
        ManagementObjectCollection NetCol = null;
        ManagementBaseObject ErrorCode = null;
        ManagementBaseObject StaticParams = null;

        //Set Objects.
        try
        {
            NetConfig = new ManagementClass("Win32_NetworkAdapterConfiguration");
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error creating network adapter configuration object: {0}", err.Message);
            return (false);
        }

        try
        {
            NetCol = NetConfig.GetInstances();
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error creating network instance collection object: {0}", err.Message);
            return (false);
        }

        //Cycle instances.
        foreach (ManagementObject NetAdapter in NetCol)
        {
            String Caption = (String)NetAdapter["Caption"];
            if ((bool)NetAdapter["IPEnabled"] && !Caption.Contains("Microsoft TV/Video Connection"))
            {
                //Get Parameters
                try
                {
                    //The parameters and methods of SetDNSServerSearchOrder are used in this method.
                    StaticParams = NetAdapter.GetMethodParameters("SetDNSServerSearchOrder");
                }
                catch (Exception err)
                {
                    this.ErrorMsg = string.Format("Error creating parameters: {0}", err.Message);
                    return (false);
                }
                
                //Set Parameters
                try
                {
                    //Set DNSServerSearchOrder to null, causing the network adapter to use DNS.
                    StaticParams["DNSServerSearchOrder"] = null;
                }
                catch (Exception err)
                {
                    this.ErrorMsg = string.Format("Error initializing parameters: {0}", err.Message);
                    return (false);
                }

                //Invoke Method.
                try
                {
                    //Call SetDNSServerSearchOrder method with null parameters.
                    ErrorCode = NetAdapter.InvokeMethod("SetDNSServerSearchOrder", StaticParams, null); 
                    ReturnValue = ErrorCode.GetPropertyValue("ReturnValue").ToString();
                }
                catch (Exception err)
                {
                    this.ErrorMsg = string.Format("Error enabling DNS: {0}", err.Message);
                    return (false);
                }
            }
        }

        //Non Exceptional Errors.
        switch (ReturnValue)
        {
            case "0":
                return true;

            case "1":
                this.ErrorMsg = string.Format("Restart required");
                return true;

            default:
                this.ErrorMsg = string.Format("Unexpected error, DNS not enabled: {0}", ReturnValue);
                return false;
        }
    }

    /// <summary>
    /// Retrieves the local machine's IP Address.
    /// </summary>
    /// <returns></returns>
    public String GetIpAddress()
    {
        //Method closely resembles SetIpAddress, only changes will be highlighted
        this.ErrorMsg = string.Empty;

        //Declarations
        //Ip holds all the current IP addresss.
        String[] Ip = null;
        ManagementClass NetConfig = null;
        ManagementObjectCollection NetCol = null;

        //Sets Object.
        try
        {
            NetConfig = new ManagementClass("Win32_NetworkAdapterConfiguration");
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error creating network adapter configuration object: {0}", err.Message);
            return (null);
        }

        try
        {
            NetCol = NetConfig.GetInstances();
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error creating network instance collection object: {0}", err.Message);
            return (null);
        }
        //Cycles instances.
        foreach (ManagementObject NetAdapter in NetCol)
        {
            String Caption = (String)NetAdapter["Caption"];
            if ((bool)NetAdapter["IPEnabled"] && !Caption.Contains("Microsoft TV/Video Connection"))
            {
                //Get Properties.
                try
                {
                    //Sets IP to the NetworkAdapterConfiguration property IPAddress.
                    Ip = (String[])NetAdapter["IPAddress"];
                    //In most cases the local machine is only going to contain one relevent IP Address. If more IP address are needed, have method return array.
                    return Ip[0];
                }
                catch (Exception err)
                {
                    this.ErrorMsg = string.Format("Error getting IP address: {0}", err.Message);
                    return (null);
                }
            }
        }

        this.ErrorMsg = "No IP Enabled Devices found";
        return (null);
    }

	/// <summary>
	/// Retrieves a remote machine's IP Address.
	/// </summary>
	/// <param name="Hostname">Hostname of remote machine.</param>
	/// <returns></returns>
	public string GetRemoteIpAddress(String Hostname)
	{
		this.ErrorMsg = string.Empty;
		//A list to contain the resolved IP address's.
		IPAddress[] host = null;
		//Holds the hosts associated IP addresses.
		string ipAddress = String.Empty;

		//Checks to see if the network is available.
		if (NetworkInterface.GetIsNetworkAvailable())
		{
			//Resolves the hostname to a list of IP's
			try
			{
				host = Dns.GetHostAddresses(Hostname);
			}
			catch (Exception err)
			{
				this.ErrorMsg = string.Format("Could not resolve the host {0}: {1}", Hostname, err.Message);
				return (null);
			}

			try
			{
				for (int i = 0; i < host.Length; i++)
				{
					ipAddress += host[i].ToString() + " ";
				}
				return (ipAddress);
			}
			catch (Exception err)
			{
				this.ErrorMsg = string.Format("Could not retrive the IP address from host {0}:{1}", Hostname, err.Message);
				return (null);
			}
		}

		this.ErrorMsg = string.Format("No network connection");
		return (null);
	}

    /// <summary>
    /// Retrieves the local machine's Subnet Mask.
    /// </summary>
    /// <returns></returns>
    public String GetSubnetMask()
    {
        //Method closely resembles SetIpAddress, only changes will be highlighted
        this.ErrorMsg = string.Empty;

        //Declarations
        //Array holds local subnet masks.
        String[] SubnetMask = null;
        ManagementClass NetConfig = null;
        ManagementObjectCollection NetCol = null;

        //Set Objects.
        try
        {
            NetConfig = new ManagementClass("Win32_NetworkAdapterConfiguration");
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error creating network adapter configuration object: {0}", err.Message);
            return (null);
        }

        try
        {
            NetCol = NetConfig.GetInstances();
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error creating network instance collection object: {0}", err.Message);
            return (null);
        }
        //Cycles instances.
        foreach (ManagementObject NetAdapter in NetCol)
        {
            String Caption = (String)NetAdapter["Caption"];
            if ((bool)NetAdapter["IPEnabled"] && !Caption.Contains("Microsoft TV/Video Connection"))
            {
                //Get Properties.
                try
                {
                    //Sets SubnetMask to the NetworkAdapterConfiguration property IPSubnet.
                    SubnetMask = (String[])NetAdapter["IPSubnet"];
                    return SubnetMask[0];
                }
                catch (Exception err)
                {
                    this.ErrorMsg = string.Format("Error getting Subnet Mask: {0}", err.Message);
                    return (null);
                }
            }
        }

        this.ErrorMsg = "No IP Enabled Devices found";
        return (null);
    }

    /// <summary>
    /// Retrieves the local machine's DNS Servers.
    /// </summary>
    /// <returns></returns>
    public String[] GetDNSServers()
    {
        //Method closely resembles SetIpAddress, only changes will be highlighted
        this.ErrorMsg = string.Empty;

        //Declarations
        //Array holds DNS servers.
        String[] Servers = null;
        ManagementClass NetConfig = null;
        ManagementObjectCollection NetCol = null;

        //Set Objects.
        try
        {
            NetConfig = new ManagementClass("Win32_NetworkAdapterConfiguration");
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error creating network adapter configuration object: {0}", err.Message);
            return (null);
        }

        try
        {
            NetCol = NetConfig.GetInstances();
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error creating network instance collection object: {0}", err.Message);
            return (null);
        }

        //Cycles instances.
        foreach (ManagementObject NetAdapter in NetCol)
        {
            String Caption = (String)NetAdapter["Caption"];
            if ((bool)NetAdapter["IPEnabled"] && !Caption.Contains("Microsoft TV/Video Connection"))
            {
                //Get Properties.
                try
                {
                    //Sets Servers to the NetworkAdapterConfiguration property DNSServerSearchOrder.
                    Servers = (String[])NetAdapter["DNSServerSearchOrder"];
                    //Returns all DNS servers.
                    return Servers;
                }
                catch (Exception err)
                {
                    this.ErrorMsg = string.Format("Error getting DNS servers: {0}", err.Message);
                    return (null);
                }
            }
        }

        this.ErrorMsg = "No IP Enabled Devices found";
        return null;
    }

    /// <summary>
    /// Returns the current gateway.
    /// </summary>
    /// <returns></returns>
    public String[] GetGateway()
    {
        //Method closely resembles SetIpAddress, only changes will be highlighted       
        this.ErrorMsg = string.Empty;

        //Declarations
        String[] Gateway = null;
        ManagementClass NetConfig = null;
        ManagementObjectCollection NetCol = null;

        //Set Object.
        try
        {
            NetConfig = new ManagementClass("Win32_NetworkAdapterConfiguration");
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error creating network adapter configuration object: {0}", err.Message);
            return (null);
        }

        try
        {
            NetCol = NetConfig.GetInstances();
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error creating network instance collection object: {0}", err.Message);
            return (null);
        }

        //Cycles instances.
        foreach (ManagementObject NetAdapter in NetCol)
        {
            String Caption = (String)NetAdapter["Caption"];
            if ((bool)NetAdapter["IPEnabled"] && !Caption.Contains("Microsoft TV/Video Connection"))
            {
                //Get Properties.
                try
                {
                    //Sets Servers to the NetworkAdapterConfiguration property DefaultIPGateway.
                    Gateway = (String[])NetAdapter["DefaultIPGateway"];
                    return Gateway;
                }
                catch (Exception err)
                {
                    this.ErrorMsg = string.Format("Error getting Gateway servers: {0}", err.Message);
                    return (null);
                }
            }
        }

        this.ErrorMsg = "No IP Enabled Devices found";
        return null;
    }

    /// <summary>
    /// Retrieves the local machine's hostname.
    /// </summary>
    /// <returns></returns>
    public String GetHostname()
    {
        //Get hostname by Environment variable.
        try
        {
            return Environment.MachineName;
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error could not retrieve local computer name: {0}", err.Message);
        }

        return null;
    }

	public String GetRemoteHostname(String Ip)
	{
		//Holds the remote IP address entry
		IPAddress remoteIp = null;

		IPHostEntry host = null;

		try
		{
			remoteIp = IPAddress.Parse(Ip);
			host = Dns.GetHostEntry(remoteIp);
		}
		catch (Exception err)
		{
			this.ErrorMsg = String.Format("Error parsing IP address {0}: {1}", Ip, err.Message);
			return (null);
		}

		try
		{
			return (host.HostName.ToString());
		}
		catch (Exception err)
		{
			this.ErrorMsg = String.Format("Error retriving host name of IP address {0}: {1}", Ip, err.Message);
			return (null);
		}
		
	}

    /// <summary>
    /// Pings a host and reports back the number of successful pings. On error returns -1.
    /// </summary>
    /// <param name="Hostname">The name of the host to ping.</param>
    /// <param name="Count">The number of times to ping.</param>
    /// <returns></returns>
    public float PingIt(string Hostname, int Count)
    {
        this.ErrorMsg = string.Empty;
        //Holds the amount of successful pings.
        float success = 0;
        //Holds the specific ping options, 64 is the TTL. True doesn't fragment the packet.
        PingOptions options = new PingOptions(64, true);
        //A 32 byte variable that contains the data to ping with.
        byte[] buffer = new byte[32];
        //The object that conducts network commands.
        Ping attempt = new Ping();
        //A list to contain the resolved IP address's.
        IPAddress[] host = null;
        //Holds the result of the ping.
        PingReply reply = null;
        
        //Checks to see if the network is available.
        if (NetworkInterface.GetIsNetworkAvailable())
        {
            //Resolves the hostname to a list of IP's
            try
            {
                host = Dns.GetHostAddresses(Hostname);
            }
            catch (Exception err)
            {
                this.ErrorMsg = string.Format("Could not resolve the host {0}: {1}", Hostname, err.Message);
                return (-1);
            }

            //Loop to cycle for the desired number of pings.
            for (int i = 1; i <= Count; i++)
            {
                //Ping machine
                try
                {
                    //Sends a ping message at a host, with a time out of 1000, data buffer, and options of options.
                    reply = attempt.Send(host[0], 1000, buffer, options);

                    //Checks the reply for success
                    if (reply.Status == IPStatus.Success)
                    {
                        //If success, increment success
                        success++;
                    }
                }
                catch (Exception err)
                {
                    this.ErrorMsg = string.Format("Error pinging host {0} with IP {1}: {2}", Hostname, host[0].ToString(), err.Message);
                    return (-1);
                }
            }

            return success;
        }

        this.ErrorMsg = string.Format("No network connection");
        return (-1);
    }

    /// <summary>
    /// Generates a defualt gateway based on an IP address and subnet mask.
    /// </summary>
    /// <param name="IPAddr">IP address to use.</param>
    /// <param name="SubnetMask">Subnet mask to use.</param>
    /// <returns></returns>
    private String DefualtGateway(String IPAddr, String SubnetMask)
    {
        //Arrays hold ip and subnet split by a period
        String[] SubArr = null;
        String[] IPArr = null;
        //Holds the octets for the ip address and subnet
        int SubOct = 0;
        int IPOct = 0;
        //Holds the calculated gateway
        String Gateway = null;

        try
        {
            //Split subnet and ip address.
            SubArr = SubnetMask.Split('.');
            IPArr = IPAddr.Split('.');
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error initializing IP {0} and Subnet {1} for defualt gateway: {2}", IPAddr, SubnetMask, err.Message); 
            return null;
        }

        try
        {
            //Run loop for size of one of the arrays. Should only be 4 passes, or 0 to 3.
            for (int i = 0; i < SubArr.Length; i++)
            {
                //Convert octets to integer.
                SubOct = Convert.ToInt32(SubArr[i]);
                IPOct = Convert.ToInt32(IPArr[i]);

                //Perform bitwise calculations and what to do on the final octet.
                if (i == (SubArr.Length - 1) && SubOct == 0)
                {
                    Gateway += "1";
                }
                else if (i == (SubArr.Length - 1))
                {
                    Gateway += (SubOct & IPOct);
                }
                else
                {
                    Gateway += (SubOct & IPOct) + ".";
                }

            }
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error calculating defualt gateway with IP {0} and Subnet {1}: {2}", IPAddr, SubnetMask, err.Message);
            return null;
        }

        return Gateway;
    }

    /// <summary>
    /// Used to restart the local machine.
    /// </summary>
    /// <returns></returns>
    private bool Restart()
    {
        //Restarts computer via shutdown command.
        try
        {
            Process Shutdown = null;
            Shutdown = Process.Start("shutdown.exe", "-r -t 5");

            //Wait for process to close.
            do
            {
                //Check if proccess has exited.
                if (Shutdown.HasExited)
                {
                    //Check a for a success return code of 0.
                    if (Shutdown.ExitCode == 0)
                    {
                        return true;
                    }
                }

            } while (Shutdown.WaitForExit(1000));

            return false;
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error failed to restart: {0}", err.Message);
            return false;
        }

       
        
        /* WMI Method of Restart\\
        //Method closely resembles SetIpAddress, only changes will be highlighted.
        this.ErrorMsg = string.Empty;
        String ReturnValue = string.Empty;

        ManagementClass Shutdown = null;
        ManagementBaseObject ErrorCode = null;
        ManagementBaseObject ShutdownParams = null;

        //Get Objects
        try
        {
            //Gets OperatingSystem class.
            Shutdown = new ManagementClass("Win32_OperatingSystem");
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error creating shutdown object: {0}", err.Message);
            return false;
        }

        //Get Parameters.
        try
        {
            //Get Parameters of OperatingSystem method Win32Shutdown.
            ShutdownParams = Shutdown.GetMethodParameters("Win32Shutdown");

        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error creating shutdown parameters: {0}", err.Message);
            return false;
        }

        //Set Parameters.
        try
        {
            //Flags are the shut down type. 1 is shutdown, 2 is reboot. 6 is forced reboot.
            ShutdownParams["Flags"] = "6";
            ShutdownParams["Reserved"] = "0";
        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error initializing shutdown parameters: {0}", err.Message);
            return false;
        }

        //Invoke Methods.
        try
        {
            Shutdown.Scope.Options.EnablePrivileges = true;
            //Cycle instances.
            foreach (ManagementObject OperatingSystem in Shutdown.GetInstances())
            {
                //Enable privileges to allow reboot of computer.
                Shutdown.Scope.Options.EnablePrivileges = true;
                OperatingSystem.Scope.Options.EnablePrivileges = true;
                //Invoke OperatingSystem method Win32Shutdown with new parameters.
                ErrorCode = OperatingSystem.InvokeMethod("Win32Shutdown", ShutdownParams, null);
            }
            ReturnValue = ErrorCode.GetPropertyValue("ReturnValue").ToString();

        }
        catch (Exception err)
        {
            this.ErrorMsg = string.Format("Error invoking reboot: {0}", err.Message);
            return false;
        }

        switch (ReturnValue)
        {
            case "0":
                return true;

            default:
                this.ErrorMsg = string.Format("Unexpected error: {0}", ReturnValue);
                return false;
        }
         */
    }
}

    