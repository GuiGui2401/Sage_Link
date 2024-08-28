using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Xml;
using tBool = System.Boolean;
using tChar = System.String;
using tUInt32 = System.UInt32;
using tUInt8 = System.Byte;

namespace MAXI_X3
{
    internal class MaximoManager
    {
        private tBool m_b_TransferOK;
        private tBool m_b_SendNotification;

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov solutions and service
        @Creation:  25.07.2023
        ----------------------------------------------------------------
        @Function Description:  constructor
        ----------------------------------------------------------------
        @parameter: tChar pui8_LastTransferDateTimeFile, tBool URLWithoutDateTime
                                , E_TransactionType e_TransactionType
                                , E_Environnement e_Environnement
                                , E_URLSource e_URLSource
        @Returnvalue: MaximoManager object created
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        public MaximoManager()
        {
            m_b_TransferOK = true;
            m_b_SendNotification = false;
        }

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov solutions and services
        @Creation: 21.04.2024
        ----------------------------------------------------------------
        @Function Description:  create a new folder
        ----------------------------------------------------------------
        @parameter: tChar pui8_FolderPath
        @Returnvalue:   -
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        public void v_CreateFolder(tChar pui8_FolderPath)
        {
            if (!Directory.Exists(pui8_FolderPath))
            {
                Directory.CreateDirectory(pui8_FolderPath);
            }
        }

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov
        @Creation:  25.07.2023
        ----------------------------------------------------------------
        @Function Description: Get Purchase request from Maximo
        When the number of Purchase request is greater than 0 the the given
        tBool variable is set to true
        ----------------------------------------------------------------
        @parameter: ref tBool b_PurchaseRequestIsLoaded
        @Returnvalue: A List of purchase request
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        public List<tChar> lpui8_GetPurchaseRequestFromMAXI(ref tBool b_PurchaseRequestIsLoaded, DateTime c_Datetime)
        {
            b_PurchaseRequestIsLoaded = false;

            tUInt32 ui32_NumOfPurchaseRequest = 0;
            List<tChar> list_PurchaseRequest = new List<tChar>();
            HttpClient c_HttpClientPRHeader = new HttpClient();
            HttpClient c_HttpClientPRLine = new HttpClient();
            tChar pui8_TodayDate = c_Datetime.ToString("yyyy-MM-dd");

            // Add an Accept header for JSON format.
            c_HttpClientPRHeader.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
            c_HttpClientPRLine.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

            Tuple<tBool, XmlDocument> t_OjectPRLineRead = new Tuple<tBool, XmlDocument>(true, null);
            Tuple<tBool, XmlDocument> t_OjectPRHeaderRead = new Tuple<tBool, XmlDocument>(true, null);

            if (E_URLSource.e_URLSource_Offline == Program.m_e_URLSource)
            {
                t_OjectPRLineRead = t_ReadXmldocument(Program.m_pui8_DirectoryPath + "/Test_ConfigFiles/PurchaseRequestLine_Maximo.xml");
                t_OjectPRHeaderRead = t_ReadXmldocument(Program.m_pui8_DirectoryPath + "/Test_ConfigFiles/PurchaseRequestHeader_Maximo.xml");
            }
            else
            {
                v_GetServerCertificat();

                /* start connection to PRHeader and Line
                Program will wait here until a response is received or a timeout occurs 
                */
                Tuple<HttpResponseMessage, HttpResponseMessage> t_ConnectionWithPRUrl = t_StartConnectionWithPRURL(c_HttpClientPRHeader, c_HttpClientPRLine);
                HttpResponseMessage c_HTTPResponseMsgHeader = t_ConnectionWithPRUrl.Item1;
                HttpResponseMessage c_HTTPResponseMsgLine = t_ConnectionWithPRUrl.Item2;

                if (c_HTTPResponseMsgHeader != null && c_HTTPResponseMsgLine != null)
                {
                    t_OjectPRHeaderRead = t_GetContentURLPR(c_HTTPResponseMsgHeader);
                    t_OjectPRLineRead = t_GetContentURLPR(c_HTTPResponseMsgLine);
                }
                else
                {
                    if (c_HTTPResponseMsgHeader == null)
                    {
                        Utilities.v_PrintMessage("MaximoMng: HTTP response message header failed", true, E_LOGLEVEL.e_LogLevel_Error);
                    }
                    if (c_HTTPResponseMsgLine == null)
                    {
                        Utilities.v_PrintMessage("MaximoMng: HTTP response message line failed", true, E_LOGLEVEL.e_LogLevel_Error);
                    }
                }
            }

            if (t_OjectPRHeaderRead.Item1 && t_OjectPRLineRead.Item1)
            {
                XmlDocument c_XmlDocumentPRHeader = t_OjectPRHeaderRead.Item2;
                XmlDocument c_XmlDocumentPRLine = t_OjectPRLineRead.Item2;

                try
                {
                    if (null != c_XmlDocumentPRHeader)
                    {
                        foreach (XmlNode c_XmlNodePurchaseRequestHeader in c_XmlDocumentPRHeader["PRMboSet"])
                        {

                            tChar pui8_XmlPurchaseRequestLine = "";
                            tChar pui8_ZSUPPLIER = "";
                            tChar pui8_CCE1 = "";
                            tChar pui8_LINACC2 = "";
                            tChar pui8_ZPROG = "";
                            tChar pui8_ZOBJ = "";
                            ref tChar pui8_ZNATURE = ref pui8_LINACC2;
                            ref tChar pui8_ZTACHE = ref pui8_CCE1;
                            ref tChar pui8_ZMEMO = ref pui8_ZOBJ;
                            ref tChar pui8_BSPNUM = ref pui8_ZSUPPLIER;
                            tUInt32 ui32_CounterLine = 1;
                            tUInt32 ui32_SizePurchaseRequest = 0;
                            tChar pui8_MissedAttribut = "";
                            tBool b_AllowedToSendPurchaseRequest = true;
                            tBool b_PurchaseRequestHasALigne = false;
                            tChar m_pui8_OldBudgetLine = "";

                            /* set the Supplier. If don't exist send a error message 
                            and continue the programm */
                            if (c_XmlNodePurchaseRequestHeader["VENDOR"] != null)
                            {
                                pui8_ZSUPPLIER = c_XmlNodePurchaseRequestHeader["VENDOR"].InnerText.Trim();
                            }
                            else
                            {
                                if (pui8_MissedAttribut.Contains("VENDOR ") == false)
                                {
                                    pui8_MissedAttribut += "VENDOR ";
                                }
                                b_AllowedToSendPurchaseRequest = false;
                            }

                            try
                            {
                                foreach (XmlNode c_XmlNodePurchaseRequestLine in c_XmlDocumentPRLine["PRLINEMboSet"])
                                {
                                    tChar pui8_CurrentBudgetLine = "";
                                    b_PurchaseRequestHasALigne |= false;
                                    //Check if the line and the header are the same
                                    if (c_XmlNodePurchaseRequestLine["PRNUM"].InnerText.Trim().ToLower()
                                        == c_XmlNodePurchaseRequestHeader["PRNUM"].InnerText.Trim().ToLower())
                                    {
                                        b_PurchaseRequestHasALigne |= true;
                                        try
                                        {
                                            // when the attribut is not existing, block the sending of PR
                                            if (null != c_XmlNodePurchaseRequestLine["FCPROJECTID"])
                                            {
                                                // get the element once
                                                pui8_CurrentBudgetLine = c_XmlNodePurchaseRequestLine["FCPROJECTID"].InnerText.Trim();

                                                if (pui8_CurrentBudgetLine.Length >= 7)
                                                {
                                                    Tuple<tChar, tChar> t = t_TruncBudgetLine(pui8_CurrentBudgetLine);
                                                    tChar[] pui8_FCPROJECTID = { t.Item1, t.Item2 };             // To Remove when the isse ´with budget line is solved by imaxeam: tChar[] pui8_FCPROJECTID = pui8_CurrentBudgetLine.Split('-');

                                                    b_AllowedToSendPurchaseRequest = ExctractFromStringArray(pui8_FCPROJECTID, ref pui8_CCE1, ref pui8_LINACC2, ref pui8_MissedAttribut);
                                                }
                                                else
                                                {
                                                    b_AllowedToSendPurchaseRequest = false;
                                                    Utilities.v_PrintMessage("MaximoMng: Budget line length too short", false, E_LOGLEVEL.e_LogLevel_Error);
                                                }
                                            }
                                            else
                                            {
                                                b_AllowedToSendPurchaseRequest = false;
                                                Utilities.v_PrintMessage("MaximoMng: Informations regarding the budget are missing", false, E_LOGLEVEL.e_LogLevel_Error);
                                                if (pui8_MissedAttribut.Contains("CCE1") == false)
                                                {
                                                    pui8_MissedAttribut += "CCE1 ";
                                                }
                                                if (pui8_MissedAttribut.Contains("LINACC2") == false)
                                                {
                                                    pui8_MissedAttribut += "LINACC2 ";
                                                }
                                            }

                                        }
                                        catch (Exception ex)
                                        {
                                            m_b_TransferOK = false;
                                            if (pui8_MissedAttribut.Contains("CCE1") == false)
                                            {
                                                pui8_MissedAttribut += "CCE1 ";
                                            }
                                            if (pui8_MissedAttribut.Contains("LINACC2") == false)
                                            {
                                                pui8_MissedAttribut += "LINACC2 ";
                                            }
                                            Utilities.v_PrintMessage("MaximoMng: An Exception occurred while extracting FCPROJECTID", true, E_LOGLEVEL.e_LogLevel_Error);
                                            Utilities.v_PrintMessage("MaximoMng: " + ex.Message, true, E_LOGLEVEL.e_LogLevel_Error);
                                            b_AllowedToSendPurchaseRequest = false;
                                        }

                                        pui8_ZOBJ = (c_XmlNodePurchaseRequestHeader["DESCRIPTION"] != null) ? c_XmlNodePurchaseRequestHeader["DESCRIPTION"].InnerText.Trim() : "Unknown";

                                        // Fill the pruchase request line with data
                                        tChar pui8_ITMREF = c_XmlNodePurchaseRequestLine["ITEMNUM"].InnerText.Trim();

                                        // Check if the date is correct
                                        tChar pui8_EXTRCPDAT = "";
                                        tChar pui8_ReqDate = DateTime.Parse(c_XmlNodePurchaseRequestHeader["REQUIREDDATE"].InnerText.Trim()).ToString("yyyy-MM-dd");
                                        v_CheckDate(pui8_ReqDate, pui8_TodayDate, ref pui8_EXTRCPDAT);
                                        tChar pui8_QTYPUU = c_XmlNodePurchaseRequestLine["ORDERQTY"].InnerText.Trim();
                                        tChar pui8_NETPRI = c_XmlNodePurchaseRequestLine["UNITCOST"].InnerText.Trim();

                                        if ((m_pui8_OldBudgetLine == pui8_CurrentBudgetLine) || (m_pui8_OldBudgetLine == ""))
                                        {
                                            pui8_XmlPurchaseRequestLine += FillPurchaseRequestLine(ui32_CounterLine++, pui8_ITMREF, pui8_EXTRCPDAT
                                                                        , pui8_QTYPUU, pui8_BSPNUM, pui8_NETPRI, pui8_LINACC2
                                                                        , pui8_CCE1, ref pui8_MissedAttribut);
                                            m_pui8_OldBudgetLine = pui8_CurrentBudgetLine;
                                            ui32_SizePurchaseRequest++;
                                        }
                                        else
                                        {
                                            pui8_XmlPurchaseRequestLine += FillPurchaseRequestLine(ui32_CounterLine++, pui8_ITMREF, pui8_EXTRCPDAT
                                                                        , pui8_QTYPUU, pui8_BSPNUM, pui8_NETPRI, pui8_LINACC2
                                                                        , pui8_CCE1, ref pui8_MissedAttribut);
                                            ui32_SizePurchaseRequest++;
                                            b_AllowedToSendPurchaseRequest = false;
                                            Utilities.v_PrintMessage("MaximoMng: The lines in the purchase request " + c_XmlNodePurchaseRequestHeader["PRNUM"].InnerText.Trim() + " do not have the same information about the budget", true, E_LOGLEVEL.e_LogLevel_Error);
                                        }
                                    }

                                }
                                if (pui8_XmlPurchaseRequestLine.Length == 0)
                                {
                                    b_AllowedToSendPurchaseRequest = false;
                                }

                                //Fill the purchase request with data
                                tChar pui8_DateTime = DateTime.Parse(c_XmlNodePurchaseRequestHeader["ISSUEDATE"].InnerText.Trim()).ToString("yyyy-MM-dd");

                                tChar pui8_PRQDAT = "";
                                tChar pui8_ZISSUEDAT = "";

                                v_CheckDate(pui8_TodayDate, pui8_DateTime, ref pui8_PRQDAT);
                                v_CheckDate(pui8_TodayDate, pui8_DateTime, ref pui8_ZISSUEDAT);

                                tChar pui8_REQUSR = c_XmlNodePurchaseRequestHeader["REQUESTEDBY"].InnerText.Trim().Replace("-", "");
                                if (pui8_REQUSR.ToUpper().Replace(" ", "") == "MAXADMIN")
                                {
                                    pui8_REQUSR = "GMAO";
                                }
                                tChar pui8_ZREFDA = c_XmlNodePurchaseRequestHeader["PRNUM"].InnerText.Trim();

                                tChar pui8_XmlPurchaseRequest = FillPurchaseRequest(pui8_REQUSR, pui8_PRQDAT, pui8_ZISSUEDAT, pui8_ZSUPPLIER, pui8_ZREFDA
                                                                , pui8_ZNATURE, pui8_ZTACHE, pui8_ZPROG, pui8_ZMEMO
                                                                , ui32_SizePurchaseRequest, pui8_XmlPurchaseRequestLine);

                                ui32_NumOfPurchaseRequest++;

                                if (b_AllowedToSendPurchaseRequest && b_PurchaseRequestHasALigne)
                                {
                                    try
                                    {
                                        list_PurchaseRequest.Add(pui8_XmlPurchaseRequest);
                                        Utilities.v_PrintMessage("MaximoMng: A new PurchaseRequest PRNUM = "
                                            + c_XmlNodePurchaseRequestHeader["PRNUM"].InnerText.Trim() + " added, Total = "
                                            + list_PurchaseRequest.Count.ToString()
                                        );
                                        m_b_SendNotification |= false;
                                    }
                                    catch (Exception e)
                                    {
                                        Utilities.v_PrintMessage("MaximoMng: An Exception occurred while saving the PR to the list", true, E_LOGLEVEL.e_LogLevel_Error);
                                        Utilities.v_PrintMessage("MaximoMng: " + e.Message, true, E_LOGLEVEL.e_LogLevel_Error);
                                        m_b_TransferOK = false;
                                    }
                                }
                                else if (!b_PurchaseRequestHasALigne)
                                {
                                    Utilities.v_PrintMessage("MaximoMng: The PurchaseRequest PRNUM = "
                                    + c_XmlNodePurchaseRequestHeader["PRNUM"].InnerText.Trim() + " has no ligne"
                                    , true, E_LOGLEVEL.e_LogLevel_Warning);
                                    m_b_SendNotification = true;
                                }
                                else
                                {
                                    Utilities.v_PrintMessage("MaximoMng: Problem occured with purchase request, PRNUM = "
                                    + c_XmlNodePurchaseRequestHeader["PRNUM"].InnerText.Trim() + "\n"
                                    + pui8_FormatXMLText(pui8_XmlPurchaseRequest) + "\n", true, E_LOGLEVEL.e_LogLevel_Warning);
                                    m_b_SendNotification = true;
                                }
                            }
                            catch (Exception e)
                            {
                                Utilities.v_PrintMessage("MaximoMng: An Exception occurs The PurchaseRequestLine could not be fit into the list. An exception has occured", true, E_LOGLEVEL.e_LogLevel_Error);
                                Utilities.v_PrintMessage("MaximoMng: " + e.Message, true, E_LOGLEVEL.e_LogLevel_Error);
                                m_b_TransferOK = false;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Utilities.v_PrintMessage("MaximoMng: An Exception occurs PurchaseRequestHeader not valid", true, E_LOGLEVEL.e_LogLevel_Error);
                    Utilities.v_PrintMessage("MaximoMng: " + ex.Message, true, E_LOGLEVEL.e_LogLevel_Error);
                    m_b_TransferOK = false;
                }
            }
            /* Dispose once all HttpClient calls are complete. This is not necessary 
            if the containing object will be disposed of; for example in this case 
            the HttpClient instance will be disposed automatically when the application 
            terminates so the following call is superfluous. */
            c_HttpClientPRHeader.Dispose();
            c_HttpClientPRLine.Dispose();

            if (list_PurchaseRequest.Count > 0)
            {
                b_PurchaseRequestIsLoaded = true;
            }

            tChar pui8_help = (list_PurchaseRequest.Count <= 1) ? " was" : " were";
            Utilities.v_PrintMessage("MaximoMng: " + list_PurchaseRequest.Count.ToString() + " PurchaseRequest" + pui8_help + " found in Maximo");
            Utilities.v_PrintMessage("MaximoMng: " + (ui32_NumOfPurchaseRequest - list_PurchaseRequest.Count).ToString() + " PurchaseRequest not valid"
                            , true, (ui32_NumOfPurchaseRequest - list_PurchaseRequest.Count) == 0 ? 0 : E_LOGLEVEL.e_LogLevel_Warning);

            return list_PurchaseRequest;
        }

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov
        @Creation:  03.03.2024
        ----------------------------------------------------------------
        @Function Description: return a PurchaseRequest as a tChar-Object
        ----------------------------------------------------------------
        @parameter: tChar pui8_ZTACHE, tChar pui8_REQUSR, tChar pui8_PRQDAT, tChar pui8_ZSUPPLIER
                    , tChar pui8_ZNATURE, tChar pui8_ZOBJ
                    , tUInt32 ui32_Size, tChar pui8_XmlPurchaseRequestLine
                    , tChar pui8_ZISSUEDAT, tChar pui8_ZREFDA, tChar pui8_ZPROG
        @Returnvalue: pui8_XmlPurchaseRequest
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        private tChar FillPurchaseRequest(tChar pui8_REQUSR, tChar pui8_PRQDAT, tChar pui8_ZISSUEDAT, tChar pui8_ZSUPPLIER, tChar pui8_ZREFDA
                                            , tChar pui8_ZNATURE, tChar pui8_ZTACHE, tChar pui8_ZPROG, tChar pui8_MEMO
                                            , tUInt32 ui32_Size, tChar pui8_XmlPurchaseRequestLine)
        {

            tChar pui8_XmlPurchaseRequest = "<PARAM><GRP ID=\"PSH0_1\">"
                                            + "<FLD NAME=\"REQUSR\" TYPE=\"Char\">" + pui8_REQUSR + "</FLD>"
                                            + "<FLD NAME=\"PSHFCY\" TYPE=\"Char\">MGD</FLD>"
                                            + "<FLD NAME=\"PSHNUM\" TYPE=\"Char\"></FLD>"
                                            + "<FLD NAME=\"PRQDAT\" TYPE=\"Date\">" + pui8_PRQDAT + "</FLD>"
                                            + "<FLD NAME=\"ZSUPPLIER\" TYPE=\"Char\">" + pui8_ZSUPPLIER + "</FLD>"
                                            + "<FLD NAME=\"ZREFDA\" TYPE=\"Char\">" + pui8_ZREFDA + "</FLD>";

            if (E_TransactionType.e_Transaction_DDLM == Program.m_e_TransactionType)
            {
                pui8_XmlPurchaseRequest += "<FLD NAME=\"ZISSUEDAT\" TYPE=\"Date\">" + pui8_ZISSUEDAT + "</FLD>";
            }
            pui8_XmlPurchaseRequest += "</GRP>";
            if (E_TransactionType.e_Transaction_STD == Program.m_e_TransactionType)
            {
                pui8_XmlPurchaseRequest += "<GRP ID=\"PSH1_3\">"
                                        + "<FLD NAME=\"ZNATURE\" TYPE=\"Char\">" + pui8_ZNATURE + "</FLD>"
                                        + "<FLD NAME=\"ZTACHE\" TYPE=\"Char\">" + pui8_ZTACHE + "</FLD>"
                                        + "<FLD NAME=\"ZMEMO\" TYPE=\"Char\">" + pui8_MEMO + "</FLD>"
                                        + "</GRP>";
            }
            else if (E_TransactionType.e_Transaction_DDLM == Program.m_e_TransactionType)
            {
                pui8_XmlPurchaseRequest += "<GRP ID=\"PSH1_3\">"
                                        + "<FLD NAME=\"ZACTIVITE\" TYPE=\"Char\">" + pui8_ZNATURE + "</FLD>"
                                        + "<FLD NAME=\"ZTACHE\" TYPE=\"Char\">" + pui8_ZTACHE + "</FLD>"
                                        + "<FLD NAME=\"ZPROG\" TYPE=\"Char\">" + pui8_ZPROG + "</FLD>"
                                        + "<FLD NAME=\"ZOBJ\" TYPE=\"Char\">" + pui8_MEMO + "</FLD>"
                                        + "</GRP>";
            }
            else
            {

            }
            pui8_XmlPurchaseRequest += "<TAB DIM=\"200\" ID=\"PSH1_1\" SIZE=\"" + ui32_Size + "\">" + pui8_XmlPurchaseRequestLine + "</TAB>" + "</PARAM>";
            return pui8_XmlPurchaseRequest;
        }

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov
        @Creation:  03.03.2024
        ----------------------------------------------------------------
        @Function Description: return a PurchaseRequestLine as a tChar-Object
        ----------------------------------------------------------------
        @parameter: tUInt32 ui32_CounterLine, XmlNode c_XmlNode,
                    tChar pui8_ZSUPPLIER, tChar pui8_CCE1, tChar pui8_LINACC2
        @Returnvalue: pui8_XmlPurchaseRequestLine
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        private tChar FillPurchaseRequestLine(tUInt32 ui32_CounterLine, tChar pui8_ITMREF, tChar pui8_EXTRCPDAT
                                                , tChar pui8_QTYPUU, tChar pui8_BSPNUM, tChar pui8_NETPRI
                                                , tChar pui8_LINACC2, tChar pui8_CCE1, ref tChar pui8_MissedAttribut)
        {
            tChar pui8_XmlPurchaseRequestLine = "<LIN NUM=\"" + ui32_CounterLine + "\">" // Line number                                                    
                + "<FLD NAME=\"ITMREF\" TYPE=\"Char\">" + pui8_ITMREF + "</FLD>"          // Item Reference
                + "<FLD NAME=\"PRHFCY\" TYPE=\"Char\">MGD</FLD>"                         // Site                                                      
                + "<FLD NAME=\"EXTRCPDAT\" TYPE=\"Date\">" + pui8_EXTRCPDAT + "</FLD>"    // Request date                      
                + "<FLD NAME=\"QTYPUU\" TYPE=\"Decimal\">" + pui8_QTYPUU + "</FLD>"       // Quantity UA
                + "<FLD NAME=\"QTYSTU\" TYPE=\"Decimal\">" + pui8_QTYPUU + "</FLD>"       // Quantity US
                + "<FLD NAME=\"BPSNUM\" TYPE=\"Char\">" + pui8_BSPNUM + "</FLD>"       // Supplier
                + "<FLD NAME=\"NETPRI\" TYPE=\"Decimal\">" + pui8_NETPRI + "</FLD>"       // Net price
                + "<FLD NAME=\"LINACC1\" TYPE=\"Char\">6020000000</FLD>";                // Plan Gen PAD

            if (E_TransactionType.e_Transaction_STD == Program.m_e_TransactionType)
            {
                pui8_XmlPurchaseRequestLine += "<FLD NAME=\"LINACC2\" TYPE=\"Char\">" + pui8_LINACC2 + "</FLD>";
            }

            pui8_XmlPurchaseRequestLine += "<FLD NAME=\"CCE1\" TYPE=\"Char\">" + pui8_CCE1 + "</FLD>"               // Tâches
                                                + "</LIN>";

            // read which attribut is not filled
            if ((pui8_ITMREF.Length == 0) && (pui8_MissedAttribut.Contains("ITEMNUM ") == false))
            {
                pui8_MissedAttribut += "ITEMNUM ";
            }
            if ((pui8_QTYPUU.Length == 0) && (pui8_MissedAttribut.Contains("ORDERQTY ") == false))
            {
                pui8_MissedAttribut += "ORDERQTY ";
            }
            if ((pui8_NETPRI.Length == 0) && (pui8_MissedAttribut.Contains("UNITCOST ") == false))
            {
                pui8_MissedAttribut += "UNITCOST ";
            }
            return pui8_XmlPurchaseRequestLine;
        }

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov
        @Creation:  25.07.2023
        ----------------------------------------------------------------
        @Function Description: formated the xml text for the log file
        ----------------------------------------------------------------
        @parameter: tChar pui8_XMLText
        @Returnvalue: pui8_TextFormated
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        public tChar pui8_FormatXMLText(tChar pui8_XMLText)
        {
            int ui32_Position = 0;
            tChar pui8_TextFormated = "";
            while (ui32_Position < pui8_XMLText.Length)
            {
                pui8_TextFormated += pui8_XMLText[ui32_Position];
                if (pui8_XMLText[ui32_Position] == 'D')
                {
                    if (pui8_XMLText[ui32_Position + 1] == '>')
                    {
                        pui8_TextFormated += ">\n";
                        ui32_Position++;
                    }
                }

                else if (pui8_XMLText[ui32_Position] == '"')
                {
                    if (pui8_XMLText[ui32_Position + 1] == '>')
                    {
                        if (pui8_XMLText[ui32_Position + 2] == '<')
                        {
                            if (pui8_XMLText[ui32_Position + 3] != '/')
                            {
                                pui8_TextFormated += ">\n<";
                                ui32_Position += 2;
                            }
                        }
                    }
                }
                ui32_Position++;
            }
            return pui8_TextFormated;
        }

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Jonathan Guiyoba | Digit-Tech-Innov
        @Creation:  07.03.2024
        ----------------------------------------------------------------
        @Function Description: Extracts information from FCPROJECTID and fills the corresponding variables.
                               The function returns a boolean value indicating whether the operation was successful.
        ----------------------------------------------------------------
        @parameter: tChar[] pui8_Array, ref tChar pui8_CCE1, ref tChar pui8_LINACC2,
                    ref tChar pui8_MissedAttribut  
        @Returnvalue: tBool b_RetVal (true if successful, false otherwise)
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        private tBool ExctractFromStringArray(tChar[] pui8_FCPROJECTID, ref tChar pui8_CCE1, ref tChar pui8_LINACC2, ref tChar pui8_MissedAttribut)
        {
            tBool b_RetVal = true;

            if (pui8_FCPROJECTID.Length == 2)
            {
                pui8_CCE1 = pui8_FCPROJECTID[0];     // CCE1
                pui8_LINACC2 = pui8_FCPROJECTID[1];  // LINACC2
            }
            else if (pui8_FCPROJECTID.Length == 1)
            {
                pui8_CCE1 = pui8_FCPROJECTID[0];     // CCE1
                b_RetVal = false;
                if (!pui8_MissedAttribut.Contains("LINACC2 "))
                {
                    pui8_MissedAttribut += "LINACC2 ";
                }
            }
            else
            {
                b_RetVal = false;
                if (!pui8_MissedAttribut.Contains("CCE1 LINACC2 "))
                {
                    pui8_MissedAttribut += "CCE1 LINACC2 ";
                }
            }

            return b_RetVal;
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
        @Creation:  09.03.2024
        ----------------------------------------------------------------
        @Function Description:  return SW version
        ----------------------------------------------------------------
        @parameter: -
        @Returnvalue: SW version
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        public tChar pui8_GetVersion()
        {
            tUInt8[] ui8_SWVersion = new tUInt8[3] { 3, 1, 0 };
            tChar pui8_SWVersion = "";
            if ((E_Environnement_SageX3.e_Environnement_SageX3_TEST_0 == Program.m_e_Environnement_SageX3) || (E_Environnement_SageX3.e_Environnement_SageX3_TEST_1 == Program.m_e_Environnement_SageX3))
            {
                pui8_SWVersion = "A";
            }
            else if (E_Environnement_SageX3.e_Environnement_SageX3_PROD == Program.m_e_Environnement_SageX3)
            {
                pui8_SWVersion = "X";
            }
            pui8_SWVersion += ui8_SWVersion[0].ToString() + ui8_SWVersion[1].ToString() + ui8_SWVersion[2].ToString();
            return pui8_SWVersion;
        }

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov solutions and services
        @Creation:  09.03.2024
        ----------------------------------------------------------------
        @Function Description:  return SW builds date
        ----------------------------------------------------------------
        @parameter: -
        @Returnvalue: builds date
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        public tChar pui8_GetDate()
        {
            tChar day = "25";
            tChar month = "07";
            tChar year = "2024";
            tChar hour = "23";
            tChar minute = "50";
            return year + month + day + hour + minute;
        }

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov solutions and services
        @Creation:  21.03.2024
        ----------------------------------------------------------------
        @Function Description: Compare 2 date to see which one is younger
        ----------------------------------------------------------------
        @parameter: tChar pui8_DateToCompare, tChar pui8_CompareDate
        @Returnvalue:   -1 if the pui8_DateTocompare is younger than pui8_CompareDate
                        0 if the pui8_DateTocompare is equal to pui8_CompareDate
                        1 if the pui8_DateTocompare is older than pui8_CompareDate
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        private tUInt8 ui8_CompareDate(tChar pui8_DateToCompare, tChar pui8_CompareDate)
        {
            DateTime c_DateToCompare = DateTime.Parse(pui8_DateToCompare);
            DateTime c_CompareDate = DateTime.Parse(pui8_CompareDate);

            return (tUInt8)c_DateToCompare.CompareTo(c_CompareDate);
        }

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov solutions and services
        @Creation:  21.03.2024
        ----------------------------------------------------------------------
        @Function Description: Compare 2 date to see which one is younger. 
                               
        ----------------------------------------------------------------
        @parameter: tChar pui8_DateTimeToCompare, tChar pui8_DateTimeCompareTo
                    ref tChar pui8_AttributDate
        @Returnvalue:  -
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        private void v_CheckDate(tChar pui8_DateTimeToCompare, tChar pui8_DateTimeCompareTo, ref tChar pui8_AttributDate)
        {
            // 
            pui8_AttributDate = (ui8_CompareDate(pui8_DateTimeToCompare, pui8_DateTimeCompareTo) == 1) ||
                (ui8_CompareDate(pui8_DateTimeToCompare, pui8_DateTimeCompareTo) == 0) ? pui8_DateTimeToCompare.Replace("-", "") : pui8_DateTimeCompareTo.Replace("-", "");
        }

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov solutions and services
        @Creation: 21.04.2024
        ----------------------------------------------------------------
        @Function Description:  
        ----------------------------------------------------------------
        @parameter: -
        @Returnvalue:   -
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        private void v_GetServerCertificat()
        {
            System.Net.ServicePointManager.ServerCertificateValidationCallback +=
            delegate (object sender, System.Security.Cryptography.X509Certificates.X509Certificate certificate,
                                    System.Security.Cryptography.X509Certificates.X509Chain chain,
                                    System.Net.Security.SslPolicyErrors sslPolicyErrors)
            {
                try
                {

                }
                catch (Exception ex)
                {
                    Utilities.v_PrintMessage("MaximoMng: An Exception occurs with ServerCertificateValidationCallback", true, E_LOGLEVEL.e_LogLevel_Error);
                    Utilities.v_PrintMessage("MaximoMng: " + ex.Message, true, E_LOGLEVEL.e_LogLevel_Error);
                    m_b_TransferOK = false;
                }
                return true; // **** Always accept
            };
        }

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov solutions and services
        @Creation: 21.04.2024
        ----------------------------------------------------------------
        @Function Description:  start the connection to PR Url
        ----------------------------------------------------------------
        @parameter: HttpClient c_HttpClientPRHeader, HttpClient c_HttpClientPRLine
        @Returnvalue:   
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        private Tuple<HttpResponseMessage, HttpResponseMessage> t_StartConnectionWithPRURL(HttpClient c_HttpClientPRHeader, HttpClient c_HttpClientPRLine)
        {
            HttpResponseMessage c_HTTPResponseMsgHeader = null, c_HTTPResponseMsgLine = null;
            try
            {
                Utilities.v_PrintMessage("MaximoMng: Connection to PurchaseRequestHeader started");
                c_HttpClientPRHeader.Timeout = TimeSpan.FromSeconds(Program.m_ui8_HttpRequestTimeoutInSecond);

                c_HTTPResponseMsgHeader = c_HttpClientPRHeader.GetAsync(Program.m_pui8_purchaseRequestHeader).Result;

                if (c_HTTPResponseMsgHeader.IsSuccessStatusCode)
                {
                    Utilities.v_PrintMessage("MaximoMng: Connection to PurchaseRequestHeader established");
                }
                else
                {
                    Utilities.v_PrintMessage("MaximoMng: Connection to PurchaseRequestHeader can't be established --> TR not OK", true, E_LOGLEVEL.e_LogLevel_Error);
                    m_b_TransferOK = false;
                }
            }
            catch (Exception e)
            {
                Utilities.v_PrintMessage("MaximoMng: An Exception occurred when connecting to PurchaseRequestHeader", false, E_LOGLEVEL.e_LogLevel_Error);
                Utilities.v_PrintMessage("MaximoMng: " + e.Message, false, E_LOGLEVEL.e_LogLevel_Error);
                m_b_TransferOK = false;
                m_b_SendNotification = true;
            }
            try
            {
                Utilities.v_PrintMessage("MaximoMng: Connection to PurchaseRequestLine started");
                c_HttpClientPRLine.Timeout = TimeSpan.FromSeconds(Program.m_ui8_HttpRequestTimeoutInSecond);
                c_HTTPResponseMsgLine = c_HttpClientPRLine.GetAsync(Program.m_pui8_purchaseRequestLine).Result;


                if (c_HTTPResponseMsgLine.IsSuccessStatusCode)
                {
                    Utilities.v_PrintMessage("MaximoMng: Connection to PurchaseRequestLine established");
                }
                else
                {
                    Utilities.v_PrintMessage("MaximoMng: Connection to PurchaseRequestLine can't be established --> TR not OK", true, E_LOGLEVEL.e_LogLevel_Error);
                    m_b_TransferOK = false;
                    m_b_SendNotification = true;
                }
            }
            catch (Exception e)
            {
                Utilities.v_PrintMessage("MaximoMng: An Exception occurred when connecting to PurchaseRequestLine", false, E_LOGLEVEL.e_LogLevel_Error);
                Utilities.v_PrintMessage("MaximoMng: " + e.Message, false, E_LOGLEVEL.e_LogLevel_Error);
                m_b_TransferOK = false;
                m_b_SendNotification = true;
            }
            return new Tuple<HttpResponseMessage, HttpResponseMessage>(c_HTTPResponseMsgHeader, c_HTTPResponseMsgLine);
        }

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov solutions and services
        @Creation: 21.04.2024
        ----------------------------------------------------------------
        @Function Description:  Get the url content
        ----------------------------------------------------------------
        @parameter: HttpResponseMessage c_HttpResponseMsg
        @Returnvalue:   
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        private Tuple<tBool, XmlDocument> t_GetContentURLPR(HttpResponseMessage c_HttpResponseMsg)
        {
            tBool b_SuccesStatusCode = false;
            XmlDocument c_XmlDocument = new XmlDocument();
            if (c_HttpResponseMsg != null)
            {
                if (c_HttpResponseMsg.IsSuccessStatusCode)
                {
                    var dataObjects = c_HttpResponseMsg.Content.ReadAsStringAsync().Result;

                    c_XmlDocument.LoadXml(dataObjects);
                    b_SuccesStatusCode = true;
                }
            }
            Tuple<tBool, XmlDocument> t_ContentUrlPR = Tuple.Create(b_SuccesStatusCode, c_XmlDocument);
            return t_ContentUrlPR;
        }

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov solutions and services
        @Creation: 21.04.2024
        ----------------------------------------------------------------
        @Function Description:  read the doucment a return as a xml file
        ----------------------------------------------------------------
        @parameter: tChar filepath
        @Returnvalue:   return true and the document in xml format
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        private Tuple<tBool, XmlDocument> t_ReadXmldocument(tChar filepath)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(filepath);
            return new Tuple<tBool, XmlDocument>(true, doc);
        }

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov solutions and services
        @Creation: 21.04.2024
        ----------------------------------------------------------------------
        @Function Description: splits the field FCPROJECTID to extract the 
                                values of nature and task
        ----------------------------------------------------------------------
        @parameter: tChar pui8_BudgetLine
        @Returnvalue:  field natur and tache
        +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        private Tuple<tChar, tChar> t_TruncBudgetLine(tChar pui8_BudgetLine)
        {
            tChar pui8_tache = "";
            tChar pui8_nature = "";

            if (pui8_BudgetLine.Length > 11)
            {
                const tUInt8 ui8_NatureLength = 7;
                const tUInt8 ui8_YearLength = 4;
                tUInt8 ui8_TacheLength = (tUInt8)((tUInt8)(pui8_BudgetLine.Length) - ui8_NatureLength - ui8_YearLength);
                pui8_tache = pui8_BudgetLine.Substring(0, ui8_TacheLength);
                pui8_nature = pui8_BudgetLine.Substring(ui8_TacheLength, ui8_NatureLength);
            }

            return new Tuple<tChar, tChar>(pui8_tache, pui8_nature);
        }

        public tBool b_GetSendNotification()
        {
            return m_b_SendNotification;
        }

    }
}