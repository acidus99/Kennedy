using System;
using System.Net.Sockets;
using System.Threading;

namespace Gemi.Net.Utils
{
    /// <summary>
    /// Utility class to try and do a TCP connect to a host and port, but to timeout
    /// during the connect if too much time passes
    /// 
    /// </summary>
    public class TimeoutSocket
    {
        private bool IsConnectionSuccessful = false;
        private Exception socketexception;
        private ManualResetEvent TimeoutObject = new ManualResetEvent(false);

        public TcpClient Connect(string host, int port, int timeoutMSec)
        {
            TimeoutObject.Reset();
            socketexception = null;

            TcpClient tcpclient = new TcpClient();

            tcpclient.BeginConnect(host, port,
                new AsyncCallback(CallBackMethod), tcpclient);

            if (TimeoutObject.WaitOne(timeoutMSec, false))
            {
                if (IsConnectionSuccessful)
                {
                    return tcpclient;
                }
                else
                {
                    throw socketexception;
                }
            }
            else
            {
                tcpclient.Close();
                throw new TimeoutException("TimeOut Exception");
            }
        }
        private void CallBackMethod(IAsyncResult asyncresult)
        {
            try
            {
                IsConnectionSuccessful = false;
                TcpClient tcpclient = asyncresult.AsyncState as TcpClient;

                if (tcpclient.Client != null)
                {
                    tcpclient.EndConnect(asyncresult);
                    IsConnectionSuccessful = true;
                }
            }
            catch (Exception ex)
            {
                IsConnectionSuccessful = false;
                socketexception = ex;
            }
            finally
            {
                TimeoutObject.Set();
            }
        }
    }
}
