/*
 *  Override basic Soap Client to add authentication
 */

using MAXI_X3.WebReference;
using System;
using System.Net;
using System.Text;

namespace SCDx3_v1._1
{
    internal class CAdxWebServiceXmlCCServiceBasic : CAdxWebServiceXmlCCService
    {
        #region Public properties

        private bool m_b_AuthenticationFailed = false;

        private bool _basicAuth = false;
        public bool BasicAuth
        {
            get => _basicAuth;
            set => _basicAuth = value;
        }
        private string _accessToken = "";
        public string AccessToken
        {
            get => _accessToken;
            set => _accessToken = value;
        }

        #endregion

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov solutions and services
        @Creation:  25.07.2023
        ----------------------------------------------------------------
        @Function Description: 
        ----------------------------------------------------------------
        @parameter: Uri uri
        @Returnvalue:   -
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        protected override WebRequest GetWebRequest(Uri uri)
        {
            HttpWebRequest webRequest = (HttpWebRequest)base.GetWebRequest(uri);

            if (BasicAuth == true)
            {
                if (Credentials is NetworkCredential c_NetworkCredential)
                {
                    string authInfo =
                    ((c_NetworkCredential.Domain != null) && (c_NetworkCredential.Domain.Length > 0) ?
                    c_NetworkCredential.Domain + @"\" : string.Empty) +
                    c_NetworkCredential.UserName + ":" + c_NetworkCredential.Password;
                    authInfo = Convert.ToBase64String(Encoding.Default.GetBytes(authInfo));
                    webRequest.Headers["Authorization"] = "Basic " + authInfo;
                    m_b_AuthenticationFailed = true;
                }

            }
            else
            {
                webRequest.Headers["Authorization"] = "Bearer " + AccessToken;
            }

            return webRequest;
        }

        /*++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        @Author:    Gerald Emvoutou | Digit-Tech-Innov solutions and services
        @Creation:  25.07.2023
        ----------------------------------------------------------------
        @Function Description: Get if the authentication was successful
            or not
        ----------------------------------------------------------------
        @parameter: -
        @Returnvalue:   true - authentication was successful
                        false - authentication failed
        ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++*/
        public bool GetAuthentication()
        {
            return m_b_AuthenticationFailed;
        }

    }
}
