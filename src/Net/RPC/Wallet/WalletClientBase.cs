﻿/*
 *   Coinium project - crypto currency pool software - https://github.com/raistlinthewiz/coinium
 *   Copyright (C) 2013 Hüseyin Uslu, Int6 Studios - http://www.coinium.org
 *
 *   This program is free software: you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation, either version 3 of the License, or
 *   (at your option) any later version.
 *
 *   This program is distributed in the hope that it will be useful,
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *   GNU General Public License for more details.
 *
 *   You should have received a copy of the GNU General Public License
 *   along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

/* This file is based on https://github.com/BitKoot/BitcoinRpcSharp */

using System;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using Serilog;
using coinium.Common.Extensions;

namespace coinium.Net.RPC.Wallet
{
    public class WalletClientBase
    {
        public string RpcUrl { get; set; }
        public string RpcUser { get; set; }
        public string RpcPassword { get; set; }

        public WalletClientBase(string url, string user, string password)
        {
            this.RpcUrl = url;
            this.RpcUser = user;
            this.RpcPassword = password;
        }

        /// <summary>
        /// Make a a request. Invoke the given method with the given parameters.
        /// </summary>
        /// <typeparam name="T">
        /// Type to return as the response. 
        /// We will try to convert the JSON response to this type.
        /// </typeparam>
        /// <param name="method">Method to invoke.</param>
        /// <param name="parameters">Parameters to pass to the method.</param>
        /// <returns>The JSON RPC response deserialized as the given type.</returns>
        public T MakeRequest<T>(string method, params object[] parameters)
        {
            Log.Verbose("rpc-call: {0}", method);
            var rpcResponse = MakeRpcRequest<T>(new WalletRequest(1, method, parameters));
            return rpcResponse.Result;
        }

        /// <summary>
        /// Make a raw JSON RPC request with the given request object. Returns raw JSON.
        /// </summary>
        /// <param name="walletRequest">The request object.</param>
        /// <returns>The raw JSON string.</returns>
        public string MakeRawRpcRequest(WalletRequest walletRequest)
        {
            HttpWebRequest httpWebRequest = MakeHttpRequest(walletRequest);
            return GetJsonResponse(httpWebRequest);
        }

        /// <summary>
        /// Make an JSON RPC request, and return a JSON RPC response object with the result 
        /// deserialized as the given type.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the result as.</typeparam>
        /// <param name="walletRequest">The request object.</param>
        /// <returns>A JSON RPC response with the result deserialized as the given type.</returns>
        private WalletResponse<T> MakeRpcRequest<T>(WalletRequest walletRequest)
        {
            HttpWebRequest httpWebRequest = MakeHttpRequest(walletRequest);
            return GetRpcResponse<T>(httpWebRequest);
        }

        /// <summary>
        /// Make the actual HTTP request to the Bitcoin RPC interface.
        /// </summary>
        /// <param name="walletRequest">The request to make.</param>
        /// <returns>The HTTP request object.</returns>
        private HttpWebRequest MakeHttpRequest(WalletRequest walletRequest)
        {
            var webRequest = (HttpWebRequest)WebRequest.Create(RpcUrl);
            webRequest.Credentials = new NetworkCredential(RpcUser, RpcPassword);

            // Important, otherwise the service can't deserialse your request properly
            webRequest.ContentType = "application/json-rpc";
            webRequest.Method = "POST";
            webRequest.Timeout = 2000; // 2 seconds

            byte[] byteArray = walletRequest.GetBytes();
            webRequest.ContentLength = byteArray.Length;

            try
            {
                using (Stream dataStream = webRequest.GetRequestStream())
                {
                    dataStream.Write(byteArray, 0, byteArray.Length);
                    dataStream.Close();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("There was a problem sending the request.", ex);
            }

            return webRequest;
        }

        /// <summary>
        /// Get the JSON RPC response for the given HTTP request.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response result to.</typeparam>
        /// <param name="httpWebRequest">The web request.</param>
        /// <returns>A JSON RPC response with the result deserialized as the given type.</returns>
        private WalletResponse<T> GetRpcResponse<T>(HttpWebRequest httpWebRequest)
        {
            string json = GetJsonResponse(httpWebRequest);

            try
            {
                return JsonConvert.DeserializeObject<WalletResponse<T>>(json);
            }
            catch (JsonException jsonEx)
            {
                throw new Exception("There was a problem deserializing the response from the bitcoin wallet.", jsonEx);
            }
        }

        /// <summary>
        /// Gets the JSON response for the given HTTP request.
        /// </summary>
        /// <param name="httpWebRequest">The HTTP request send to the Bitcoin RPC interface.</param>
        /// <returns>The raw JSON string.</returns>
        private string GetJsonResponse(HttpWebRequest httpWebRequest)
        {
            try
            {
                WebResponse webResponse = httpWebRequest.GetResponse();

                // Deserialize the json response
                using (var stream = webResponse.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    string result = reader.ReadToEnd();
                    reader.Close();

                    Log.Verbose(JsonFormatter.PrettyPrint(result));

                    return result;
                }
            }
            catch (ProtocolViolationException protoEx)
            {
                throw new Exception("Unable to connect to the Bitcoin server.", protoEx);
            }
            catch (WebException webEx)
            {
                HttpWebResponse webResponse = webEx.Response as HttpWebResponse;
                if (webResponse != null)
                {
                    switch (webResponse.StatusCode)
                    {
                        case HttpStatusCode.InternalServerError:
                            throw new Exception("The RPC request was either not understood by the Bitcoin server or there was a problem executing the request.", webEx);
                    }
                }

                throw new Exception("An unknown web exception occured while trying to read the JSON response.", webEx);
            }
            catch (Exception ex)
            {
                throw new Exception("An unknown exception occured while trying to read the JSON response.", ex);
            }
        }
    }
}
