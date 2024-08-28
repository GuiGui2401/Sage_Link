using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using tBool = System.Boolean;
using tChar = System.String;
using tUInt16 = System.UInt16;
using tUInt8 = System.Byte;

namespace MAXI_X3
{
    internal class Program
    {

        private static tChar m_pui8_IPAddressSageX3;
        private const tChar m_pui8_IPAddressLocalHost = "127.0.0.1";
        private static tChar m_pui8_IPAddressHostSystem = "";
        private const tUInt16 m_ui16_TerminalWaitTime = 20000;
        public static tUInt8 m_ui8_HttpRequestTimeoutInSecond = 180;
        private const tUInt8 m_ui8_NumDaysAllowedWihtoutTransDTUpd = 3;
        //private const tChar m_pui8_EmailMAximoconnect = "gerald.cezy.emvoutou@dta-alliance.com";
        public const tChar m_pui8_EmailMAximoconnect = "maximoconnect_pad@dta-alliance.com";
        public const tChar m_pui8_Password = "#Fallone01";
        public const tChar m_pui8_EmailAdmin = "emgerald@hotmail.de";
        public const tChar m_pui8_EmailAdmin_2 = "steve.fotsingtomfeu@pad.cm";

        // Path settings
        public const tChar m_pui8_DirectoryPath = "../../../MaximoConnect_Settings";
        public const tChar m_pui8_LogFileFolder = m_pui8_DirectoryPath + "/LOGs";
        public static tChar m_pui8_LogFilePath = "";
        private const tChar m_pui8_ConfigFilePath = m_pui8_DirectoryPath + "/SageIPAdresse_Config.txt";
        private const tChar m_pui8_LastTransferDateTimeFile = m_pui8_DirectoryPath + "/lastDateTime_PurchaseRequest.json";
        public const tChar m_pui8_PurchaseRequestListLocation = m_pui8_DirectoryPath + "/PRList.json";

        //  Environnement settings
        public static readonly E_TransactionType m_e_TransactionType = E_TransactionType.e_Transaction_STD;
        public static readonly E_Environnement_SageX3 m_e_Environnement_SageX3 = E_Environnement_SageX3.e_Environnement_SageX3_TEST_1;
        public static readonly E_Environnement_Maximo m_e_Environnement_Maximo = E_Environnement_Maximo.e_Environnement_Maximo_PROD;
        public static readonly E_URLSource m_e_URLSource = E_URLSource.e_URLSource_Online;
        private static readonly tBool m_b_URLWithoutDateTime = false;
        private const tChar m_pui8_GoLiveDateTime = "2024-07-01T00:00:00";

        public static DateTime m_c_DateTime;
        public static tChar m_pui8_lastUpdateTime = "";

        // URL settings
        private static tChar m_pui8_purchaseRequestHeader_Prefix;
        private static tChar m_pui8_purchaseRequestHeader_Suffix;
        private static tChar m_pui8_purchaseRequestLine_Prefix;
        private static tChar m_pui8_purchaseRequestLine_Suffix;
        public static tChar m_pui8_purchaseRequestHeader;
        public static tChar m_pui8_purchaseRequestLine;

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov solutions and services
        @Creation:  24.03.2024
        ----------------------------------------------------------------
        @Function Description: Get the APIIPAddres
        ----------------------------------------------------------------
        @parameter: -
        @Returnvalue: return the IPAddress
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        private static tChar GetAPIIPAddress()
        {
            tChar hostName = Dns.GetHostName();
            tChar pui8_IPAddressAPI = Dns.GetHostEntry(hostName).AddressList[0].ToString();
            return pui8_IPAddressAPI;
        }


        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov
        @Creation:  25.07.2023
        ----------------------------------------------------------------
        @Function Description:  The function initialize the URLs
        ----------------------------------------------------------------
        @parameter: bool URLWithoutDateTime, bool b_FileAlreadyExist
        @Returnvalue:   true -- Init went successfull
                        false -- erron during the initialization
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        private static void v_InitAll(bool URLWithoutDateTime, bool b_FileAlreadyExists)
        {

            if (!URLWithoutDateTime)
            {
                if (b_FileAlreadyExists)
                {
                    m_pui8_lastUpdateTime = Utilities.pui8_ReadDateTimeFromJSONFile(m_pui8_LastTransferDateTimeFile);
                }
                else
                {
                    m_pui8_lastUpdateTime = m_pui8_GoLiveDateTime;
                    Utilities.v_WriteDateTimeInJSONFile(DateTime.Parse(m_pui8_lastUpdateTime), m_pui8_LastTransferDateTimeFile);
                }

                TimeSpan c_LastUpdateDays = m_c_DateTime.Subtract(DateTime.Parse(m_pui8_lastUpdateTime));

                if (c_LastUpdateDays.TotalDays > m_ui8_NumDaysAllowedWihtoutTransDTUpd)
                {
                    Utilities.v_WriteDateTimeInJSONFile(m_c_DateTime, m_pui8_LastTransferDateTimeFile);
                    m_pui8_lastUpdateTime = Utilities.pui8_ReadDateTimeFromJSONFile(m_pui8_LastTransferDateTimeFile);
                    File.Delete(m_pui8_PurchaseRequestListLocation);
                }
            }

            m_pui8_purchaseRequestHeader = m_pui8_purchaseRequestHeader_Prefix + m_pui8_lastUpdateTime + m_pui8_purchaseRequestHeader_Suffix;
            m_pui8_purchaseRequestLine = m_pui8_purchaseRequestLine_Prefix + m_pui8_lastUpdateTime + m_pui8_purchaseRequestLine_Suffix;

            try
            {
                if (File.Exists(m_pui8_LogFilePath))
                {
                    Utilities.v_PrintMessage("Main Thread: PurchaseRequestHeader URL = " + m_pui8_purchaseRequestHeader, false);
                    Utilities.v_PrintMessage("Main Thread: PurchaseRequestLine URL = " + m_pui8_purchaseRequestLine, false);
                }
            }
            catch (Exception ex)
            {
                Utilities.v_PrintMessage("Main Thread: An exception occurred while writing in the file: " + m_pui8_LogFilePath, false, E_LOGLEVEL.e_LogLevel_Error);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex);
                Console.ResetColor();
            }

        }

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov solutions and services
        @Creation:  25.07.2023
        ----------------------------------------------------------------
        @Function Description: Main-function
        ----------------------------------------------------------------
        @parameter: -
        @Returnvalue:   -
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        private static void Main()
        {
            tBool b_TransferOK;
            // Create the log file 
            m_c_DateTime = DateTime.Now.ToUniversalTime();
            DateTime c_DateTimeTransferStart = m_c_DateTime;
            Utilities.v_CreateFolder(m_pui8_LogFileFolder);
            Utilities.b_CreateTheLogFile(m_pui8_LogFileFolder, m_c_DateTime, ref m_pui8_LogFilePath, m_e_Environnement_SageX3);

            Console.ForegroundColor = ConsoleColor.Green;
            Utilities.v_PrintMessage("\tSTART MAXIMO CONNECT");
            Console.ResetColor();

            Utilities.b_DeleteFiles(c_DateTimeTransferStart, m_pui8_LogFileFolder);
            tChar pui8_MaximoIPAddress = (E_Environnement_Maximo.e_Environnement_Maximo_TEST == m_e_Environnement_Maximo) ? "192.168.15.236:862" : "192.168.20.236:863";

            m_pui8_purchaseRequestHeader_Prefix = @"https://" + pui8_MaximoIPAddress + "/maxrest/rest/mbo/pr?";
            m_pui8_purchaseRequestHeader_Suffix = @"_includecols=prnum,description,currencycode,pr1,status,issuedate,requireddate,requestedby,vendor&_lid=sage&_lpwd=portdedouala";
            m_pui8_purchaseRequestLine_Prefix = @"https://" + pui8_MaximoIPAddress + "/maxrest/rest/mbo/prline?";
            m_pui8_purchaseRequestLine_Suffix = @"_includecols=prnum,prlinenum,itemnum,linetype,orderqty,unitcost,storeloc,fcprojectid,prlaln2&_lid=sage&_lpwd=portdedouala";

            if (!m_b_URLWithoutDateTime)
            {
                m_pui8_purchaseRequestHeader_Prefix = Utilities.pui8_Concat(m_pui8_purchaseRequestHeader_Prefix, @"status=~eq~OKDIR&statusdate=~gteq~");
                m_pui8_purchaseRequestHeader_Suffix = Utilities.pui8_Concat(@"&", m_pui8_purchaseRequestHeader_Suffix);
                m_pui8_purchaseRequestLine_Prefix = Utilities.pui8_Concat(m_pui8_purchaseRequestLine_Prefix, @"pr.status=~eq~OKDIR&pr.statusdate=~gteq~");
                m_pui8_purchaseRequestLine_Suffix = Utilities.pui8_Concat(@"&", m_pui8_purchaseRequestLine_Suffix);
            }

            bool b_FileAlreadyExists = !Utilities.b_CreateNewFile(m_pui8_LastTransferDateTimeFile, "");
            v_InitAll(m_b_URLWithoutDateTime, b_FileAlreadyExists);

            Sagex3Manager c_Sagex3Manager = new Sagex3Manager();


            if (E_Environnement_SageX3.e_Environnement_SageX3_PROD == m_e_Environnement_SageX3)
            {
                m_pui8_IPAddressSageX3 = "192.168.20.96";
            }
            else if (E_Environnement_SageX3.e_Environnement_SageX3_TEST_1 == m_e_Environnement_SageX3)
            {
                m_pui8_IPAddressSageX3 = "192.168.20.106";
            }
            else if (E_Environnement_SageX3.e_Environnement_SageX3_TEST_0 == m_e_Environnement_SageX3)
            {
                m_pui8_IPAddressSageX3 = GetAPIIPAddress();
            }
            else
            {
                m_pui8_IPAddressSageX3 = GetAPIIPAddress();
            }

            //Get the IP Address of API

            try
            {
                m_pui8_IPAddressHostSystem = GetAPIIPAddress();
            }
            catch (Exception e)
            {
                Utilities.v_PrintMessage("Main Thread: ERROR to get the API IPAddress " + e.Message);
            }

            /*
            the API-Host and the SageX3-Host have the same IPadress then we will write the localhost-IPaddress 127.0.0.1 in the SageX3IPAddress_ConfigFile.txt file
            the API-Host and the SageX3-Host have different IPaddress then we will write the predefined SageX3-Host IPaddress
            */
            using (FileStream fs = File.Create(m_pui8_ConfigFilePath))
            {
                if (m_pui8_IPAddressSageX3.Equals(m_pui8_IPAddressHostSystem))
                {
                    Byte[] title = new UTF8Encoding(true).GetBytes(m_pui8_IPAddressLocalHost);
                    fs.Write(title, 0, title.Length); // Add localhost-IPaddress in SageX3IPAddress_ConfigFile.txt file   
                }
                else
                {
                    Byte[] title = new UTF8Encoding(true).GetBytes(m_pui8_IPAddressSageX3);
                    fs.Write(title, 0, title.Length); // Add Sagex3-IPaddress in SageX3IPAddress_ConfigFile.txt file   
                }
            }

            //Read the SageX3 IPaddress from the SageX3IPAddress_ConfigFile.txt file
            var textReader = new StreamReader(File.OpenRead(m_pui8_ConfigFilePath), Encoding.Default, true);
            tChar pui8_SageX3IPAddress = textReader.ReadLine().ToString();

            if (!string.IsNullOrWhiteSpace(pui8_SageX3IPAddress))
            {
                c_Sagex3Manager.v_SendPurchaseRequestToSageX3(pui8_SageX3IPAddress, m_c_DateTime);
                b_TransferOK = false;

                if (c_Sagex3Manager.c_GetMaximoManager().b_TransferOK())
                {
                    if (c_Sagex3Manager.b_TransferOK())
                    {
                        b_TransferOK = true;
                    }
                    else
                    {
                        Utilities.v_PrintMessage("Main Thread: SageX3 transfer NOK");
                    }
                }
                else
                {
                    Utilities.v_PrintMessage("Main Thread: Maximo transfer NOK");
                }


                if (b_TransferOK)
                {
                    Utilities.v_WriteDateTimeInJSONFile(c_DateTimeTransferStart, m_pui8_LastTransferDateTimeFile);
                    Utilities.v_PrintMessage("Main Thread: Transfer Datetime updated " + c_DateTimeTransferStart.ToString("yyyy-MM-dd HH:mm:ss"));
                }
            }
            else
            {
                Utilities.v_PrintMessage("Main Thread: ERROR: Not possible to get SageX3 IPAddress");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Utilities.v_PrintMessage("\tPROGRAMM END ");
            Console.ResetColor();

            if (c_Sagex3Manager.b_GetSendNotification())
            {
                try
                {
                    Utilities.v_SendErrorNotification(Program.m_pui8_EmailMAximoconnect, Program.m_pui8_EmailAdmin, Program.m_pui8_LogFilePath);
                    //Utilities.v_SendErrorNotification(Program.m_pui8_EmailMAximoconnect, Program.m_pui8_EmailAdmin_2, Program.m_pui8_LogFilePath);
                }
                catch(Exception )
                {
                   
                }
            }
            Thread.Sleep(m_ui16_TerminalWaitTime);
        }


    }
}
