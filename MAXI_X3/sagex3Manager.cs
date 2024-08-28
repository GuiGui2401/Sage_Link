using MAXI_X3.WebReference;
using SCDx3_v1._1;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using tBool = System.Boolean;
using tChar = System.String;
using tUInt16 = System.UInt16;

namespace MAXI_X3
{
    internal class Sagex3Manager
    {
        // Logging file configuration

        private tBool m_b_TransferOK;
        private readonly MaximoManager m_MaximoManager;
        //Json File to savethe last transaction timestamp

        private const UInt16 ui8_MaxAttemptToSendPR = 6;
        /*private const tChar t = "<PARAM><GRP ID=\"PSH0_1\"> <FLD NAME = \"REQUSR\" TYPE=\"Char\">P1434</FLD>" +
            "<FLD NAME = \"PSHFCY\" TYPE=\"Char\">MGD</FLD><FLD NAME = \"PSHNUM\" TYPE=\"Char\"></FLD>" +
            "<FLD NAME = \"PRQDAT\" TYPE=\"Date\">20240719</FLD><FLD NAME = \"ZSUPPLIER\" TYPE=\"Char\">" +
            "</FLD><FLD NAME = \"ZREFDA\" TYPE=\"Char\">DA-/23/039</FLD></GRP><GRP ID = \"PSH1_3\" >" +
            "< FLD NAME=\"ZNATURE\" TYPE=\"Char\"></FLD><FLD NAME = \"ZTACHE\" TYPE=\"Char\"></FLD>" +
            "<FLD NAME = \"ZMEMO\" TYPE=\"Char\">Unknown</FLD></GRP><TAB DIM = \"200\" ID=\"PSH1_1\" " +
            "SIZE=\"1\"><LIN NUM = \"1\" >< FLD NAME=\"ITMREF\" TYPE=\"Char\">FPIEME-F090</FLD>" +
            "<FLD NAME = \"PRHFCY\" TYPE=\"Char\">MGD</FLD><FLD NAME = \"EXTRCPDAT\" TYPE=\"Date\">20240719</FLD>" +
            "<FLD NAME = \"QTYPUU\" TYPE=\"Decimal\">1.0</FLD><FLD NAME = \"QTYSTU\" TYPE=\"Decimal\">1.0</FLD>" +
            "<FLD NAME = \"BPSNUM\" TYPE=\"Char\"></FLD><FLD NAME = \"NETPRI\" TYPE=\"Decimal\">0.0</FLD>" +
            "<FLD NAME = \"LINACC1\" TYPE=\"Char\">6020000000</FLD><FLD NAME = \"LINACC2\" TYPE=\"Char\"></FLD><FLD NAME = \"CCE1\" TYPE=\"Char\"></FLD></LIN></TAB></PARAM>";
        private readonly tChar c = "ZREFDA";
        private readonly tChar typename = "TYPE=";
        private readonly tChar type = "Char>";*/
        private readonly FileInfo fInfo = new FileInfo(Program.m_pui8_PurchaseRequestListLocation);
        private tBool m_b_SendNotification;

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov solutions and services
        @Creation:  25.07.2023
        ----------------------------------------------------------------
        @Function Description: constructor
        ----------------------------------------------------------------
        @parameter: -
        @Returnvalue: -
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        public Sagex3Manager()
        {
            m_MaximoManager = new MaximoManager();
            m_b_TransferOK = true;
            m_b_SendNotification = false;
        }

        public tBool b_GetSendNotification()
        {
            return (m_b_SendNotification || m_MaximoManager.b_GetSendNotification());
        }

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov solutions and services
        @Creation:  25.07.2023
        ----------------------------------------------------------------
        @Function Description: return the MaximoManager object
        ----------------------------------------------------------------
        @parameter:-
        @Returnvalue: -
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        public MaximoManager c_GetMaximoManager()
        {
            return m_MaximoManager;
        }

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov solutions and services
        @Creation:  25.07.2023
        ----------------------------------------------------------------
        @Function Description: the function try the connecion to SageX3
        pool SEEDPOOL and webservice YOPSHM1. If the connection is not 
        allow the API is not able to send PR to sageX3
        ----------------------------------------------------------------
        @parameter: tChar s_sageX3IPAdress, DateTime c_Datetime
        @Returnvalue: -
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        public void v_SendPurchaseRequestToSageX3(tChar s_sageX3IPAdress, DateTime c_Datetime)
        {
            Utilities.v_PrintMessage("SageX3Mng: send purchase request to Sage X3");
            tBool b_PurchaseRequestIsLoaded = false;
            tChar c_WebServicePublicName_PR = pui8_GetWebServicesPublicName(Program.m_e_TransactionType);

            try
            {
                // Get the purchase request from maximo
                List<tChar> list_PurchaseRequest = c_GetMaximoManager().lpui8_GetPurchaseRequestFromMAXI(ref b_PurchaseRequestIsLoaded, c_Datetime);
                tUInt16 ui16_PurchaseRequestLoaded = (tUInt16)list_PurchaseRequest.Count;
                tUInt16 ui16_PurchaseRequestAlreadySent = 0;

                // Copy the list of already saved PR
                List<SageX3Reference> list_CopyPurchaseRequest = new List<SageX3Reference>();

                if (b_PurchaseRequestIsLoaded && (0 < ui16_PurchaseRequestLoaded))
                {
                    Utilities.v_PrintMessage("SageX3Mng: " + ui16_PurchaseRequestLoaded.ToString() + " purchase request loaded");
                    UInt16 ui16_CounterSentPR = 0;

                    //Get the server response
                    try
                    {
                        // start connexion to sageX3
                        Tuple<tBool, CAdxWebServiceXmlCCServiceBasic> tc_X3Connexion = t_X3Connexion(c_WebServicePublicName_PR, s_sageX3IPAdress);

                        //Checking server connexion
                        if (tc_X3Connexion.Item1)
                        {
                            //Set bill to X3 using web service obj
                            CAdxWebServiceXmlCCServiceBasic sageX3Field = tc_X3Connexion.Item2;
                            List<SageX3Reference> list_PurchaseRequestAlreadySent = new List<SageX3Reference>();
                            Utilities.v_ReadPurchaseRequestFromJSONFile(Program.m_pui8_PurchaseRequestListLocation, ref list_CopyPurchaseRequest);

                            if (null != list_CopyPurchaseRequest)
                            {
                                list_PurchaseRequestAlreadySent = list_CopyPurchaseRequest;
                            }

                            foreach (tChar s_CurrentPurchaseRequest in list_PurchaseRequest)
                            {
                                tChar pui8_ErrorMessage = "";
                                // Read the PRNum from the current PurchaseRequest
                                tChar pui8_PRNumber = pui8_ExtractFromString(s_CurrentPurchaseRequest, "Type", "Char", "ZREFDA");
                                tChar pui8_Vendor = pui8_ExtractFromString(s_CurrentPurchaseRequest, "Type", "Char", "ZSUPPLIER");
                                tChar pui8_IssueDate = pui8_ExtractFromString(s_CurrentPurchaseRequest, "Type", "Char", "EXTRCPDAT");
                                tChar pui8_RequiredDate = pui8_ExtractFromString(s_CurrentPurchaseRequest, "Type", "Char", "PRQDAT");
                                tChar pui8_RequestedBy = pui8_ExtractFromString(s_CurrentPurchaseRequest, "Type", "Char", "REQUSR");

                                //When the PR is already sent, we don't need to send it again
                                if (!Utilities.b_PRIsAlreadySaved(list_PurchaseRequestAlreadySent, pui8_PRNumber, sageX3Field))
                                {
                                    UInt16 ui8_SendCounter = 0;
                                    tBool b_SendSuccessful = false;
                                    tChar pui8_SageX3Ref = "";

                                    // try to send the PR 6 times
                                    while (ui8_MaxAttemptToSendPR > ui8_SendCounter)
                                    {
                                        if (!b_SendSuccessful)
                                        {
                                            CAdxResultXml c_CAdxResultXml = sageX3Field.save(c_GetCallContext(), c_WebServicePublicName_PR, s_CurrentPurchaseRequest);
                                            b_SendSuccessful = (c_CAdxResultXml.status == 1);

                                            foreach (CAdxMessage c_CAdxMessage in c_CAdxResultXml.messages)
                                            {
                                                if (!pui8_ErrorMessage.Contains(c_CAdxMessage.message))
                                                {
                                                    pui8_ErrorMessage += c_CAdxMessage.message + " ";
                                                }
                                            }

                                            if(b_SendSuccessful)
                                            {
                                                pui8_SageX3Ref = pui8_ExtractFromString(c_CAdxResultXml.resultXml, "Char", "TYPE", "PSHNUM");
                                            }
                                            
                                        }
                                        ui8_SendCounter++;

                                        if (b_SendSuccessful)
                                        {
                                            list_PurchaseRequestAlreadySent.Add(new SageX3Reference
                                            {
                                                PSHNUM = pui8_SageX3Ref,
                                                ZREFDA = pui8_PRNumber
                                            }) ;
                                            ui16_CounterSentPR++;
                                            break;
                                        }
                                    }
                                    Utilities.v_PrintMessage("SageX3Mng: Save PR= " + pui8_PRNumber + ", Status: " + Convert.ToInt32(b_SendSuccessful) + " Attempt: " + ui8_SendCounter);

                                    if (!b_SendSuccessful)
                                    {
                                        Utilities.v_PrintMessage("SageX3Mng: PR=" + pui8_PRNumber + " not send, " + pui8_ErrorMessage, PrintConsole: true, e_logLevel: E_LOGLEVEL.e_LogLevel_Error);
                                    }
                                }
                                else
                                {
                                    ui16_PurchaseRequestAlreadySent++;
                                    Utilities.v_PrintMessage("SageX3Mng: PurchaseRequest " + pui8_PRNumber + " already sent", PrintConsole: true);
                                }

                            }

                            // Write in the JSON file, which PR was sent
                            fInfo.IsReadOnly = false;
                            fInfo.Attributes = FileAttributes.Normal;
                            Utilities.v_WriteInJSONFile(list_PurchaseRequestAlreadySent, Program.m_pui8_PurchaseRequestListLocation);
                            fInfo.IsReadOnly = true;
                            fInfo.Attributes = FileAttributes.Hidden;

                            if ((ui16_PurchaseRequestLoaded != 0) && ((ui16_CounterSentPR + ui16_PurchaseRequestAlreadySent) == 0))
                            {
                                m_b_TransferOK = false;
                                m_b_SendNotification = true;
                                Utilities.v_PrintMessage("SageX3Mng: " + ui16_PurchaseRequestLoaded + " purchase request could not be sent --> TR not OK", e_logLevel: E_LOGLEVEL.e_LogLevel_Error);
                            }
                            else if (ui16_PurchaseRequestLoaded != (ui16_CounterSentPR + ui16_PurchaseRequestAlreadySent))
                            {
                                m_b_SendNotification = true;
                                Utilities.v_PrintMessage("SageX3Mng: " + (ui16_PurchaseRequestLoaded - (ui16_CounterSentPR + ui16_PurchaseRequestAlreadySent)) + " purchase request not sent", e_logLevel: E_LOGLEVEL.e_LogLevel_Error);
                            }

                            Utilities.v_PrintMessage("SageX3Mng: " + ui16_CounterSentPR.ToString() + " purchase request of " + ui16_PurchaseRequestLoaded.ToString() + " sent to SageX3.");
                            Utilities.v_PrintMessage("SageX3Mng: " + ui16_PurchaseRequestAlreadySent.ToString() + " purchase request already sent to SageX3.");

                        }
                        else
                        {
                            Utilities.v_PrintMessage("SageX3Mng: CONNECTION TO SAGEX3 FAILED", PrintConsole: true, e_logLevel: E_LOGLEVEL.e_LogLevel_Error);
                        }
                    }
                    catch (Exception e)
                    {
                        Utilities.v_PrintMessage("SageX3Mng: An Exception occurs during connection to SageX3", PrintConsole: true, e_logLevel: E_LOGLEVEL.e_LogLevel_Error);
                        Utilities.v_PrintMessage("SageX3Mng: " + e.Message, PrintConsole: true, e_logLevel: E_LOGLEVEL.e_LogLevel_Error);
                        m_b_TransferOK = false;
                        m_b_SendNotification = true;
                    }
                }
                else
                {
                    Utilities.v_PrintMessage("SageX3Mng: PurchaseRequest list is empty", PrintConsole: true, e_logLevel: E_LOGLEVEL.e_LogLevel_Warning);
                }
            }
            catch (Exception ex)
            {
                m_b_TransferOK = false;
                m_b_SendNotification = true;
                Utilities.v_PrintMessage("SageX3Mng: An Exception occurs during the attempt to send a PurchaseRequest to SageX3", PrintConsole: true, e_logLevel: E_LOGLEVEL.e_LogLevel_Error);
                Utilities.v_PrintMessage("SageX3Mng: " + ex.Message, PrintConsole: true, e_logLevel: E_LOGLEVEL.e_LogLevel_Error);
            }

            if (m_b_TransferOK)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Utilities.v_PrintMessage("SageX3Mng: PR Transfer Maximo <==> SageX3 SUCCESSFUL");
                Console.ResetColor();
            }
            
        }

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov solutions and services
        @Creation:  25.07.2023
        ----------------------------------------------------------------
        @Function Description:  return the CAdxCallContext object for
                                the SAGEX3 connection
        ----------------------------------------------------------------
        @parameter: --
        @Returnvalue:--
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        private CAdxCallContext c_GetCallContext()
        {
            CAdxCallContext c_CAdxCallContext = new CAdxCallContext()
            {
                codeLang = "FRA",
                poolAlias = "SEEDPOOL",
                requestConfig = "adxwss.optreturn=XML"
            };
            return c_CAdxCallContext;
        }

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov solutions and services
        @Creation:  25.07.2023
        ----------------------------------------------------------------
        @Function Description:  start the connection to a given webservice 
        located in sageX3 
        ----------------------------------------------------------------
        @parameter: tChar publicNameWebService, tChar sageIPAdress
        @Returnvalue:   True  - Connection to SageX3 established
                        False - Connection to SageX3 failed
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        private Tuple<tBool, CAdxWebServiceXmlCCServiceBasic> t_X3Connexion(tChar publicNameWebService, tChar sageIPAdress)
        {
            CAdxWebServiceXmlCCServiceBasic x3WebService = new CAdxWebServiceXmlCCServiceBasic();
            Tuple<tBool, CAdxWebServiceXmlCCServiceBasic> sageX3Connection;
            tBool b_Authenticated = false;
            Utilities.v_PrintMessage("SageX3Mng: SAGEX3 Url: http://" + sageIPAdress + ":8124/soap-generic/syracuse/collaboration/syracuse/CAdxWebServiceXmlCC?wsdl");

            try
            {
                Utilities.v_PrintMessage("SageX3Mng: Start connection to SageX3");
                CAdxResultXml c_CAdxResultXml = new CAdxResultXml();
                CAdxParamKeyValue[] objectKeys = new CAdxParamKeyValue[1];
                x3WebService.Url = @"http://" + sageIPAdress + ":8124/soap-generic/syracuse/collaboration/syracuse/CAdxWebServiceXmlCC?wsdl";
                x3WebService.BasicAuth = true;

                Tuple<tChar, tChar> SageX3Credentials = t_GetCredentials(Program.m_e_Environnement_SageX3);
                tChar sageX3UserName = SageX3Credentials.Item1;
                tChar sageX3Password = SageX3Credentials.Item2;

                x3WebService.Credentials = new NetworkCredential(sageX3UserName, sageX3Password);
                x3WebService.PreAuthenticate = true;
                x3WebService.Timeout = 50000;
                // start the connection to SAGEX3 pool SEEDPOOL and webservice YOPSHM1

                c_CAdxResultXml = x3WebService.getDescription(c_GetCallContext(), publicNameWebService);

                sageX3Connection = Tuple.Create((c_CAdxResultXml.status == 1), x3WebService);

                foreach (CAdxMessage item in c_CAdxResultXml.messages)
                {
                    b_Authenticated = (c_CAdxResultXml.status != 0);
                    tChar pui8_Status = (b_Authenticated == false) ? "failed" : "success";
                    Utilities.v_PrintMessage("SageX3Mng: Connection STATUS  " + pui8_Status);
                    Utilities.v_PrintMessage("SageX3Mng: " + item.message);
                }
            }
            catch (Exception ex)
            {
                x3WebService = null;
                sageX3Connection = Tuple.Create(false, x3WebService);
                tChar pui8_AuthenticationResult = (b_Authenticated == true) ? "success" : "failed";
                Utilities.v_PrintMessage("SageX3Mng: An Exception occurs during the connection to SageX3", PrintConsole: true, e_logLevel: E_LOGLEVEL.e_LogLevel_Error);
                Utilities.v_PrintMessage("SageX3Mng: Credentials authentication: " + pui8_AuthenticationResult, PrintConsole: true, e_logLevel: E_LOGLEVEL.e_LogLevel_Error);
                Utilities.v_PrintMessage("SageX3Mng: " + ex.Message, PrintConsole: true, e_logLevel: E_LOGLEVEL.e_LogLevel_Error);
                m_b_TransferOK = false;
                return sageX3Connection;
            }
            return sageX3Connection;
        }


        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov solutions and services
        @Creation:  08.03.2024
        ----------------------------------------------------------------
        @Function Description:  Get if the article went well.
        ----------------------------------------------------------------
        @parameter: -
        @Returnvalue:   True --- Everything went well
                        False --- An error occured during the transfer
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        public tBool b_TransferOK()
        {
            return m_b_TransferOK;
        }

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov solutions and services
        @Creation:  08.03.2024
        ----------------------------------------------------------------
        @Function Description:  Get the webservice publicname depends on transaction.
        currently we only have 2 type of transaction DDLM and STD
        ----------------------------------------------------------------
        @parameter: E_TransactionType e_TransactionType
        @Returnvalue:   e_TransactionType == e_Transaction_DDLM --> YOPSHM2
                        e_TransactionType == e_Transaction_STD --> YOPSHM1
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        private tChar pui8_GetWebServicesPublicName(E_TransactionType e_TransactionType)
        {
            tChar pui8_WebServicePublicName = "";
            if (E_TransactionType.e_Transaction_DDLM == e_TransactionType)
            {
                pui8_WebServicePublicName = "YOPSHM2";
            }
            else if (E_TransactionType.e_Transaction_STD == e_TransactionType)
            {
                pui8_WebServicePublicName = "YOPSHM1";
            }
            else
            {
                Utilities.v_PrintMessage("SageX3Mng: Transaction type not defined", PrintConsole: false, e_logLevel: E_LOGLEVEL.e_LogLevel_Error);
            }
            return pui8_WebServicePublicName;
        }

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov solutions and services
        @Creation:  24.03.2024
        ----------------------------------------------------------------
        @Function Description:  Get the SageX3 credentials
        ----------------------------------------------------------------
        @parameter: E_Environnement e_Environnement
        @Returnvalue: Tuple<Username, Password>
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        private Tuple<tChar, tChar> t_GetCredentials(E_Environnement_SageX3 e_Environnement)
        {
            Tuple<tChar, tChar> t_RetVal = ((e_Environnement == E_Environnement_SageX3.e_Environnement_SageX3_PROD) || (e_Environnement == E_Environnement_SageX3.e_Environnement_SageX3_TEST_1)) ? Tuple.Create("gmao", "gmao") : Tuple.Create("GMAO", "GMAO");
            return t_RetVal;
        }

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov solutions and services
        @Creation:  24.03.2024
        ----------------------------------------------------------------
        @Function Description:  
        ----------------------------------------------------------------
        @parameter: tChar pui8_InputString, tChar pui8_Typename, tChar pui8_Type, tChar pui8_FieldToExtract
        @Returnvalue: 
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        private tChar pui8_ExtractFromString(tChar pui8_InputString, tChar pui8_Typename, tChar pui8_Type, tChar pui8_FieldToExtract)
        {
            tChar pui8_RetVal = "";
            tChar pui8_WithoutSpecialCharacter = pui8_InputString.Replace("\"", "").Replace(" ", "");
            tUInt16 ui16_StartIndexFieldToExtract = (tUInt16)pui8_WithoutSpecialCharacter.IndexOf(pui8_FieldToExtract.ToUpper());
            pui8_Type += '>';
            pui8_Typename += '=';
            tUInt16 ui16_StartPosition = (tUInt16)(ui16_StartIndexFieldToExtract + pui8_FieldToExtract.Length + pui8_Type.Length + pui8_Typename.Length);
            tUInt16 ui16_EndPosition = (tUInt16)(pui8_WithoutSpecialCharacter.IndexOf('<', ui16_StartIndexFieldToExtract));

            try
            {
                pui8_RetVal = pui8_WithoutSpecialCharacter.Substring((ui16_StartPosition), (ui16_EndPosition - ui16_StartPosition));
            }
            catch (Exception)
            {

            }
            return pui8_RetVal;
        }

    }
}
