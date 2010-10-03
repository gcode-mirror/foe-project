﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Mail;
using System.Net;
using OpenPOP.POP3;
using System.Data.SqlClient;
using Foe.Server;
using Foe.Common;

namespace Foe.Server
{
    public static class FoeServerMessage
    {
        static string _className = "FoeServerMessage";

        public static void SendMessage(SmtpServer server, string from, string to, string subject, string content)
        {
            // simply call the SendMessage function in the Common.MessageManager namespace
            Foe.Common.MessageManager.SendMessage(server, from, to, subject, content);
        }

        /// <summary>
        /// Download email messages from the POP3 server for a given Foe message processor.
        /// </summary>
        /// <param name="server">POP3 server information</param>
        /// <param name="processorEmail">The current Foe message processor's email address.</param>
        public static void DownloadMessages(PopServer server, string processorEmail)
        {
            
            // connect to POP3 server and download messages
            //FoeDebug.Print("Connecting to POP3 server...");
            POPClient popClient = new POPClient();
            popClient.IsUsingSsl = server.SslEnabled;

            popClient.Disconnect();
            popClient.Connect(server.ServerName, server.Port);
            popClient.Authenticate(server.UserName, server.Password);

            FoeDebug.Print("Connected to POP3.");

            // get mail count
            int count = popClient.GetMessageCount();

            FoeDebug.Print("Server reported " + count.ToString() + " messages.");

            // go through each message, from newest to oldest
            for (int i = count; i >= 1; i -= 1)
            {
                //FoeDebug.Print("Opening mail message...");

                OpenPOP.MIMEParser.Message msg = popClient.GetMessage(i, true);
                if (msg != null)
                {
                    // Get subject and verify sender identity
                    // Subject line in the mail header should look like one of the followings:
                    //
                    // Normal request (for news feed and content):
                    //   Subject: Request <Request ID> by <User ID>
                    //
                    // Registration request:
                    //   Subject: Register <Request ID> by Newbie
                    //
                    // where:
                    // Request ID is the request ID generated by the Foe client
                    // User ID is the user's ID as assigned by the server

                    //FoeDebug.Print("Message is not null. Getting message details.");

                    string subject = msg.Subject;
                    string fromEmail = msg.FromEmail;

                    //FoeDebug.Print("Subject: " + subject);
                    //FoeDebug.Print("From: " + fromEmail);

                    // parse subject line
                    string[] tokens = subject.Trim().Split(new char[] { ' ' });
                    if (tokens.Length == 4)
                    {
                        // check what type of request is it
                        string requestType = tokens[0].ToUpper();
                        string requestId = tokens[1];
                        string userId = tokens[3];

                        FoeServerLog.Add(_className + ".DownloadMessages", FoeServerLog.LogType.Message,
                                "subject: " + subject + "requestType: "+ requestType);
                        if (requestType.ToUpper().CompareTo("REGISTE") == 0)
                        {
                            //FoeDebug.Print("This is a registration message.");
                            // It's a registration request
                            SaveRegistrationRequest(requestId, fromEmail, processorEmail);

                            FoeServerLog.Add(_className + ".DownloadMessages", FoeServerLog.LogType.Message,
                                "Received registration request from " + fromEmail);
                        }
                        else if (requestType.ToUpper().CompareTo("CATALOG") == 0)
                        {                            
                            // get user info by email address
                            FoeUser user = FoeServerUser.GetUser(fromEmail);

                            // verify user's email against the user ID
                            if ((user != null) && (userId == user.UserId) && (processorEmail == user.ProcessorEmail))
                            {
                                FoeDebug.Print("User verified.");

                                // the user's identity is verified
                                SaveCatalogRequest(requestId, user.Email, processorEmail);

                            }
                            else
                            {
                                //FoeDebug.Print("User is not registered. Request not processed.");
                                FoeServerLog.Add(_className + ".DownloadMessages", FoeServerLog.LogType.Warning,
                                    "Received content request from unregistered user " + fromEmail);
                            }
                        }
                        else if (requestType.ToUpper().CompareTo("CONTENT") == 0)
                        {
                            //FoeDebug.Print("This is a content request message.");

                            // It's a content request.
                            // We need to verify the user's identify first.

                            //FoeDebug.Print("Verifying user identity...");

                            // get user info by email address
                            FoeUser user = FoeServerUser.GetUser(fromEmail);

                            // verify user's email against the user ID
                            if ((user != null) && (userId == user.UserId) && (processorEmail == user.ProcessorEmail))
                            {
                                FoeDebug.Print("User verified.");

                                // the user's identity is verified
                                // get the full message body
                                OpenPOP.MIMEParser.Message wholeMsg = popClient.GetMessage(i, false);
                                string msgBody = (string)wholeMsg.MessageBody[0];

                                try
                                {
                                    // decompress it
                                    byte[] compressedMsg = Convert.FromBase64String(msgBody);
                                    byte[] decompressedMsg = CompressionManager.Decompress(compressedMsg);
                                    string foe = Encoding.UTF8.GetString(decompressedMsg);

                                    string[] catalogs = foe.Trim().Split(new char[] { ',' });
                                    // save request
                                    if (catalogs.Length == 0) 
                                    {
                                        return;
                                    }
                                    SaveContentRequest(requestId, user.Email, catalogs, processorEmail);

                                    //FoeDebug.Print("Request saved and pending processing.");
                                    FoeServerLog.Add(_className + ".DownloadMessages", FoeServerLog.LogType.Message,
                                        "Received content request from verified user " + fromEmail);
                                }
                                catch (Exception except)
                                {
                                    // the message is likely malformed
                                    // so just ignore it
                                    FoeServerLog.Add(_className + ".DownloadMessages", FoeServerLog.LogType.Warning,
                                        "Received malformed content request from verified user " + fromEmail + "\r\n" +
                                        except.ToString() +
                                        "Raw message:\r\n" + msgBody + "\r\n");

                                    //throw except;
                                }
                            }
                            else
                            {
                                //FoeDebug.Print("User is not registered. Request not processed.");
                                FoeServerLog.Add(_className + ".DownloadMessages", FoeServerLog.LogType.Warning,
                                    "Received content request from unregistered user " + fromEmail);
                            }
                        }
                        else if (requestType.ToUpper().CompareTo("FEED") == 0)
                        {
                            //FoeDebug.Print("This is a content request message.");

                            // It's a content request.
                            // We need to verify the user's identify first.

                            //FoeDebug.Print("Verifying user identity...");

                            // get user info by email address
                            FoeUser user = FoeServerUser.GetUser(fromEmail);

                            // verify user's email against the user ID
                            if ((user != null) && (userId == user.UserId) && (processorEmail == user.ProcessorEmail))
                            {
                                FoeDebug.Print("User verified.");

                                // the user's identity is verified
                                // get the full message body
                                OpenPOP.MIMEParser.Message wholeMsg = popClient.GetMessage(i, false);
                                string msgBody = (string)wholeMsg.MessageBody[0];

                                try
                                {
                                    // decompress it
                                    byte[] compressedMsg = Convert.FromBase64String(msgBody);
                                    byte[] decompressedMsg = CompressionManager.Decompress(compressedMsg);
                                    string foe = Encoding.UTF8.GetString(decompressedMsg);

                                    string[] array = foe.Trim().Split(new char[] { ',' });
                                    // save request
                                    if (array.Length == 0)
                                    {
                                        return;
                                    }
                                    SaveFeedRequest(requestId, user.Email, array, processorEmail);

                                    //FoeDebug.Print("Request saved and pending processing.");
                                    FoeServerLog.Add(_className + ".DownloadMessages", FoeServerLog.LogType.Message,
                                        "Received feed request from verified user " + fromEmail);
                                }
                                catch (Exception except)
                                {
                                    // the message is likely malformed
                                    // so just ignore it
                                    FoeServerLog.Add(_className + ".DownloadMessages", FoeServerLog.LogType.Warning,
                                        "Received malformed feed request from verified user " + fromEmail + "\r\n" +
                                        except.ToString() +
                                        "Raw message:\r\n" + msgBody + "\r\n");

                                    //throw except;
                                }
                            }
                            else
                            {
                                //FoeDebug.Print("User is not registered. Request not processed.");
                                FoeServerLog.Add(_className + ".DownloadMessages", FoeServerLog.LogType.Warning,
                                    "Received content request from unregistered user " + fromEmail);
                            }
                        }
                        else
                        {
                            // Non-Foe message
                            FoeServerLog.Add(_className + ".DownloadMessages", FoeServerLog.LogType.Message,
                                "Received non-Foe message from " + fromEmail);
                        }
                    }
                    else
                    {
                        // Non-Foe message
                        FoeServerLog.Add(_className + ".DownloadMessages", FoeServerLog.LogType.Message,
                            "Received non-Foe message from " + fromEmail);
                    }

                    // Delete the current message
                    popClient.DeleteMessage(i);
                }
            }
            popClient.Disconnect();
        }

        private static void SaveFeedRequest(string requestId, string userEmail, string[] array, string processorEmail)
        {
            // Connect to DB
            SqlConnection conn = FoeServerDb.OpenDb();
            SqlCommand cmd = conn.CreateCommand();

            string feedName = array[0];
            string location = array[1];

            // insert the feed to CatalogRss
            cmd.CommandText =
                "insert into CatalogRss (Code,Name,ContentType,Description,Location) " +
                "values (@code,@name,'RSS',@description,@location)";

            cmd.Parameters.Add("@code", System.Data.SqlDbType.NVarChar, 10);
            cmd.Parameters.Add("@name", System.Data.SqlDbType.NVarChar, 32);
            cmd.Parameters.Add("@description", System.Data.SqlDbType.NVarChar, 512);
            cmd.Parameters.Add("@location", System.Data.SqlDbType.NVarChar, 512);
            cmd.Prepare();
            cmd.Parameters["@code"].Value = feedName;
            cmd.Parameters["@name"].Value = feedName;
            cmd.Parameters["@description"].Value = feedName;
            cmd.Parameters["@location"].Value = location;

            cmd.ExecuteNonQuery();

            // Prepare and run query
            // Default status to 'P' (Pending)
            cmd.CommandText =
                "insert into Requests (RequestType, UserEmail, RequestId, ProcessorEmail, DtReceived, Status) " +
                "values (@requestType, @userEmail, @requestId, @processorEmail, @dtReceived, 'P')";

            cmd.Parameters.Add("@requestType", System.Data.SqlDbType.NVarChar, 10);
            cmd.Parameters.Add("@userEmail", System.Data.SqlDbType.NVarChar, 256);
            cmd.Parameters.Add("@requestId", System.Data.SqlDbType.NVarChar, 128);
            cmd.Parameters.Add("@processorEmail", System.Data.SqlDbType.NVarChar, 256);
            cmd.Parameters.Add("@dtReceived", System.Data.SqlDbType.DateTime);
            cmd.Prepare();
            cmd.Parameters["@requestType"].Value = FoeServerRequest.RequestTypeToString(RequestType.Feed);
            cmd.Parameters["@userEmail"].Value = userEmail;
            cmd.Parameters["@requestId"].Value = requestId;
            cmd.Parameters["@processorEmail"].Value = processorEmail;
            cmd.Parameters["@dtReceived"].Value = DateTime.Now;

            cmd.ExecuteNonQuery();

            conn.Close();
        }

        private static void SaveRegistrationRequest(string requestId, string email, string processorEmail)
        {
            // Connect to DB
            SqlConnection conn = FoeServerDb.OpenDb();
            SqlCommand cmd = conn.CreateCommand();

            // Prepare and run query
            // Default status to 'P' (Pending)
            cmd.CommandText =
                "insert into Requests (RequestType, UserEmail, RequestId, ProcessorEmail, DtReceived, Status) " +
                "values (@requestType, @userEmail, @requestId, @processorEmail, @dtReceived, 'P')";

            cmd.Parameters.Add("@requestType", System.Data.SqlDbType.NVarChar, 10);
            cmd.Parameters.Add("@userEmail", System.Data.SqlDbType.NVarChar, 256);
            cmd.Parameters.Add("@requestId", System.Data.SqlDbType.NVarChar, 128);
            cmd.Parameters.Add("@processorEmail", System.Data.SqlDbType.NVarChar, 256);
            cmd.Parameters.Add("@dtReceived", System.Data.SqlDbType.DateTime);
            cmd.Prepare();
            cmd.Parameters["@requestType"].Value = FoeServerRequest.RequestTypeToString(RequestType.Registration);
            cmd.Parameters["@userEmail"].Value = email;
            cmd.Parameters["@requestId"].Value = requestId;
            cmd.Parameters["@processorEmail"].Value = processorEmail;
            cmd.Parameters["@dtReceived"].Value = DateTime.Now;

            cmd.ExecuteNonQuery();
            conn.Close();
        }

        private static void SaveCatalogRequest(string requestId, string userEmail, string processorEmail)
        {
            // Connect to DB
            SqlConnection conn = FoeServerDb.OpenDb();
            SqlCommand cmd = conn.CreateCommand();

            // Prepare and run query
            // Default status to 'P' (Pending)
            cmd.CommandText =
                "insert into Requests (RequestType, UserEmail, RequestId, ProcessorEmail, DtReceived, Status) " +
                "values (@requestType, @userEmail, @requestId, @processorEmail, @dtReceived, 'P')";

            cmd.Parameters.Add("@requestType", System.Data.SqlDbType.NVarChar, 10);
            cmd.Parameters.Add("@userEmail", System.Data.SqlDbType.NVarChar, 256);
            cmd.Parameters.Add("@requestId", System.Data.SqlDbType.NVarChar, 128);
            cmd.Parameters.Add("@processorEmail", System.Data.SqlDbType.NVarChar, 256);
            cmd.Parameters.Add("@dtReceived", System.Data.SqlDbType.DateTime);
            cmd.Prepare();
            cmd.Parameters["@requestType"].Value = FoeServerRequest.RequestTypeToString(RequestType.Catalog);
            cmd.Parameters["@userEmail"].Value = userEmail;
            cmd.Parameters["@requestId"].Value = requestId;
            cmd.Parameters["@processorEmail"].Value = processorEmail;
            cmd.Parameters["@dtReceived"].Value = DateTime.Now;

            cmd.ExecuteNonQuery();
            conn.Close();
        }

        private static void SaveContentRequest(string requestId, string userEmail, string[] catalogs, string processorEmail)
        {
            // Connect to DB
            SqlConnection conn = FoeServerDb.OpenDb();
            SqlCommand cmd = conn.CreateCommand();

            // Prepare and run query
            // Default status to 'P' (Pending)
            cmd.CommandText =
                "insert into Requests (RequestType, UserEmail, RequestId, ProcessorEmail, RequestMessage, DtReceived, Status) " +
                "values (@requestType, @userEmail, @requestId, @processorEmail, @requestMessage, @dtReceived, 'P')";

            cmd.Parameters.Add("@requestType", System.Data.SqlDbType.NVarChar, 10);
            cmd.Parameters.Add("@userEmail", System.Data.SqlDbType.NVarChar, 256);
            cmd.Parameters.Add("@requestId", System.Data.SqlDbType.NVarChar, 128);
            cmd.Parameters.Add("@processorEmail", System.Data.SqlDbType.NVarChar, 256);
            cmd.Parameters.Add("@requestMessage", System.Data.SqlDbType.NVarChar, -1);
            cmd.Parameters.Add("@dtReceived", System.Data.SqlDbType.DateTime);
            cmd.Prepare();
            
            //message is {abc,def,ghi,}, so the last element of catalogs is ""
            for (int i=0;i<catalogs .Length-1;i++)
            {
                cmd.Parameters["@requestType"].Value = FoeServerRequest.RequestTypeToString(RequestType.Content);
                cmd.Parameters["@userEmail"].Value = userEmail;
                cmd.Parameters["@requestId"].Value = requestId;
                cmd.Parameters["@processorEmail"].Value = processorEmail;
                cmd.Parameters["@requestMessage"].Value = catalogs[i];
                cmd.Parameters["@dtReceived"].Value = DateTime.Now;

                cmd.ExecuteNonQuery();
            }
            conn.Close();
        }

        public static SmtpServer GetDefaultSmtpServer()
        {
            SmtpServer server = new SmtpServer();

            server.ServerName = FoeServerRegistry.Get("SmtpServerName");
            server.Port = Convert.ToInt32(FoeServerRegistry.Get("SmtpPort"));
            server.AuthRequired = (FoeServerRegistry.Get("SmtpAuthRequired").ToUpper().CompareTo("T") == 0);
            server.SslEnabled = (FoeServerRegistry.Get("SmtpSslEnabled").ToUpper().CompareTo("T") == 0);
            server.UserName = FoeServerRegistry.Get("SmtpUserName");
            server.Password = FoeServerRegistry.Get("SmtpPassword");

            return server;
        }

        public static PopServer GetDefaultPopServer()
        {
            PopServer server = new PopServer();

            server.ServerName = FoeServerRegistry.Get("PopServerName");
            server.Port = Convert.ToInt32(FoeServerRegistry.Get("PopPort"));
            server.SslEnabled = (FoeServerRegistry.Get("PopSslEnabled").ToUpper().CompareTo("T") == 0);
            server.UserName = FoeServerRegistry.Get("PopUserName");
            server.Password = FoeServerRegistry.Get("PopPassword");

            return server;
        }
    }
}
